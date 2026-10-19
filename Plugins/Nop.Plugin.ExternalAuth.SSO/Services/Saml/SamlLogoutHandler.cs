using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens.Saml2;
using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Services.Authentication;
using Nop.Services.Common;
using Nop.Services.Customers;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Default implementation of <see cref="ISamlLogoutHandler"/>.
/// </summary>
public class SamlLogoutHandler : ISamlLogoutHandler
{
    #region Fields

    private readonly IAuthenticationService _authenticationService;
    private readonly ICustomerService _customerService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISaml2ConfigurationFactory _configFactory;
    private readonly ISsoEventService _eventService;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public SamlLogoutHandler(
        IAuthenticationService authenticationService,
        ICustomerService customerService,
        IGenericAttributeService genericAttributeService,
        IHttpContextAccessor httpContextAccessor,
        ISaml2ConfigurationFactory configFactory,
        ISsoEventService eventService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _authenticationService = authenticationService;
        _customerService = customerService;
        _genericAttributeService = genericAttributeService;
        _httpContextAccessor = httpContextAccessor;
        _configFactory = configFactory;
        _eventService = eventService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<IActionResult> InitiateAsync()
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var customer = await _workContext.GetCurrentCustomerAsync();

        if (customer is null || !await _customerService.IsRegisteredAsync(customer))
            return new RedirectResult("/");

        var nameId = await _genericAttributeService.GetAttributeAsync<string>(customer, SsoCustomerAttributes.SamlNameId);
        var sessionIndex = await _genericAttributeService.GetAttributeAsync<string>(customer, SsoCustomerAttributes.SamlSessionIndex);

        if (string.IsNullOrEmpty(nameId))
        {
            // No SAML state on the customer — just sign out locally and bail.
            await _authenticationService.SignOutAsync();
            return new RedirectResult("/");
        }

        var config = await _configFactory.CreateForCurrentStoreAsync();

        if (config.SingleLogoutDestination is null)
        {
            // IdP SLO URL not configured. Sign out locally only; the IdP session lingers.
            await _authenticationService.SignOutAsync();
            return new RedirectResult("/");
        }

        var logoutRequest = new Saml2LogoutRequest(config)
        {
            NameId = new Saml2NameIdentifier(nameId),
            SessionIndex = sessionIndex
        };

        var requestId = logoutRequest.Id?.Value ?? Guid.NewGuid().ToString();
        IssueLogoutCorrelationCookie(requestId);

        // Sign out locally before redirecting so the local session ends even if the IdP
        // round-trip is interrupted (browser close, network failure, etc.).
        await ClearSamlAttributesAsync(customer);
        await _authenticationService.SignOutAsync();

        await _eventService.LogAsync(
            eventType: SsoAuthEventTypes.Slo,
            protocol: SsoProtocolNames.Saml2,
            storeId: store.Id,
            customerId: customer.Id,
            customerEmail: customer.Email ?? string.Empty,
            idpSubject: nameId,
            notes: $"SP-initiated SLO; LogoutRequest id={requestId}");

        var binding = new Saml2RedirectBinding();
        binding.Bind(logoutRequest);
        return binding.ToActionResult();
    }

    /// <inheritdoc />
    public async Task<IActionResult> HandleLogoutRequestAsync(HttpRequest request)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var config = await _configFactory.CreateForCurrentStoreAsync();

        var logoutRequest = new Saml2LogoutRequest(config);

        try
        {
            // ITfoxtec auto-detects redirect vs POST binding from the request shape.
            new Saml2PostBinding().Unbind(request.ToGenericHttpRequest(), logoutRequest);
        }
        catch
        {
            // Fall back to redirect binding (IdP sometimes uses HTTP-Redirect for logout).
            try
            {
                new Saml2RedirectBinding().Unbind(request.ToGenericHttpRequest(), logoutRequest);
            }
            catch (Exception ex)
            {
                await _eventService.LogAsync(
                    eventType: SsoAuthEventTypes.Failed,
                    protocol: SsoProtocolNames.Saml2,
                    storeId: store.Id,
                    customerId: null,
                    customerEmail: string.Empty,
                    idpSubject: string.Empty,
                    notes: SsoFailureReasons.Format(SsoFailureReasons.SloRequestFailed,
                        $"IdP-initiated SLO request validation failed: {ex.Message}"));
                return new RedirectResult("/");
            }
        }

        // Sign out locally — the IdP has already cleared its end.
        var customer = await _workContext.GetCurrentCustomerAsync();
        if (customer is not null)
            await ClearSamlAttributesAsync(customer);
        await _authenticationService.SignOutAsync();

        await _eventService.LogAsync(
            eventType: SsoAuthEventTypes.Slo,
            protocol: SsoProtocolNames.Saml2,
            storeId: store.Id,
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
        var store = await _storeContext.GetCurrentStoreAsync();
        var config = await _configFactory.CreateForCurrentStoreAsync();

        var logoutResponse = new Saml2LogoutResponse(config);

        try
        {
            new Saml2RedirectBinding().Unbind(request.ToGenericHttpRequest(), logoutResponse);
        }
        catch (Exception ex)
        {
            await _eventService.LogAsync(
                eventType: SsoAuthEventTypes.Failed,
                protocol: SsoProtocolNames.Saml2,
                storeId: store.Id,
                customerId: null,
                customerEmail: string.Empty,
                idpSubject: string.Empty,
                notes: SsoFailureReasons.Format(SsoFailureReasons.SloResponseFailed,
                    $"SP-initiated SLO response validation failed: {ex.Message}"));
            ClearLogoutCorrelationCookie();
            return new RedirectResult("/");
        }

        // Validate InResponseTo if the cookie is present.
        var expectedRequestId = ReadLogoutCorrelationCookie();
        var actualInResponseTo = logoutResponse.InResponseToAsString;
        if (!string.IsNullOrEmpty(expectedRequestId)
            && !string.Equals(actualInResponseTo, expectedRequestId, StringComparison.Ordinal))
        {
            await _eventService.LogAsync(
                eventType: SsoAuthEventTypes.Failed,
                protocol: SsoProtocolNames.Saml2,
                storeId: store.Id,
                customerId: null,
                customerEmail: string.Empty,
                idpSubject: string.Empty,
                notes: SsoFailureReasons.Format(SsoFailureReasons.SloResponseFailed,
                    $"SLO InResponseTo mismatch (expected {expectedRequestId}, got {actualInResponseTo ?? "<null>"})."));
            ClearLogoutCorrelationCookie();
            return new RedirectResult("/");
        }

        ClearLogoutCorrelationCookie();

        // Local sign-out already happened in InitiateAsync. This handler is just the
        // confirmation hop — log a follow-up note and land the user on the home page.
        return new RedirectResult("/");
    }

    #endregion

    #region Utilities

    private async Task ClearSamlAttributesAsync(Nop.Core.Domain.Customers.Customer customer)
    {
        await _genericAttributeService.SaveAttributeAsync<string?>(customer, SsoCustomerAttributes.SamlNameId, null);
        await _genericAttributeService.SaveAttributeAsync<string?>(customer, SsoCustomerAttributes.SamlSessionIndex, null);
        await _genericAttributeService.SaveAttributeAsync<string?>(customer, SsoCustomerAttributes.SignInProtocol, null);
    }

    private void IssueLogoutCorrelationCookie(string requestId)
    {
        var response = _httpContextAccessor.HttpContext?.Response;
        if (response is null) return;

        response.Cookies.Append(
            SSOAuthenticationDefaults.SamlLogoutCorrelationCookieName,
            requestId,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = response.HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.Add(SSOAuthenticationDefaults.SamlCorrelationCookieLifetime),
                IsEssential = true
            });
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
            new CookieOptions
            {
                HttpOnly = true,
                Secure = response.HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            });
    }

    #endregion
}
