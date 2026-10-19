using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Nop.Core.Infrastructure;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Authentication.External;
using Microsoft.Extensions.DependencyInjection;

namespace Nop.Plugin.ExternalAuth.SSO.Infrastructure;

/// <summary>
/// Registers the plugin's OpenID Connect authentication scheme with the ASP.NET Core auth builder.
/// </summary>
/// <remarks>
/// <para>
/// Scheme name: <see cref="SSOAuthenticationDefaults.OidcAuthenticationScheme"/>.
/// Options are populated from <see cref="SsoConfigurationSettings"/> resolved via
/// <see cref="EngineContext"/> when the scheme is first activated.
/// </para>
/// <para>
/// <b>Known M4 limitation:</b> options are cached per scheme name. When an admin changes
/// per-store OIDC settings, the application must be restarted (or the options cache cleared)
/// for the new values to take effect. Full per-store dynamic options binding via
/// <c>IPostConfigureOptions&lt;OpenIdConnectOptions&gt;</c> is deferred — see SSOPlan.v2.md §3.5.
/// </para>
/// </remarks>
public class OidcAuthenticationRegistrar : IExternalAuthenticationRegistrar
{
    /// <summary>
    /// Configure the OIDC scheme.
    /// </summary>
    public void Configure(AuthenticationBuilder builder)
    {
        builder.AddOpenIdConnect(SSOAuthenticationDefaults.OidcAuthenticationScheme, options =>
        {
            var settings = EngineContext.Current.Resolve<SsoConfigurationSettings>();
            var secrets = EngineContext.Current.Resolve<ISsoSecretsService>();

            // OpenIdConnectOptions.Validate() throws ArgumentException when Authority or
            // ClientId is empty. The OIDC handler is a request-handler scheme — ASP.NET Core
            // initialises it on EVERY request so it can check whether the URL matches the
            // configured CallbackPath. That means an unconfigured plugin would crash every
            // request to the site, not just the SSO login path.
            //
            // To keep the scheme registered cleanly when an admin hasn't filled in OIDC yet
            // (or the active store has no per-store override), we fall back to placeholder
            // values that pass Validate(). The handler still only activates on its CallbackPath
            // (HandleRequestAsync returns false for any other URL), so non-callback traffic is
            // unaffected. If an admin clicks the public "Sign in with Microsoft" button before
            // configuring OIDC the actual OAuth call will fail and OnRemoteFailure below
            // redirects back to /login with a notification.
            options.Authority = string.IsNullOrWhiteSpace(settings.Authority)
                ? "https://login.microsoftonline.com/common/v2.0"
                : settings.Authority;
            options.ClientId = string.IsNullOrWhiteSpace(settings.ClientId)
                ? "00000000-0000-0000-0000-000000000000"
                : settings.ClientId;

            // Decrypt client secret. Empty unprotect result keeps the handler off — the public
            // login button view component is also expected to hide itself in that case.
            options.ClientSecret = secrets.TryUnprotect(settings.ClientSecret, out var decryptedSecret)
                ? decryptedSecret
                : string.Empty;

            options.CallbackPath = string.IsNullOrWhiteSpace(settings.CallbackPath)
                ? "/signin-microsoft"
                : settings.CallbackPath;

            options.ResponseType = "code";
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;

            // M6.c — federated sign-out. SignedOutCallbackPath is where Entra redirects after
            // it clears its own session in response to our end_session_endpoint hit. The
            // OIDC handler intercepts this path automatically; no action method needed.
            options.SignedOutCallbackPath = SSOAuthenticationDefaults.OidcSignedOutCallbackPath;
            options.SignedOutRedirectUri = "/";

            // Configure scopes — start with the handler's defaults (openid) and add the rest.
            var configuredScopes = (settings.Scopes ?? "openid profile email")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var scope in configuredScopes)
                if (!options.Scope.Contains(scope))
                    options.Scope.Add(scope);

            // Map the OIDC "name" claim to ClaimTypes.Name (handler default also does this,
            // but be explicit for downstream FindFirstValue lookups in the provisioning service).
            options.TokenValidationParameters.NameClaimType = "name";

            options.Events = new OpenIdConnectEvents
            {
                // Convert remote auth failures into a clean redirect back to the login page.
                // Without HandleResponse the handler tries to render a 500 error page.
                OnRemoteFailure = context =>
                {
                    context.HandleResponse();
                    var errorUrl = context.Properties?.GetString(SSOAuthenticationDefaults.ErrorCallback)
                                   ?? "/login";
                    context.Response.Redirect(errorUrl);
                    return Task.CompletedTask;
                }
            };
        });
    }
}
