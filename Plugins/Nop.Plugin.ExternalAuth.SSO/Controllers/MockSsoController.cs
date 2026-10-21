#if DEBUG
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Http;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Configuration;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.ExternalAuth.SSO.Controllers;

/// <summary>
/// Dev-only mock IdP. Lets a developer exercise the SSO login flow end-to-end without
/// a working Azure AD tenant or SP/IdP certificates.
/// </summary>
/// <remarks>
/// <para>
/// The entire controller is wrapped in <c>#if DEBUG</c> so the routes do not exist in
/// Release builds at all. A second gate — <see cref="SsoConfigurationSettings.EnableMockMode"/>
/// per-store — must be on for any action to do real work; when it's off, every action
/// returns 404 so the surface is invisible to an attacker even on a Debug build.
/// </para>
/// <para>
/// Architectural rule: the mock never touches the production cryptographic path. There is
/// no forged SAML XML, no forged JWT, no relaxation of signature/audience checks anywhere
/// in <c>SamlResponseProcessor</c> or the OIDC handler. Instead the mock builds a
/// <see cref="ClaimsPrincipal"/> from form fields and hands it directly to
/// <c>ISsoUserProvisioningService.HandleAsync</c> — the same protocol-agnostic chokepoint
/// the real callbacks use. See References/SSO_Plugin_Reference.md §9.
/// </para>
/// </remarks>
[Route("mocksso")]
[AllowAnonymous]
[AutoValidateAntiforgeryToken]
public class MockSsoController : BasePluginController
{
    #region Fields

    protected readonly ISettingService _settingService;
    protected readonly ISsoEventService _eventService;
    protected readonly ISsoUserProvisioningService _provisioningService;
    protected readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public MockSsoController(
        ISettingService settingService,
        ISsoEventService eventService,
        ISsoUserProvisioningService provisioningService,
        IStoreContext storeContext)
    {
        _settingService = settingService;
        _eventService = eventService;
        _provisioningService = provisioningService;
        _storeContext = storeContext;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Shared completion path: validates the mock is enabled, handles the failure scenario
    /// up front, and otherwise hands a fresh <see cref="ClaimsPrincipal"/> to the
    /// provisioning service.
    /// </summary>
    private async Task<IActionResult> CompleteAsync(MockLoginModel model, string protocolName)
    {
        if (!await IsMockEnabledAsync())
            return NotFound();

        var store = await _storeContext.GetCurrentStoreAsync();

        // Failure scenario short-circuits before provisioning runs — matches the
        // legacy 4.1 mock and exercises the audit + redirect path.
        if (string.Equals(model.Scenario, SsoMockScenarios.Failure, StringComparison.OrdinalIgnoreCase))
        {
            await _eventService.LogFailureAsync(protocolName, SsoFailureReasons.MockFailure,
                "[MOCK] Failure scenario — mock IdP rejected the user before provisioning.",
                customerEmail: model.Email, idpSubject: model.Subject);

            return RedirectToRoute(NopRouteNames.General.LOGIN, new { returnUrl = model.ReturnUrl });
        }

        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);
        var principal = BuildPrincipal(model, settings, protocolName);

        return await _provisioningService.HandleAsync(
            principal,
            protocolName,
            accessToken: null,
            model.ReturnUrl);
    }

    /// <summary>
    /// Construct a <see cref="ClaimsPrincipal"/> whose claim types match the per-store
    /// claim mapping configuration. This lets the existing provisioning service walk the
    /// claims with no protocol-specific code in the mock path.
    /// </summary>
    private static ClaimsPrincipal BuildPrincipal(MockLoginModel model, SsoConfigurationSettings settings, string protocolName)
    {
        var email = (model.Email ?? string.Empty).Trim();
        var firstName = (model.FirstName ?? string.Empty).Trim();
        var lastName = (model.LastName ?? string.Empty).Trim();
        var subject = string.IsNullOrWhiteSpace(model.Subject) ? email : model.Subject.Trim();
        var username = email;

        var claims = new List<Claim>
        {
            // NameIdentifier — read directly by SsoUserProvisioningService for the subject.
            new(ClaimTypes.NameIdentifier, subject),
        };

        AddClaim(claims, settings.EmailClaimType, ClaimTypes.Email, email);
        AddClaim(claims, settings.FirstNameClaimType, ClaimTypes.GivenName, firstName);
        AddClaim(claims, settings.LastNameClaimType, ClaimTypes.Surname, lastName);
        AddClaim(claims, settings.UsernameClaimType, ClaimTypes.Name, username);

        // Groups only emit for the with-groups scenario. Splits + trims so admins can
        // type "guid1, guid2, guid3" without worrying about whitespace.
        if (string.Equals(model.Scenario, SsoMockScenarios.SuccessWithGroups, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(model.Groups))
        {
            var groupClaimType = string.IsNullOrWhiteSpace(settings.GroupsClaimType)
                ? "groups"
                : settings.GroupsClaimType;

            foreach (var g in model.Groups.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                claims.Add(new Claim(groupClaimType, g));
        }

        // Auth-type string keeps the principal IsAuthenticated true (a null/empty
        // authentication type renders the identity unauthenticated). The string is
        // diagnostic only — nothing downstream branches on it.
        var identity = new ClaimsIdentity(claims, $"Mock-{protocolName}");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Emit a claim under the per-store configured claim type and (when different) the
    /// canonical ASP.NET Core schema URI as well, so the provisioning service's
    /// fallback lookup also finds it.
    /// </summary>
    private static void AddClaim(List<Claim> claims, string? primaryType, string fallbackType, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var primary = string.IsNullOrWhiteSpace(primaryType) ? fallbackType : primaryType!;
        claims.Add(new Claim(primary, value));

        if (!string.Equals(primary, fallbackType, StringComparison.Ordinal))
            claims.Add(new Claim(fallbackType, value));
    }

    /// <summary>
    /// True when the per-store <see cref="SsoConfigurationSettings.EnableMockMode"/> is on.
    /// The whole controller is already <c>#if DEBUG</c> — this is the second gate.
    /// </summary>
    private async Task<bool> IsMockEnabledAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);
        return settings.EnableMockMode;
    }

    #endregion

    #region Methods

    /// <summary>
    /// GET /mocksso/login — renders the mock IdP UI. <paramref name="protocol"/> drives
    /// which complete-endpoint the form posts to (<c>saml</c> or <c>oidc</c>).
    /// </summary>
    [HttpGet("login")]
    public async Task<IActionResult> Login(string? protocol, string? returnUrl)
    {
        if (!await IsMockEnabledAsync())
            return NotFound();

        var resolvedProtocol = string.Equals(protocol, MockSsoDefaults.ProtocolOidc, StringComparison.OrdinalIgnoreCase)
            ? MockSsoDefaults.ProtocolOidc
            : MockSsoDefaults.ProtocolSaml;

        var model = new MockLoginModel
        {
            Scenario = SsoMockScenarios.SuccessExisting,
            ReturnUrl = returnUrl ?? string.Empty
        };

        ViewBag.Protocol = resolvedProtocol;
        ViewBag.CompletePath = resolvedProtocol == MockSsoDefaults.ProtocolOidc
            ? MockSsoDefaults.OidcCompletePath
            : MockSsoDefaults.SamlCompletePath;

        return View("~/Plugins/ExternalAuth.SSO/Views/MockSso/Login.cshtml", model);
    }

    /// <summary>
    /// POST /mocksso/saml/complete — receives the mock SAML form, builds a SAML-flavoured
    /// <see cref="ClaimsPrincipal"/>, and delegates to the provisioning service.
    /// </summary>
    [HttpPost("saml/complete")]
    public Task<IActionResult> SamlComplete(MockLoginModel model)
        => CompleteAsync(model, SsoProtocolNames.Saml2);

    /// <summary>
    /// POST /mocksso/oidc/complete — receives the mock OIDC form, builds an OIDC-flavoured
    /// <see cref="ClaimsPrincipal"/>, and delegates to the provisioning service.
    /// </summary>
    [HttpPost("oidc/complete")]
    public Task<IActionResult> OidcComplete(MockLoginModel model)
        => CompleteAsync(model, SsoProtocolNames.Oidc);

    #endregion
}
#endif
