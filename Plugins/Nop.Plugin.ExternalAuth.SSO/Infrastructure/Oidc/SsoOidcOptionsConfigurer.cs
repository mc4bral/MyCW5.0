using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Helpers;

namespace Nop.Plugin.ExternalAuth.SSO.Infrastructure.Oidc;

/// <summary>
/// Fills the plugin's <see cref="OpenIdConnectOptions"/> from the current store's
/// <see cref="SsoConfigurationSettings"/>.
/// </summary>
/// <remarks>
/// Runs once per store and cache lifetime — <see cref="StoreAwareOidcOptionsCache"/> keys the
/// options by store, so the factory calls this for each store separately. The static parts of the
/// options (response type, token saving, events) stay in <see cref="OidcAuthenticationRegistrar"/>.
/// </remarks>
public class SsoOidcOptionsConfigurer : IConfigureNamedOptions<OpenIdConnectOptions>
{
    #region Fields

    /// <summary>Authority used when the store has none, so <c>Validate()</c> passes.</summary>
    public const string PlaceholderAuthority = "https://login.microsoftonline.com/common/v2.0";

    /// <summary>Client ID used when the store has none, so <c>Validate()</c> passes.</summary>
    public const string PlaceholderClientId = "00000000-0000-0000-0000-000000000000";

    private const string DefaultScopes = "openid profile email";
    private const string DefaultCallbackPath = "/signin-microsoft";

    protected readonly IHttpContextAccessor _httpContextAccessor;

    #endregion

    #region Ctor

    public SsoOidcOptionsConfigurer(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// A store-relative path (<c>/</c> when empty or not local), for the post-logout landing page.
    /// </summary>
    private static string LocalPathOrRoot(IWebHelper? webHelper, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "/";

        var trimmed = url.Trim();
        return webHelper?.CheckIsLocalUrl(trimmed) == true ? trimmed : "/";
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public void Configure(OpenIdConnectOptions options)
        => Configure(Microsoft.Extensions.Options.Options.DefaultName, options);

    /// <inheritdoc />
    public void Configure(string? name, OpenIdConnectOptions options)
    {
        if (!string.Equals(name, SSOAuthenticationDefaults.OidcAuthenticationScheme, StringComparison.Ordinal))
            return;

        // The OIDC handler is initialised on every request, including requests for stores that
        // have no OIDC settings. Those stores get placeholder values: the handler only acts on its
        // CallbackPath, so nothing else is affected, and a sign-in attempt fails at the IdP and
        // comes back through OnRemoteFailure. Without the placeholders Validate() would throw on
        // every page of such a store.
        var services = _httpContextAccessor.HttpContext?.RequestServices;

        // nop registers each ISettings class as scoped, loaded for the request's store.
        var settings = services?.GetService<SsoConfigurationSettings>() ?? new SsoConfigurationSettings();
        var secrets = services?.GetService<ISsoSecretsService>();
        var webHelper = services?.GetService<IWebHelper>();

        options.Authority = string.IsNullOrWhiteSpace(settings.Authority) ? PlaceholderAuthority : settings.Authority.Trim();
        options.ClientId = string.IsNullOrWhiteSpace(settings.ClientId) ? PlaceholderClientId : settings.ClientId.Trim();
        options.ClientSecret = secrets is not null && secrets.TryUnprotect(settings.ClientSecret, out var clientSecret)
            ? clientSecret
            : string.Empty;

        options.CallbackPath = string.IsNullOrWhiteSpace(settings.CallbackPath) ? DefaultCallbackPath : settings.CallbackPath.Trim();
        options.SignedOutCallbackPath = SSOAuthenticationDefaults.OidcSignedOutCallbackPath;
        options.SignedOutRedirectUri = LocalPathOrRoot(webHelper, settings.PostLogoutRedirectUrl);

        var scopes = string.IsNullOrWhiteSpace(settings.Scopes) ? DefaultScopes : settings.Scopes;
        foreach (var scope in scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (!options.Scope.Contains(scope))
                options.Scope.Add(scope);
    }

    #endregion
}
