using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Configuration;
using Nop.Services.Helpers;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Default implementation of <see cref="ISamlChallengeBuilder"/>.
/// </summary>
public class SamlChallengeBuilder : ISamlChallengeBuilder
{
    #region Fields

    protected readonly IHttpContextAccessor _httpContextAccessor;
    protected readonly ISaml2ConfigurationFactory _configFactory;
    protected readonly ISettingService _settingService;
    protected readonly ISsoEventService _eventService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public SamlChallengeBuilder(
        IHttpContextAccessor httpContextAccessor,
        ISaml2ConfigurationFactory configFactory,
        ISettingService settingService,
        ISsoEventService eventService,
        IStoreContext storeContext,
        IWebHelper webHelper)
    {
        _httpContextAccessor = httpContextAccessor;
        _configFactory = configFactory;
        _settingService = settingService;
        _eventService = eventService;
        _storeContext = storeContext;
        _webHelper = webHelper;
    }

    #endregion

    #region Utilities

    private string ResolveAcsUrl(SsoConfigurationSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.AcsUrl))
            return settings.AcsUrl;

        var baseUrl = !string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? settings.BaseUrl.TrimEnd('/')
            : _webHelper.GetStoreLocation().TrimEnd('/');

        return baseUrl + SSOAuthenticationDefaults.SamlAcsPath;
    }

    private void IssueCorrelationCookie(string requestId, string? returnUrl)
    {
        var response = _httpContextAccessor.HttpContext?.Response;
        if (response is null) return;

        // Pack request ID + returnUrl into the cookie value as a pipe-delimited tuple.
        // No need for a full key-value codec; the two parts are short and well-formed.
        var safeReturnUrl = returnUrl ?? string.Empty;
        var cookieValue = $"{requestId}|{Uri.EscapeDataString(safeReturnUrl)}";

        // SameSite=None over HTTPS: the IdP returns with a cross-site POST (see SamlCookieOptions).
        response.Cookies.Append(
            SSOAuthenticationDefaults.SamlCorrelationCookieName,
            cookieValue,
            SamlCookieOptions.For(response.HttpContext.Request, SSOAuthenticationDefaults.SamlCorrelationCookieLifetime));
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<IActionResult> BuildChallengeAsync(string? returnUrl)
    {
        var config = await _configFactory.CreateForCurrentStoreAsync();
        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);

        var acsUrl = ResolveAcsUrl(settings);

        // Build the AuthnRequest. The Id is a fresh random GUID — ITfoxtec generates one
        // automatically when constructing Saml2AuthnRequest, but we capture it for
        // InResponseTo correlation on the ACS endpoint.
        var authnRequest = new Saml2AuthnRequest(config)
        {
            ForceAuthn = false,
            IsPassive = false,
            NameIdPolicy = new NameIdPolicy
            {
                AllowCreate = true,
                Format = NameIdentifierFormats.Persistent.OriginalString
            },
            AssertionConsumerServiceUrl = new Uri(acsUrl),
            // The ACS accepts POST only, as the SAML web SSO profile requires for responses. Say so:
            // an IdP that is not told may answer with the binding the request came on (Keycloak
            // sends the response in the URL), which the ACS then refuses.
            ProtocolBinding = ProtocolBindings.HttpPost
        };

        var requestId = authnRequest.Id?.Value ?? Guid.NewGuid().ToString();

        // Persist request ID + returnUrl in the correlation cookie. ACS reads them back to
        // validate InResponseTo and to redirect to the originally-requested page.
        IssueCorrelationCookie(requestId, returnUrl);

        // Pre-redirect audit row — Login event records that the challenge was initiated.
        await _eventService.LogAsync(
            eventType: SsoAuthEventTypes.Login,
            protocol: SsoProtocolNames.Saml2,
            notes: $"SAML AuthnRequest id={requestId}");

        // Bind and return. Most Entra SAML apps use HTTP-Redirect for AuthnRequest; we
        // honour the per-store setting in case the IdP requires POST.
        // ITfoxtec.Identity.Saml2.MvcCore only ships a ToActionResult() extension for
        // Saml2RedirectBinding — for POST binding we surface the auto-submitting form
        // HTML (PostContent) as a ContentResult.
        if (settings.LoginBinding == SamlBinding.HttpPost)
        {
            var postBinding = new Saml2PostBinding();
            postBinding.Bind(authnRequest);
            return new ContentResult
            {
                Content = postBinding.PostContent,
                ContentType = "text/html"
            };
        }

        var redirectBinding = new Saml2RedirectBinding();
        redirectBinding.Bind(authnRequest);
        // Not ToActionResult(): nop's redirect executor would URL-decode the SAMLRequest (see SamlRedirectResult).
        return new SamlRedirectResult(redirectBinding.RedirectLocation);
    }

    #endregion
}
