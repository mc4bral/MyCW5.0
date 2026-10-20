using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Http;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Services.Saml;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Authentication.External;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.ExternalAuth.SSO.Controllers;

/// <summary>
/// Public-facing controller that drives the SSO challenge and callback flow.
/// </summary>
/// <remarks>
/// Patterned after <c>FacebookAuthenticationController.Login</c> / <c>LoginCallback</c>:
/// the GET <c>Login</c> action issues an ASP.NET Core <c>Challenge</c> against the OIDC
/// scheme; the handler does its OIDC dance with the IdP and redirects back to
/// <c>LoginCallback</c>; that action reads the claims and delegates to
/// <see cref="ISsoUserProvisioningService"/> for the security gates + nop handoff.
///
/// SAML starts here too: <c>Login</c> hands it to <see cref="ISamlChallengeBuilder"/>, and the
/// IdP's response comes back to <c>SamlController</c> (<c>/saml/acs</c>), not to <c>LoginCallback</c>.
/// </remarks>
public class ExternalAuthenticationController : BasePluginController
{
    #region Fields

    protected readonly IAuthenticationPluginManager _authenticationPluginManager;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly ISamlChallengeBuilder _samlChallengeBuilder;
    protected readonly ISettingService _settingService;
    protected readonly ISsoEventService _eventService;
    protected readonly ISsoRateLimiter _rateLimiter;
    protected readonly ISsoUserProvisioningService _provisioningService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public ExternalAuthenticationController(
        IAuthenticationPluginManager authenticationPluginManager,
        ILocalizationService localizationService,
        INotificationService notificationService,
        ISamlChallengeBuilder samlChallengeBuilder,
        ISettingService settingService,
        ISsoEventService eventService,
        ISsoRateLimiter rateLimiter,
        ISsoUserProvisioningService provisioningService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _authenticationPluginManager = authenticationPluginManager;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _samlChallengeBuilder = samlChallengeBuilder;
        _settingService = settingService;
        _eventService = eventService;
        _rateLimiter = rateLimiter;
        _provisioningService = provisioningService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Entry point invoked when the public "Sign in with Microsoft" button is clicked.
    /// Branches by per-store <see cref="SsoConfigurationSettings.Protocol"/>.
    /// </summary>
    public async Task<IActionResult> Login(string? returnUrl)
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        // rate-cap check up front so a runaway redirect loop or scripted
        // enumeration short-circuits at the cheapest layer.
        if (!await _rateLimiter.CheckAndIncrementAsync())
        {
            await _eventService.LogFailureAsync(string.Empty, SsoFailureReasons.LoginRetryCapHit,
                "SSO login rate-limit exceeded for this client IP.");

            _notificationService.WarningNotification(
                await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Login.RateLimited"));
            return RedirectToRoute(NopRouteNames.General.LOGIN, new { returnUrl });
        }

        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);

        // Sanity: plugin must be installed and registered as an active external auth method.
        var methodIsAvailable = await _authenticationPluginManager.IsPluginActiveAsync(
            SSOAuthenticationDefaults.SystemName,
            await _workContext.GetCurrentCustomerAsync(),
            store.Id);

        if (!methodIsAvailable || !settings.IsEnabled || settings.Protocol == SsoProtocol.Off)
        {
            _notificationService.WarningNotification(
                await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Login.NotAvailable"));
            return RedirectToRoute(NopRouteNames.General.LOGIN, new { returnUrl });
        }

#if DEBUG
        // Mock IdP branch (dev-only). When EnableMockMode is on, short-circuit before
        // any real IdP interaction and redirect to the in-plugin mock login page. The
        // entire mock surface is excluded from Release builds via #if DEBUG, so this
        // branch literally does not exist outside dev.
        if (settings.EnableMockMode)
        {
            var mockProtocol = settings.Protocol == SsoProtocol.Saml2
                ? Constants.MockSsoDefaults.ProtocolSaml
                : Constants.MockSsoDefaults.ProtocolOidc;
            return Redirect($"{Constants.MockSsoDefaults.LoginPath}?{Constants.MockSsoDefaults.ProtocolQueryKey}={mockProtocol}&returnUrl={Uri.EscapeDataString(returnUrl ?? string.Empty)}");
        }
#endif

        // SAML branch — delegate to the challenge builder which handles AuthnRequest
        // construction, correlation-cookie issuance, the audit Login row, and the redirect
        // IActionResult creation. Any IdP-side or config-side failure is reported back to
        // the user as a notification + redirect to /login.
        if (settings.Protocol == SsoProtocol.Saml2)
        {
            try
            {
                return await _samlChallengeBuilder.BuildChallengeAsync(returnUrl);
            }
            catch (Exception ex)
            {
                _notificationService.ErrorNotification(
                    string.Format(
                        await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Saml.ConfigurationIncomplete"),
                        ex.Message));
                return RedirectToRoute(NopRouteNames.General.LOGIN, new { returnUrl });
            }
        }

        // OpenID Connect challenge — the handler takes over from here.
        var authenticationProperties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(LoginCallback), "ExternalAuthentication", new { returnUrl })
        };
        authenticationProperties.SetString(
            SSOAuthenticationDefaults.ErrorCallback,
            Url.RouteUrl(NopRouteNames.General.LOGIN, new { returnUrl }));

        // "Login started", as the SAML challenge writes, so both protocols show the same trail.
        await _eventService.LogAsync(
            eventType: SsoAuthEventTypes.Login,
            protocol: SsoProtocolNames.Oidc,
            notes: "OIDC challenge sent to the IdP.");

        return Challenge(authenticationProperties, SSOAuthenticationDefaults.OidcAuthenticationScheme);
    }

    /// <summary>
    /// Callback action invoked by the OIDC handler after the IdP authentication completes.
    /// Pulls claims out of the cookie produced by the handler and hands off to
    /// <see cref="ISsoUserProvisioningService.HandleAsync"/>.
    /// </summary>
    public async Task<IActionResult> LoginCallback(string? returnUrl)
    {

        var authenticateResult = await HttpContext.AuthenticateAsync(
            SSOAuthenticationDefaults.OidcAuthenticationScheme);

        if (!authenticateResult.Succeeded || authenticateResult.Principal is null
            || !authenticateResult.Principal.Claims.Any())
        {
            await _eventService.LogFailureAsync(SsoProtocolNames.Oidc, SsoFailureReasons.OidcCallbackFailed,
                $"OIDC AuthenticateAsync did not succeed: {authenticateResult.Failure?.Message ?? "no principal"}");

            return RedirectToRoute(NopRouteNames.General.LOGIN, new { returnUrl });
        }

        // OIDC handler stores tokens under the standard names "access_token", "id_token",
        // "refresh_token" when SaveTokens = true.
        var accessToken = await HttpContext.GetTokenAsync(
            SSOAuthenticationDefaults.OidcAuthenticationScheme,
            "access_token");
        var idToken = await HttpContext.GetTokenAsync(
            SSOAuthenticationDefaults.OidcAuthenticationScheme,
            "id_token");

        return await _provisioningService.HandleAsync(
            authenticateResult.Principal,
            SsoProtocolNames.Oidc,
            accessToken,
            returnUrl,
            idToken);
    }

    #endregion
}
