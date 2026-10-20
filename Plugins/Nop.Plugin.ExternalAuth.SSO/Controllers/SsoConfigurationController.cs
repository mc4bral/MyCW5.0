using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Factories;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Security;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Services.Saml;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.ExternalAuth.SSO.Controllers;

/// <summary>
/// Configure page: per-store and system-wide settings, the plugin's store scope, IdP metadata
/// import and the onboarding report.
/// </summary>
public class SsoConfigurationController : SsoAdminControllerBase
{
    #region Fields

    protected readonly IConfigurationModelFactory _configurationModelFactory;
    protected readonly IIdpMetadataImporter _idpMetadataImporter;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly IOnboardingReportService _onboardingReportService;
    protected readonly ISsoEventService _eventService;
    protected readonly IStoreContext _storeContext;
    protected readonly IStoreService _storeService;

    #endregion

    #region Ctor

    public SsoConfigurationController(
        IConfigurationModelFactory configurationModelFactory,
        IIdpMetadataImporter idpMetadataImporter,
        ILocalizationService localizationService,
        INotificationService notificationService,
        IOnboardingReportService onboardingReportService,
        ISsoEventService eventService,
        IStoreContext storeContext,
        IStoreService storeService)
    {
        _configurationModelFactory = configurationModelFactory;
        _idpMetadataImporter = idpMetadataImporter;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _onboardingReportService = onboardingReportService;
        _eventService = eventService;
        _storeContext = storeContext;
        _storeService = storeService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Render the Configure form for the admin's SSO store scope (0 = "All stores": the
    /// system-wide cards only).
    /// </summary>
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> Configure()
    {
        var model = await _configurationModelFactory.PrepareConfigurationModelAsync();
        return PluginView("Configure", model);
    }

    /// <summary>
    /// Open Configure for a store: <paramref name="id"/> &gt; 0 selects that store, &lt; 0 selects
    /// "All stores", 0 keeps the current scope. Used by the admin menu and the SSO Accounts grid.
    /// </summary>
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> ConfigureStore(int id = 0)
    {
        if (id != 0)
            await _configurationModelFactory.SetStoreScopeAsync(Math.Max(id, 0));

        return RedirectToAction(nameof(Configure));
    }

    /// <summary>
    /// Save the Configure form for the admin's SSO store scope and audit the change.
    /// </summary>
    [HttpPost]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
        {
            // Re-prepare for the scope and Has* indicators; keep what the admin typed.
            var prepared = await _configurationModelFactory.PrepareConfigurationModelAsync();
            var redisplay = model with
            {
                ActiveStoreScopeConfiguration = prepared.ActiveStoreScopeConfiguration,
                AvailableBindings = prepared.AvailableBindings,
                AvailableCustomerRoles = prepared.AvailableCustomerRoles,
                HasClientSecret = prepared.HasClientSecret,
                HasIdpCertificate = prepared.HasIdpCertificate,
                HasSpPfx = prepared.HasSpPfx,
                HasSpPfxPassword = prepared.HasSpPfxPassword,
                HasSsoBypassToken = prepared.HasSsoBypassToken
            };
            return PluginView("Configure", redisplay);
        }

        await _configurationModelFactory.ApplyModelToSettingsAsync(model);

        // The scope the factory saved, not a posted field — store 0 = "All stores".
        var admin = await _configurationModelFactory.GetCurrentCustomerAsync();
        var savedScope = await _configurationModelFactory.GetActiveStoreScopeSSOAsync();
        await _eventService.LogAsync(
            eventType: SsoAuthEventTypes.ConfigChanged,
            protocol: string.Empty,
            customerId: admin?.Id,
            customerEmail: admin?.Email,
            notes: $"Configure saved by {admin?.Email ?? "<unknown admin>"} for store scope {savedScope}.",
            storeId: savedScope);

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return RedirectToAction(nameof(Configure));
    }

    /// <summary>
    /// Change the admin's SSO store scope from the scope dropdown, then return to the page.
    /// </summary>
    /// <param name="storeid">Store id; 0 = "All stores"</param>
    /// <param name="returnUrl">Local page to return to</param>
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> ChangeStoreScopeSSOConfiguration(int storeid, string returnUrl = "")
    {
        if (storeid == 0 || await _storeService.GetStoreByIdAsync(storeid) is not null)
            await _configurationModelFactory.SetStoreScopeAsync(storeid);

        if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
            return RedirectToAction(nameof(Configure));

        return Redirect(returnUrl);
    }

    /// <summary>
    /// Read IdP metadata from an uploaded file or a URL and return the four IdP fields as JSON
    /// for the Configure page to fill in. Nothing is saved.
    /// </summary>
    [HttpPost]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> ImportIdpMetadata(string? url, IFormFile? file)
    {
        try
        {
            IdpMetadataImportResult result;

            if (file is not null && file.Length > 0)
            {
                using var reader = new StreamReader(file.OpenReadStream());
                result = await _idpMetadataImporter.ImportFromXmlAsync(await reader.ReadToEndAsync());
            }
            else if (!string.IsNullOrWhiteSpace(url))
            {
                result = await _idpMetadataImporter.ImportFromUrlAsync(url);
            }
            else
            {
                return Json(new
                {
                    success = false,
                    error = await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.IdpMetadata.Error.NoInput")
                });
            }

            return Json(new { success = true, result });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Download the IdP-onboarding document: the SP values to send to the customer's IT team and
    /// labelled blanks for what they send back.
    /// </summary>
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> OnboardingReport()
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var storeScope = await _configurationModelFactory.GetActiveStoreScopeSSOAsync();

        var report = await _onboardingReportService.BuildReportAsync(storeScope, Request.Scheme ?? "https", Request.Host.Value ?? string.Empty);
        var filename = $"sso-onboarding-store{(storeScope > 0 ? storeScope : store.Id)}.txt";
        return File(System.Text.Encoding.UTF8.GetBytes(report), "text/plain", filename);
    }

    #endregion
}
