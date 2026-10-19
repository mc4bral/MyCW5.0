using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Infrastructure;
using Nop.Plugin.ExternalAuth.SSO.Factories;
using Nop.Plugin.ExternalAuth.SSO.Filters;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Services.Saml;
using Nop.Services.Authentication.External;

namespace Nop.Plugin.ExternalAuth.SSO.Infrastructure;

/// <summary>
/// Registers the ExternalAuth.SSO plugin's services with the ASP.NET Core DI container.
/// </summary>
/// <remarks>
/// NopCommerce discovers this class automatically via the <see cref="INopStartup"/> interface.
/// Composition by milestone:
/// <list type="bullet">
///   <item>M2.a — secrets service;</item>
///   <item>M2.b — configuration model factory + view location expander;</item>
///   <item>M3 — group→role mapping service;</item>
///   <item>M4 — OIDC <c>IExternalAuthenticationRegistrar</c>;</item>
///   <item>M5 — global MVC filters (LoginActionsFilter, LocalLoginEnforcementFilter);</item>
///   <item>M6 — SAML protocol services + correlation cookie configuration.</item>
/// </list>
/// </remarks>
public class NopStartup : INopStartup
{
    /// <summary>
    /// Load order — run after the core NopCommerce services are registered.
    /// </summary>
    public int Order => 3000;

    /// <summary>
    /// Register services with the DI container.
    /// </summary>
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // M2.a
        services.AddScoped<ISsoSecretsService, SsoSecretsService>();

        // M2.b
        services.AddScoped<IConfigurationModelFactory, ConfigurationModelFactory>();

        // M4 — OIDC end-to-end services + the external-auth registrar that hooks the OIDC
        // handler into nop's auth pipeline. The registrar is discovered by nop via DI and
        // its Configure(AuthenticationBuilder) is invoked during startup.
        services.AddScoped<ISsoEventService, SsoEventService>();
        services.AddScoped<ISsoUserProvisioningService, SsoUserProvisioningService>();
        services.AddSingleton<IExternalAuthenticationRegistrar, OidcAuthenticationRegistrar>();

        // M5 — global MVC filters. The front-door filter (LoginActionsFilter) decides whether
        // a GET /Customer/Login should be auto-redirected to the SSO challenge; the back-door
        // filter (LocalLoginEnforcementFilter) enforces the role allowlist on the resulting
        // POST when the bypass cookie is set. M6.c adds LogoutActionsFilter that routes
        // /Customer/Logout through the federated SLO flow for SSO-signed-in customers.
        // All rely on constructor-injected services.
        services.AddScoped<LoginActionsFilter>();
        services.AddScoped<LocalLoginEnforcementFilter>();
        services.AddScoped<LogoutActionsFilter>();
        services.Configure<MvcOptions>(options =>
        {
            options.Filters.AddService<LoginActionsFilter>();
            options.Filters.AddService<LocalLoginEnforcementFilter>();
            options.Filters.AddService<LogoutActionsFilter>();
        });

        // M6.a — SAML 2.0 services. Built around ITfoxtec.Identity.Saml2; per-store
        // Saml2Configuration is rebuilt per request rather than cached at startup (the
        // key advantage of ITfoxtec over Sustainsys for multi-store deployments).
        services.AddScoped<ISaml2ConfigurationFactory, Saml2ConfigurationFactory>();
        services.AddScoped<ISamlChallengeBuilder, SamlChallengeBuilder>();
        services.AddScoped<ISamlResponseProcessor, SamlResponseProcessor>();

        // M6.b — SP metadata endpoint backing the /saml/metadata route on SamlController.
        services.AddScoped<ISamlMetadataBuilder, SamlMetadataBuilder>();

        // M6.c — SAML Single Log-Out (SP-initiated and IdP-initiated). The OIDC SLO path
        // is wired via SignedOutCallbackPath on OidcAuthenticationRegistrar; no plugin
        // service needed for that protocol.
        services.AddScoped<ISamlLogoutHandler, SamlLogoutHandler>();

        // M11 — per-IP rate limiter for the SSO login entry point. Backed by IStaticCacheManager.
        services.AddScoped<ISsoRateLimiter, SsoRateLimiter>();

        // M9 — IdP-onboarding-document generator. Pure CLR; no HTTP or external state.
        services.AddScoped<IOnboardingReportService, OnboardingReportService>();

        // M10 — audit-log admin grid backing services. Factory only; the read+clear surface
        // lives directly on ISsoEventService.
        services.AddScoped<ISsoAuthEventModelFactory, SsoAuthEventModelFactory>();

        // M12 — SSO Accounts list page. Summary service aggregates per-store telemetry;
        // test-connection service runs the lightweight reachability + cert check.
        services.AddScoped<ISsoStoreSummaryService, SsoStoreSummaryService>();
        services.AddScoped<ISsoTestConnectionService, SsoTestConnectionService>();
        services.AddScoped<ISsoStoreSummaryModelFactory, SsoStoreSummaryModelFactory>();

        // M3 — Group → role mapping admin + the runtime resolver hooked into
        // SsoUserProvisioningService.HandleAsync. The privileged-role guard is enforced
        // at the service layer regardless of mapping configuration.
        services.AddScoped<ISsoGroupRoleMappingService, SsoGroupRoleMappingService>();
        services.AddScoped<ISsoGroupRoleMappingModelFactory, SsoGroupRoleMappingModelFactory>();

        // M7 — IdP federation metadata import. Named HttpClient with a 10-second timeout
        // for URL fetches; the SSRF guard inside the importer rejects HTTP, loopback, and
        // private-IP addresses.
        services.AddScoped<IIdpMetadataImporter, IdpMetadataImporter>();
        services.AddHttpClient(IdpMetadataImporter.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/samlmetadata+xml"));
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/xml"));
            client.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/xml"));
        });

        // Make the plugin's Views/ folder discoverable so the admin controller can use
        // short view names (e.g. View("Configure", model)) without an absolute path.
        services.Configure<RazorViewEngineOptions>(options =>
        {
            options.ViewLocationExpanders.Add(new ViewLocationExpander());
        });
    }

    /// <summary>
    /// Configure the application pipeline. No middleware in M2.
    /// </summary>
    public void Configure(IApplicationBuilder application)
    {
    }
}
