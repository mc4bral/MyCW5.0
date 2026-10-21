using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens.Saml2;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Authentication;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Helpers;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Default implementation of <see cref="ISamlLogoutHandler"/>.
/// </summary>
public class SamlLogoutHandler : ISamlLogoutHandler
{
    #region Fields

    protected readonly IAuthenticationService _authenticationService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly IHttpContextAccessor _httpContextAccessor;
    protected readonly ISaml2ConfigurationFactory _configFactory;
    protected readonly ISettingService _settingService;
    protected readonly ISsoEventService _eventService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWebHelper _webHelper;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public SamlLogoutHandler(
        IAuthenticationService authenticationService,
        IGenericAttributeService genericAttributeService,
        IHttpContextAccessor httpContextAccessor,
        ISaml2ConfigurationFactory configFactory,
        ISettingService settingService,
        ISsoEventService eventService,
        IStoreContext storeContext,
        IWebHelper webHelper,
        IWorkContext workContext)
    {
        _authenticationService = authenticationService;
        _genericAttributeService = genericAttributeService;
        _httpContextAccessor = httpContextAccessor;
        _configFactory = configFactory;
        _settingService = settingService;
        _eventService = eventService;
        _storeContext = storeContext;
        _webHelper = webHelper;
        _workContext = workContext;
    }

    #endregion

    #region Utilities

    private async Task ClearSsoAttributesAsync(Customer customer)
    {
        await _genericAttributeService.SaveAttributeAsync<string?>(customer, SsoCustomerAttributes.SamlNameId, null);
        await _genericAttributeService.SaveAttributeAsync<string?>(customer, SsoCustomerAttributes.SamlSessionIndex, null);
        await _genericAttributeService.SaveAttributeAsync<string?>(customer, SsoCustomerAttributes.SignInProtocol, null);
        await _genericAttributeService.SaveAttributeAsync<string?>(customer, SsoCustomerAttributes.OidcIdToken, null);
    }

    private void IssueLogoutCorrelationCookie(string requestId)
    {
        var response = _httpContextAccessor.HttpContext?.Response;
        if (response is null) return;

        response.Cookies.Append(
            SSOAuthenticationDefaults.SamlLogoutCorrelationCookieName,
            requestId,
            SamlCookieOptions.For(response.HttpContext.Request, SSOAuthenticationDefaults.SamlCorrelationCookieLifetime));
    }

    private string? ReadLogoutCorrelationCookie()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        return request?.Cookies.TryGetValue(SSOAuthenticationDefaults.SamlLogoutCorrelationCookieName, out var v) == true
            ? v
            : null;
    }

    private void ClearLogoutCorrelationCookie()
    {
        var response = _httpContextAccessor.HttpContext?.Response;
        response?.Cookies.Delete(
            SSOAuthenticationDefaults.SamlLogoutCorrelationCookieName,
            SamlCookieOptions.For(response.HttpContext.Request));
    }

    /// <summary>Audit the failure and land the customer on the post-logout page.</summary>
    private async Task<IActionResult> FailAsync(string reason, string detail)
    {
        await _eventService.LogFailureAsync(SsoProtocolNames.Saml2, reason, detail);
        return new RedirectResult(await GetPostLogoutUrlAsync());
    }

    /// <summary>
    /// Unbind a SAML message sent with either binding — IdPs differ, and some answer a
    /// redirect-bound request with a POST.
    /// </summary>
    private static void UnbindEither(HttpRequest request, Saml2Request message)
    {
        if (request.HasFormContentType)
            new Saml2PostBinding().Unbind(request.ToGenericHttpRequest(), message);
        else
            new Saml2RedirectBinding().Unbind(request.ToGenericHttpRequest(), message);
    }

    /// <summary>
    /// The same for a SAML response (a <c>LogoutResponse</c>). ITfoxtec's bindings have a separate
    /// <c>Unbind</c> for responses that reads <c>SAMLResponse</c>; overloads are chosen by the declared
    /// type, and ITfoxtec's Saml2LogoutResponse derives from Saml2Request (which derives from
    /// Saml2Response), so the request overload is the closer match. Called through the request helper,
    /// a response always reached it and failed with "Query String does not contain SAMLRequest"
    /// (found against Keycloak, 2026-10-21) — hence a separately named helper.
    /// </summary>
    private static void UnbindResponseEither(HttpRequest request, Saml2Response message)
    {
        if (request.HasFormContentType)
            new Saml2PostBinding().Unbind(request.ToGenericHttpRequest(), message);
        else
            new Saml2RedirectBinding().Unbind(request.ToGenericHttpRequest(), message);
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<string> GetPostLogoutUrlAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);
        var url = settings.PostLogoutRedirectUrl?.Trim();

        return !string.IsNullOrEmpty(url) && _webHelper.CheckIsLocalUrl(url) ? url : "/";
    }

    /// <inheritdoc />
    public async Task<IActionResult> InitiateAsync(Customer customer, string nameId, string? sessionIndex)
    {
        var config = await _configFactory.CreateForCurrentStoreAsync(SamlConfigPurpose.Logout);

        // No SLO endpoint at the IdP: the local sign-out nop already did is all there is.
        if (config.SingleLogoutDestination is null)
            return new RedirectResult(await GetPostLogoutUrlAsync());

        var logoutRequest = new Saml2LogoutRequest(config)
        {
            NameId = new Saml2NameIdentifier(nameId),
            SessionIndex = sessionIndex
        };

        var requestId = logoutRequest.Id?.Value ?? Guid.NewGuid().ToString();
        IssueLogoutCorrelationCookie(requestId);

        await _eventService.LogAsync(
            eventType: SsoAuthEventTypes.Slo,
            protocol: SsoProtocolNames.Saml2,
            customerId: customer.Id,
            customerEmail: customer.Email ?? string.Empty,
            idpSubject: nameId,
            notes: $"SP-initiated SLO; LogoutRequest id={requestId}");

        var binding = new Saml2RedirectBinding();
        binding.Bind(logoutRequest);
        // Not ToActionResult(): nop's redirect executor would URL-decode the SAMLRequest (see SamlRedirectResult).
        return new SamlRedirectResult(binding.RedirectLocation);
    }

    /// <inheritdoc />
    public async Task<IActionResult> HandleLogoutRequestAsync(HttpRequest request)
    {
        var config = await _configFactory.CreateForCurrentStoreAsync(SamlConfigPurpose.Logout);
        var logoutRequest = new Saml2LogoutRequest(config);

        try
        {
            UnbindEither(request, logoutRequest);
        }
        catch (Exception ex)
        {
            return await FailAsync(SsoFailureReasons.SloRequestFailed,
                $"IdP-initiated SLO request validation failed: {ex.Message}");
        }

        // Sign out locally — the IdP has already cleared its end.
        var customer = await _workContext.GetCurrentCustomerAsync();
        if (customer is not null)
            await ClearSsoAttributesAsync(customer);

        await _authenticationService.SignOutAsync();

        await _eventService.LogAsync(
            eventType: SsoAuthEventTypes.Slo,
            protocol: SsoProtocolNames.Saml2,
            customerId: customer?.Id,
            customerEmail: customer?.Email ?? string.Empty,
            idpSubject: logoutRequest.NameId?.Value ?? string.Empty,
            notes: "IdP-initiated SLO completed");

        // Reply with a LogoutResponse so the IdP knows we've cleared our side.
        var logoutResponse = new Saml2LogoutResponse(config)
        {
            InResponseToAsString = logoutRequest.Id?.Value,
            Status = Saml2StatusCodes.Success
        };

        var binding = new Saml2PostBinding();
        binding.Bind(logoutResponse);
        return binding.ToActionResult();
    }

    /// <inheritdoc />
    public async Task<IActionResult> HandleLogoutResponseAsync(HttpRequest request)
    {
        var config = await _configFactory.CreateForCurrentStoreAsync(SamlConfigPurpose.Logout);
        var logoutResponse = new Saml2LogoutResponse(config);

        var expectedRequestId = ReadLogoutCorrelationCookie();
        ClearLogoutCorrelationCookie();

        try
        {
            UnbindResponseEither(request, logoutResponse);
        }
        catch (Exception ex)
        {
            return await FailAsync(SsoFailureReasons.SloResponseFailed,
                $"SP-initiated SLO response validation failed: {ex.Message}");
        }

        var actualInResponseTo = logoutResponse.InResponseToAsString;
        if (!string.IsNullOrEmpty(expectedRequestId)
            && !string.Equals(actualInResponseTo, expectedRequestId, StringComparison.Ordinal))
        {
            return await FailAsync(SsoFailureReasons.SloResponseFailed,
                $"SLO InResponseTo mismatch (expected {expectedRequestId}, got {actualInResponseTo ?? "<null>"}).");
        }

        // The local sign-out happened before the redirect to the IdP; this is the confirmation hop.
        return new RedirectResult(await GetPostLogoutUrlAsync());
    }

    #endregion
}
