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

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISaml2ConfigurationFactory _configFactory;
    private readonly ISettingService _settingService;
    private readonly ISsoEventService _eventService;
    private readonly IStoreContext _storeContext;
    private readonly IWebHelper _webHelper;

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
            AssertionConsumerServiceUrl = new Uri(acsUrl)
        };

        var requestId = authnRequest.Id?.Value ?? Guid.NewGuid().ToString();

        // Persist request ID + returnUrl in the correlation cookie. ACS reads them back to
        // validate InResponseTo and to redirect to the originally-requested page.
        IssueCorrelationCookie(requestId, returnUrl);

        // Pre-redirect audit row — Login event records that the challenge was initiated.
        await _eventService.LogAsync(
            eventType: SsoAuthEventTypes.Login,
            protocol: SsoProtocolNames.Saml2,
            storeId: store.Id,
            customerId: null,
            customerEmail: string.Empty,
            idpSubject: string.Empty,
            notes: $"SAML AuthnRequest id={requestId}");

        // Bind and return. Most Entra SAML apps use HTTP-Redirect for AuthnRequest; we
        // honour the per-store setting in case the IdP requires POST.
        // ITfoxtec.Identity.Saml2.MvcCore only ships a ToActionResult() extension for
        // Saml2RedirectBinding — for POST binding we surface the auto-submitting form
        // HTML (PostContent) as a ContentResult.
        if (IsPostBinding(settings.LoginBinding))
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
        return redirectBinding.ToActionResult();
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

    private static bool IsPostBinding(string? binding)
        => !string.IsNullOrWhiteSpace(binding)
           && binding.Contains("POST", StringComparison.OrdinalIgnoreCase);

    private void IssueCorrelationCookie(string requestId, string? returnUrl)
    {
        var response = _httpContextAccessor.HttpContext?.Response;
        if (response is null) return;

        // Pack request ID + returnUrl into the cookie value as a pipe-delimited tuple.
        // No need for a full key-value codec; the two parts are short and well-formed.
        var safeReturnUrl = returnUrl ?? string.Empty;
        var cookieValue = $"{requestId}|{Uri.EscapeDataString(safeReturnUrl)}";

        response.Cookies.Append(
            SSOAuthenticationDefaults.SamlCorrelationCookieName,
            cookieValue,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = response.HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.Add(SSOAuthenticationDefaults.SamlCorrelationCookieLifetime),
                IsEssential = true
            });
    }

    #endregion
}
