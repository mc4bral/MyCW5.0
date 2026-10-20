using System.Security.Claims;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Http;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Default implementation of <see cref="ISamlResponseProcessor"/>.
/// </summary>
/// <remarks>
/// Every response is validated by ITfoxtec, in every build. The DEBUG mock IdP does not come
/// through here: it hands its principal to the provisioning service directly.
/// </remarks>
public class SamlResponseProcessor : ISamlResponseProcessor
{
    #region Fields

    protected readonly ICustomerService _customerService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly ISaml2ConfigurationFactory _configFactory;
    protected readonly ISettingService _settingService;
    protected readonly ISsoEventService _eventService;
    protected readonly ISsoRateLimiter _rateLimiter;
    protected readonly ISsoUserProvisioningService _provisioningService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public SamlResponseProcessor(
        ICustomerService customerService,
        IGenericAttributeService genericAttributeService,
        ISaml2ConfigurationFactory configFactory,
        ISettingService settingService,
        ISsoEventService eventService,
        ISsoRateLimiter rateLimiter,
        ISsoUserProvisioningService provisioningService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _customerService = customerService;
        _genericAttributeService = genericAttributeService;
        _configFactory = configFactory;
        _settingService = settingService;
        _eventService = eventService;
        _rateLimiter = rateLimiter;
        _provisioningService = provisioningService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Parse the request ID and return URL from the correlation cookie value
    /// (<c>"&lt;requestId&gt;|&lt;escaped-returnUrl&gt;"</c>). Both empty when the cookie is absent.
    /// </summary>
    private static (string requestId, string returnUrl) ReadCorrelationCookie(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(SSOAuthenticationDefaults.SamlCorrelationCookieName, out var raw)
            || string.IsNullOrEmpty(raw))
        {
            return (string.Empty, string.Empty);
        }

        var pipeIndex = raw.IndexOf('|');
        if (pipeIndex < 0)
            return (raw, string.Empty);

        var requestId = raw[..pipeIndex];
        var returnUrl = Uri.UnescapeDataString(raw[(pipeIndex + 1)..]);
        return (requestId, returnUrl);
    }

    private static void ClearCorrelationCookie(HttpResponse response)
    {
        response.Cookies.Delete(
            SSOAuthenticationDefaults.SamlCorrelationCookieName,
            SamlCookieOptions.For(response.HttpContext.Request));
    }

    /// <summary>Audit the failure and send the customer back to the login page.</summary>
    private async Task<IActionResult> FailAsync(string reason, string detail, string returnUrl)
    {
        await _eventService.LogFailureAsync(SsoProtocolNames.Saml2, reason, detail);
        return new RedirectToRouteResult(NopRouteNames.General.LOGIN, new { returnUrl });
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<IActionResult> ProcessAcsAsync(HttpRequest request)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);

        // 1. The correlation cookie — InResponseTo and the original returnUrl. It is absent when
        //    the store did not start this login (IdP-initiated), which the store must allow.
        var (expectedRequestId, returnUrl) = ReadCorrelationCookie(request);

        // Per-IP cap before any XML or signature work: the ACS is anonymous, and each POST costs
        // a parse and a signature check.
        if (!await _rateLimiter.CheckAndIncrementAsync(ISsoRateLimiter.AcsBucket))
            return await FailAsync(SsoFailureReasons.LoginRetryCapHit,
                "SAML ACS rate limit exceeded for this client IP.", returnUrl);
        ClearCorrelationCookie(request.HttpContext.Response);

        if (string.IsNullOrEmpty(expectedRequestId) && !settings.AllowIdpInitiatedLogin)
        {
            return await FailAsync(SsoFailureReasons.AcsPendingCookieInvalid,
                "SAML response without a pending request from this store (IdP-initiated login is off, or the correlation cookie expired or was blocked).",
                returnUrl);
        }

        // 2. Unbind and validate. ITfoxtec checks the signature, digest allowlist (no SHA-1),
        //    audience, NotBefore / NotOnOrAfter and decrypts encrypted assertions.
        var config = await _configFactory.CreateForCurrentStoreAsync(SamlConfigPurpose.Login);
        var saml2AuthnResponse = new Saml2AuthnResponse(config);

        try
        {
            var binding = new Saml2PostBinding();
            binding.ReadSamlResponse(request.ToGenericHttpRequest(), saml2AuthnResponse);

            if (saml2AuthnResponse.Status != Saml2StatusCodes.Success)
            {
                return await FailAsync(SsoFailureReasons.AcsSecurityValidationFailed,
                    $"SAML response status not Success: {saml2AuthnResponse.Status}", returnUrl);
            }

            binding.Unbind(request.ToGenericHttpRequest(), saml2AuthnResponse);
        }
        catch (Exception ex)
        {
            return await FailAsync(SsoFailureReasons.AcsSecurityValidationFailed,
                $"SAML response validation failed: {ex.Message}", returnUrl);
        }

        // 3. InResponseTo must answer this store's pending request.
        var actualInResponseTo = saml2AuthnResponse.InResponseTo?.Value;
        if (!string.IsNullOrEmpty(expectedRequestId)
            && !string.Equals(actualInResponseTo, expectedRequestId, StringComparison.Ordinal))
        {
            return await FailAsync(SsoFailureReasons.AcsPendingCookieInvalid,
                $"InResponseTo mismatch (expected {expectedRequestId}, got {actualInResponseTo ?? "<null>"}).",
                returnUrl);
        }

        // 4. Provisioning: the same gates, linking, sign-in and audit as OIDC.
        var principal = new ClaimsPrincipal(saml2AuthnResponse.ClaimsIdentity);
        var nameId = saml2AuthnResponse.NameId?.Value ?? string.Empty;
        var sessionIndex = saml2AuthnResponse.SessionIndex ?? string.Empty;

        var result = await _provisioningService.HandleAsync(
            principal,
            SsoProtocolNames.Saml2,
            accessToken: null,
            returnUrl);

        // 5. NameID + SessionIndex for SP-initiated SLO, on the customer now signed in. The
        //    provisioning service records the sign-in protocol.
        var customer = await _workContext.GetCurrentCustomerAsync();
        if (customer is not null && await _customerService.IsRegisteredAsync(customer))
        {
            await _genericAttributeService.SaveAttributeAsync(customer, SsoCustomerAttributes.SamlNameId, nameId);
            await _genericAttributeService.SaveAttributeAsync(customer, SsoCustomerAttributes.SamlSessionIndex, sessionIndex);
        }

        return result;
    }

    #endregion
}
