using System.Security.Claims;
using System.Text;
using System.Xml;
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
public class SamlResponseProcessor : ISamlResponseProcessor
{
    #region Fields

    private readonly ICustomerService _customerService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly ISaml2ConfigurationFactory _configFactory;
    private readonly ISettingService _settingService;
    private readonly ISsoEventService _eventService;
    private readonly ISsoUserProvisioningService _provisioningService;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public SamlResponseProcessor(
        ICustomerService customerService,
        IGenericAttributeService genericAttributeService,
        ISaml2ConfigurationFactory configFactory,
        ISettingService settingService,
        ISsoEventService eventService,
        ISsoUserProvisioningService provisioningService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _customerService = customerService;
        _genericAttributeService = genericAttributeService;
        _configFactory = configFactory;
        _settingService = settingService;
        _eventService = eventService;
        _provisioningService = provisioningService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<IActionResult> ProcessAcsAsync(HttpRequest request)
    {
        var store = await _storeContext.GetCurrentStoreAsync();

        // 1. Read the correlation cookie — provides InResponseTo + the original returnUrl.
        var (expectedRequestId, returnUrl) = ReadCorrelationCookie(request);

#if DEBUG
        // Mock-mode bypass. When the per-store EnableMockMode flag is on we skip ITfoxtec's
        // signature / audience / cert / clock-skew checks and parse the forged SAML response
        // with XmlDocument. Mirrors the legacy 4.1 plugin's pattern
        // (SAMLService.ProcessAcsResponse → ProcessMockAcsResponse). The whole branch is
        // gated by #if DEBUG so production binaries can't reach it.
        var mockSettings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);
        if (mockSettings.EnableMockMode)
            return await ProcessMockAcsAsync(request, store.Id, returnUrl, mockSettings);
#endif

        // 2. Build a fresh Saml2Configuration and unbind the response. ITfoxtec validates
        //    signature, digest method allowlist (rejects SHA-1), audience, NotBefore /
        //    NotOnOrAfter (with the 5-min clock skew from the factory), and decrypts
        //    encrypted assertions when DecryptionCertificates is set.
        var config = await _configFactory.CreateForCurrentStoreAsync();
        var saml2AuthnResponse = new Saml2AuthnResponse(config);

        try
        {
            var binding = new Saml2PostBinding();
            binding.ReadSamlResponse(request.ToGenericHttpRequest(), saml2AuthnResponse);

            if (saml2AuthnResponse.Status != Saml2StatusCodes.Success)
            {
                await _eventService.LogAsync(
                    eventType: SsoAuthEventTypes.Failed,
                    protocol: SsoProtocolNames.Saml2,
                    storeId: store.Id,
                    customerId: null,
                    customerEmail: string.Empty,
                    idpSubject: string.Empty,
                    notes: SsoFailureReasons.Format(SsoFailureReasons.AcsSecurityValidationFailed,
                        $"SAML response status not Success: {saml2AuthnResponse.Status}"));
                return new RedirectToRouteResult(NopRouteNames.General.LOGIN, new { returnUrl });
            }

            binding.Unbind(request.ToGenericHttpRequest(), saml2AuthnResponse);
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
                notes: SsoFailureReasons.Format(SsoFailureReasons.AcsSecurityValidationFailed,
                    $"SAML response validation failed: {ex.Message}"));
            return new RedirectToRouteResult(NopRouteNames.General.LOGIN, new { returnUrl });
        }

        // 3. InResponseTo correlation check.
        var actualInResponseTo = saml2AuthnResponse.InResponseTo?.Value;
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
                notes: SsoFailureReasons.Format(SsoFailureReasons.AcsPendingCookieInvalid,
                    $"InResponseTo mismatch (expected {expectedRequestId}, got {actualInResponseTo ?? "<null>"})."));
            ClearCorrelationCookie(request.HttpContext.Response);
            return new RedirectToRouteResult(NopRouteNames.General.LOGIN, new { returnUrl });
        }

        // Correlation OK — drop the cookie so a replayed response can't reuse it.
        ClearCorrelationCookie(request.HttpContext.Response);

        // 4. Hand off to the provisioning pipeline. It runs the same security gates
        //    (domain allowlist, admin-account-link block), audits, and delegates to
        //    IExternalAuthenticationService.AuthenticateAsync.
        var principal = new ClaimsPrincipal(saml2AuthnResponse.ClaimsIdentity);
        var nameId = saml2AuthnResponse.NameId?.Value ?? string.Empty;
        var sessionIndex = saml2AuthnResponse.SessionIndex ?? string.Empty;

        var result = await _provisioningService.HandleAsync(
            principal,
            SsoProtocolNames.Saml2,
            accessToken: null,
            returnUrl);

        // 5. After provisioning, persist NameID + SessionIndex on the customer for SLO.
        var customer = await _workContext.GetCurrentCustomerAsync();
        if (customer is not null && await _customerService.IsRegisteredAsync(customer))
        {
            await _genericAttributeService.SaveAttributeAsync(customer, SsoCustomerAttributes.SamlNameId, nameId);
            await _genericAttributeService.SaveAttributeAsync(customer, SsoCustomerAttributes.SamlSessionIndex, sessionIndex);
            await _genericAttributeService.SaveAttributeAsync(customer, SsoCustomerAttributes.SignInProtocol, SsoProtocolNames.Saml2);
        }

        return result;
    }

#if DEBUG
    /// <summary>
    /// Process a forged SAML response in mock mode. Skips ITfoxtec's signature / audience /
    /// cert / clock-skew validation; parses the SAML XML with an XXE-safe <see cref="XmlDocument"/>;
    /// still honours Status (so the "failure" scenario hits the audit + redirect path); still
    /// extracts claims from the Assertion and dispatches to <c>ISsoUserProvisioningService</c>
    /// — the same chokepoint the real callback uses. Mirrors the legacy 4.1 plugin's
    /// <c>SAMLService.ProcessMockAcsResponse</c>. Whole method excluded from Release builds.
    /// </summary>
    private async Task<IActionResult> ProcessMockAcsAsync(
        HttpRequest request,
        int storeId,
        string returnUrl,
        SsoConfigurationSettings settings)
    {
        // Drop the correlation cookie if any (the mock flow doesn't issue an AuthnRequest
        // so there's nothing to correlate; clearing prevents a stale cookie blocking the
        // real flow on the next request).
        ClearCorrelationCookie(request.HttpContext.Response);

        var samlResponseB64 = request.Form["SAMLResponse"].ToString();
        if (string.IsNullOrWhiteSpace(samlResponseB64))
        {
            await _eventService.LogAsync(
                eventType: SsoAuthEventTypes.Failed,
                protocol: SsoProtocolNames.Saml2,
                storeId: storeId,
                customerId: null,
                customerEmail: string.Empty,
                idpSubject: string.Empty,
                notes: "[SSO-MOCK] ACS received empty SAMLResponse.");
            return new RedirectToRouteResult(NopRouteNames.General.LOGIN, new { returnUrl });
        }

        XmlDocument xml;
        try
        {
            var xmlBytes = Convert.FromBase64String(samlResponseB64);
            var xmlText = Encoding.UTF8.GetString(xmlBytes);
            xml = LoadXmlSafely(xmlText);
        }
        catch (Exception ex)
        {
            await _eventService.LogAsync(
                eventType: SsoAuthEventTypes.Failed,
                protocol: SsoProtocolNames.Saml2,
                storeId: storeId,
                customerId: null,
                customerEmail: string.Empty,
                idpSubject: string.Empty,
                notes: $"[SSO-MOCK] SAML response parse failed: {ex.Message}");
            return new RedirectToRouteResult(NopRouteNames.General.LOGIN, new { returnUrl });
        }

        var ns = CreateSamlNamespaceManager(xml);

        // Honour Status — the "failure" scenario sends Status != Success and we want it to
        // exercise the audit + redirect path just like the real failure path does.
        var statusCodeNode = xml.SelectSingleNode("//samlp:Status/samlp:StatusCode", ns) as XmlElement;
        var statusValue = statusCodeNode?.GetAttribute("Value") ?? string.Empty;
        if (!statusValue.EndsWith(":Success", StringComparison.OrdinalIgnoreCase))
        {
            await _eventService.LogAsync(
                eventType: SsoAuthEventTypes.Failed,
                protocol: SsoProtocolNames.Saml2,
                storeId: storeId,
                customerId: null,
                customerEmail: string.Empty,
                idpSubject: string.Empty,
                notes: $"[SSO-MOCK] SAML Status not Success: {statusValue}");
            return new RedirectToRouteResult(NopRouteNames.General.LOGIN, new { returnUrl });
        }

        var assertion = xml.SelectSingleNode("//saml:Assertion", ns)
            ?? throw new InvalidOperationException("[SSO-MOCK] SAML response has no Assertion.");

        // Build a ClaimsIdentity from the AttributeStatement + NameID. Mock skips
        // InResponseTo / audience / NotBefore / NotOnOrAfter / signature; that is the
        // entire point of mock mode.
        var nameId = assertion.SelectSingleNode("saml:Subject/saml:NameID", ns)?.InnerText ?? string.Empty;
        var claims = new List<Claim>();
        if (!string.IsNullOrEmpty(nameId))
            claims.Add(new Claim(ClaimTypes.NameIdentifier, nameId));

        foreach (XmlElement attr in assertion.SelectNodes("saml:AttributeStatement/saml:Attribute", ns)!)
        {
            var attrName = attr.GetAttribute("Name");
            if (string.IsNullOrWhiteSpace(attrName)) continue;
            foreach (XmlNode v in attr.SelectNodes("saml:AttributeValue", ns)!)
            {
                var value = v.InnerText ?? string.Empty;
                if (string.IsNullOrEmpty(value)) continue;
                claims.Add(new Claim(attrName, value));
                // Also emit the matching fallback claim type so the provisioning service's
                // FindFirst lookup catches it (it tries settings.X first, then ClaimTypes.X).
                if (string.Equals(attrName, settings.EmailClaimType, StringComparison.Ordinal))
                    claims.Add(new Claim(ClaimTypes.Email, value));
                else if (string.Equals(attrName, settings.FirstNameClaimType, StringComparison.Ordinal))
                    claims.Add(new Claim(ClaimTypes.GivenName, value));
                else if (string.Equals(attrName, settings.LastNameClaimType, StringComparison.Ordinal))
                    claims.Add(new Claim(ClaimTypes.Surname, value));
                else if (string.Equals(attrName, settings.UsernameClaimType, StringComparison.Ordinal))
                    claims.Add(new Claim(ClaimTypes.Name, value));
            }
        }

        var identity = new ClaimsIdentity(claims, $"Mock-{SsoProtocolNames.Saml2}");
        var principal = new ClaimsPrincipal(identity);

        var result = await _provisioningService.HandleAsync(
            principal,
            SsoProtocolNames.Saml2,
            accessToken: null,
            returnUrl);

        var customer = await _workContext.GetCurrentCustomerAsync();
        if (customer is not null && await _customerService.IsRegisteredAsync(customer))
        {
            await _genericAttributeService.SaveAttributeAsync(customer, SsoCustomerAttributes.SamlNameId, nameId);
            await _genericAttributeService.SaveAttributeAsync(customer, SsoCustomerAttributes.SignInProtocol, SsoProtocolNames.Saml2);
        }

        return result;
    }

    /// <summary>XXE-safe XML loader for mock SAML response. DTDs prohibited, no external entities.</summary>
    private static XmlDocument LoadXmlSafely(string xmlText)
    {
        var readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        var doc = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
        using var sr = new StringReader(xmlText);
        using var xr = XmlReader.Create(sr, readerSettings);
        doc.Load(xr);
        return doc;
    }

    /// <summary>SAML 2.0 namespace manager for XPath against forged mock responses.</summary>
    private static XmlNamespaceManager CreateSamlNamespaceManager(XmlDocument doc)
    {
        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("samlp", "urn:oasis:names:tc:SAML:2.0:protocol");
        ns.AddNamespace("saml", "urn:oasis:names:tc:SAML:2.0:assertion");
        return ns;
    }
#endif

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
