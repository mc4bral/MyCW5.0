using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Infrastructure.Oidc;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Services.Authentication.External;

namespace Nop.Plugin.ExternalAuth.SSO.Infrastructure;

/// <summary>
/// Registers the plugin's OpenID Connect authentication scheme with the ASP.NET Core auth builder.
/// </summary>
/// <remarks>
/// <para>
/// Scheme name: <see cref="SSOAuthenticationDefaults.OidcAuthenticationScheme"/>. nop creates this
/// class with <c>Activator.CreateInstance</c> while building authentication, so it has no injected
/// dependencies; it only registers.
/// </para>
/// <para>
/// The options are <b>per store</b>. This class sets the parts that are the same everywhere
/// (response type, token saving, claim mapping, events).
/// <see cref="SsoOidcOptionsConfigurer"/> fills the store-specific parts (authority, client,
/// callback paths, scopes) from the current store's settings, and
/// <see cref="StoreAwareOidcOptionsCache"/> keeps one options object per store, so each store can
/// use its own IdP registration and a settings change applies without a restart.
/// </para>
/// </remarks>
public class OidcAuthenticationRegistrar : IExternalAuthenticationRegistrar
{
    /// <summary>
    /// Configure the OIDC scheme and the per-store options plumbing.
    /// </summary>
    /// <param name="builder">Authentication builder</param>
    public void Configure(AuthenticationBuilder builder)
    {
        builder.AddOpenIdConnect(SSOAuthenticationDefaults.OidcAuthenticationScheme, options =>
        {
            options.ResponseType = "code";
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;

            // Map the OIDC "name" claim to ClaimTypes.Name for the provisioning service's lookups.
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
                },

                // The handler looks for the id_token in nop's external cookie, which is usually gone
                // by logout time. LogoutActionsFilter passes the one saved at sign-in instead.
                OnRedirectToIdentityProviderForSignOut = context =>
                {
                    if (context.Properties.Items.TryGetValue(SSOAuthenticationDefaults.IdTokenHintItem, out var idTokenHint)
                        && !string.IsNullOrEmpty(idTokenHint))
                        context.ProtocolMessage.IdTokenHint = idTokenHint;

                    return Task.CompletedTask;
                }
            };
        });

        // Store-specific values. Registered after AddOpenIdConnect so it runs after the delegate
        // above, and before the framework's post-configure step builds the discovery manager.
        builder.Services.AddSingleton<IConfigureOptions<OpenIdConnectOptions>, SsoOidcOptionsConfigurer>();

        // One options object per store. The closed registration takes precedence over the
        // framework's open-generic IOptionsMonitorCache<>; the same instance serves the
        // invalidation interface used when settings are saved.
        builder.Services.AddSingleton<StoreAwareOidcOptionsCache>();
        builder.Services.AddSingleton<IOptionsMonitorCache<OpenIdConnectOptions>>(sp => sp.GetRequiredService<StoreAwareOidcOptionsCache>());
        builder.Services.AddSingleton<ISsoOidcOptionsCache>(sp => sp.GetRequiredService<StoreAwareOidcOptionsCache>());
    }
}
