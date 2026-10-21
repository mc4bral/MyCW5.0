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

namespace Nop.Plugin.ExternalAuth.SSO.Infrastructure;

/// <summary>
/// Registers the ExternalAuth.SSO plugin's services with the ASP.NET Core DI container.
/// </summary>
/// <remarks>
/// NopCommerce discovers this class through <see cref="INopStartup"/>. Registered elsewhere, not here:
/// the OIDC scheme and its per-store options cache (<c>OidcAuthenticationRegistrar</c>, which nop
/// creates with <c>Activator.CreateInstance</c> while building authentication), the admin menu
/// (<c>EventConsumer</c>), the new-customer names consumer and the audit-retention scheduled task
/// (nop finds both by type).
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
        // Shared: secrets at rest, audit log, per-IP rate limiter, certificate / metadata findings.
        services.AddScoped<ISsoSecretsService, SsoSecretsService>();
        services.AddScoped<ISsoEventService, SsoEventService>();
        services.AddScoped<ISsoRateLimiter, SsoRateLimiter>();
        services.AddScoped<ISsoMonitoringService, SsoMonitoringService>();

        // Sign-in: one provisioning path for both protocols, and the group/app-role → customer-role
        // resolver it calls (Administrators are refused there whatever the mappings say).
        services.AddScoped<ISsoUserProvisioningService, SsoUserProvisioningService>();
        services.AddScoped<ISsoGroupRoleMappingService, SsoGroupRoleMappingService>();

        // Global MVC filters on nop's Customer/Login and Customer/Logout: the SSO redirect and bypass
        // check on GET login, the role allowlist on a bypass POST, and the federated sign-out.
        services.AddScoped<LoginActionsFilter>();
        services.AddScoped<LocalLoginEnforcementFilter>();
        services.AddScoped<LogoutActionsFilter>();
        services.Configure<MvcOptions>(options =>
        {
            options.Filters.AddService<LoginActionsFilter>();
            options.Filters.AddService<LocalLoginEnforcementFilter>();
            options.Filters.AddService<LogoutActionsFilter>();
        });

        // SAML 2.0 (ITfoxtec). The Saml2Configuration is built per request from the store's
        // settings, so per-store SAML needs no options cache.
        services.AddScoped<ISaml2ConfigurationFactory, Saml2ConfigurationFactory>();
        services.AddScoped<ISamlChallengeBuilder, SamlChallengeBuilder>();
        services.AddScoped<ISamlResponseProcessor, SamlResponseProcessor>();
        services.AddScoped<ISamlMetadataBuilder, SamlMetadataBuilder>();
        services.AddScoped<ISamlLogoutHandler, SamlLogoutHandler>();

        // IdP metadata import (Configure page and Test connection). Outbound URLs pass the SafeUrl
        // guard; the named client adds a timeout and the metadata Accept types.
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

        // Admin pages: Configure (with the onboarding report), SSO Accounts (store summary and
        // Test connection), Group → Role Mappings, Audit Log.
        services.AddScoped<IConfigurationModelFactory, ConfigurationModelFactory>();
        services.AddScoped<IOnboardingReportService, OnboardingReportService>();
        services.AddScoped<ISsoStoreSummaryService, SsoStoreSummaryService>();
        services.AddScoped<ISsoTestConnectionService, SsoTestConnectionService>();
        services.AddScoped<ISsoStoreSummaryModelFactory, SsoStoreSummaryModelFactory>();
        services.AddScoped<ISsoGroupRoleMappingModelFactory, SsoGroupRoleMappingModelFactory>();
        services.AddScoped<ISsoAuthEventModelFactory, SsoAuthEventModelFactory>();

        // Make the plugin's Views/ folder discoverable so controllers can use short view names
        // (e.g. View("Configure", model)) without an absolute path.
        services.Configure<RazorViewEngineOptions>(options =>
        {
            options.ViewLocationExpanders.Add(new ViewLocationExpander());
        });
    }

    /// <summary>
    /// Configure the application pipeline. The plugin adds no middleware.
    /// </summary>
    public void Configure(IApplicationBuilder application)
    {
    }
}
