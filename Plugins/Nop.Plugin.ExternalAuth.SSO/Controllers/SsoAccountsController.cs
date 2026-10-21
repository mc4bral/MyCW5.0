using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Factories;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Security;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Stores;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.ExternalAuth.SSO.Controllers;

/// <summary>
/// SSO Accounts: one row per store with SSO configured, adding a store, testing its connection
/// and removing its configuration.
/// </summary>
public class SsoAccountsController : SsoAdminControllerBase
{
    #region Fields

    protected readonly IConfigurationModelFactory _configurationModelFactory;
    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly ISsoEventService _eventService;
    protected readonly ISsoSecretsService _secretsService;
    protected readonly ISsoStoreSummaryModelFactory _storeSummaryFactory;
    protected readonly ISsoTestConnectionService _testConnectionService;
    protected readonly IStoreService _storeService;

    #endregion

    #region Ctor

    public SsoAccountsController(
        IConfigurationModelFactory configurationModelFactory,
        ILocalizationService localizationService,
        INotificationService notificationService,
        ISsoEventService eventService,
        ISsoSecretsService secretsService,
        ISsoStoreSummaryModelFactory storeSummaryFactory,
        ISsoTestConnectionService testConnectionService,
        IStoreService storeService)
    {
        _configurationModelFactory = configurationModelFactory;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _eventService = eventService;
        _secretsService = secretsService;
        _storeSummaryFactory = storeSummaryFactory;
        _testConnectionService = testConnectionService;
        _storeService = storeService;
    }

    #endregion

    #region Utilities

#if DEBUG
    /// <summary>
    /// Dev/test aid, compiled into DEBUG builds only: when the store picked on Add SSO account is
    /// named "Goodtest", pre-fill its SAML configuration so QA does not retype it. The tenant ID,
    /// certificate and PFX are placeholders (this repository is public) — put the real tenant ID in
    /// locally and never commit it. Delete this method when the test tenant is decommissioned.
    /// </summary>
    /// <param name="storeId">The store just picked</param>
    protected virtual async Task SeedTestStoreAsync(int storeId)
    {
        var store = await _storeService.GetStoreByIdAsync(storeId);
        if (store is null || !string.Equals(store.Name, "Goodtest", StringComparison.OrdinalIgnoreCase))
            return;

        var seed = new SsoConfigurationSettings
        {
            Protocol = SsoProtocol.Saml2,
            IsEnabled = true,
            BaseUrl = "https://Goodtest.totalimagegroup.com.au",
            IdpDisplayName = "Goodtest Entra",
            EnableMockMode = false,

            // SAML — IdP. The tenant ID is a placeholder (this repository is public); replace
            // 00000000-... with the test tenant's ID locally, never commit the real value.
            IdpEntityId = "https://sts.windows.net/00000000-0000-0000-0000-000000000000/",
            IdpSsoUrl = "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/saml2",
            IdpSloUrl = "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/saml2",
            // A placeholder string, encrypted at rest; it only satisfies validation.
            IdpCertificateBase64 = _secretsService.Protect("CERTIFICATEKEYHEREGTxD+JqsjvOsvEND"),
            IdpMetadataUrl = "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/federationmetadata/2007-06/federationmetadata.xml",

            // SAML — SP
            SpEntityId = "https://goodtest.totalimagegroup.com.au",
            AcsUrl = "https://goodtest.totalimagegroup.com.au/saml/acs",
            SpLogoutUrl = "https://goodtest.totalimagegroup.com.au/saml/logout",
            // Placeholder PFX + password, encrypted at rest. Signing and encryption are off, so the
            // SAML flow never uses them; they only satisfy validation.
            SpPfxBase64 = _secretsService.Protect("PFXBASE64PLACEHOLDER=="),
            SpPfxPassword = _secretsService.Protect("pfxtestpassword"),

            SignedRequest = false,
            SignLogoutRequest = false,
            EncryptedAssertion = false,
            LoginBinding = SamlBinding.HttpRedirect,
            LogoutBinding = SamlBinding.HttpRedirect,

            EmailClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
            UsernameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
            FirstNameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname",
            LastNameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname",
            GroupsClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/groups",

            AutoRegister = true,
            AllowedEmailDomains = "totalimagegroup.com.au",
            RefreshRolesOnLogin = true,
            RedirectLoginToSso = true,

            PostLoginRedirectUrl = "/",
            PostLogoutRedirectUrl = "/"
        };
        await _configurationModelFactory.SeedSettingsForStoreAsync(seed, storeId);
    }
#endif

    #endregion

    #region Methods

    /// <summary>
    /// SSO Accounts list.
    /// </summary>
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> SsoAccounts()
    {
        var model = await _storeSummaryFactory.PrepareSearchModelAsync(new SsoStoreSummarySearchModel());
        return PluginView("SsoAccounts", model);
    }

    /// <summary>
    /// DataTables JSON for the SSO Accounts grid.
    /// </summary>
    [HttpPost]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> SsoAccountsList(SsoStoreSummarySearchModel searchModel)
        => Json(await _storeSummaryFactory.PrepareListModelAsync(searchModel));

    /// <summary>
    /// Add SSO account: pick a store that has none yet.
    /// </summary>
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> AddSsoAccount()
    {
        var model = await _storeSummaryFactory.PrepareAddModelAsync();
        return PluginView("AddSsoAccount", model);
    }

    /// <summary>
    /// Add SSO account: open Configure scoped to the picked store. Settings rows are created on
    /// the first save.
    /// </summary>
    [HttpPost]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> AddSsoAccount(AddSsoAccountModel model)
    {
        // AddSsoAccountModelValidator: the store must be one the picker offers.
        if (!ModelState.IsValid)
        {
            var redisplay = await _storeSummaryFactory.PrepareAddModelAsync();
            redisplay.StoreId = model.StoreId;
            return PluginView("AddSsoAccount", redisplay);
        }

        await _configurationModelFactory.SetStoreScopeAsync(model.StoreId);

#if DEBUG
        await SeedTestStoreAsync(model.StoreId);
#endif

        return RedirectToAction(nameof(SsoConfigurationController.Configure), SsoAdminControllers.Configuration);
    }

    /// <summary>
    /// Test a store's IdP connection; JSON for the inline status on the grid and on Configure.
    /// </summary>
    [HttpPost]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> TestConnection(int storeId)
        => Json(await _testConnectionService.TestAsync(storeId));

    /// <summary>
    /// Delete a store's SSO configuration (its settings go back to the defaults) and audit it.
    /// JSON for the grid's AJAX caller.
    /// </summary>
    [HttpPost]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> DeleteSsoAccount(int storeId)
    {
        if (storeId <= 0)
            return Json(new { success = false, error = "Invalid store id." });

        var deleted = await _configurationModelFactory.DeleteSettingsForStoreAsync(storeId);

        var admin = await _configurationModelFactory.GetCurrentCustomerAsync();
        await _eventService.LogAsync(
            eventType: SsoAuthEventTypes.ConfigChanged,
            protocol: string.Empty,
            customerId: admin?.Id,
            customerEmail: admin?.Email,
            notes: $"SSO configuration deleted by {admin?.Email ?? "<unknown admin>"} for store {storeId} ({deleted} setting rows removed).",
            storeId: storeId);

        var message = string.Format(await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.SsoAccounts.Deleted"), deleted);
        _notificationService.SuccessNotification(message);

        return Json(new { success = true, message, deleted });
    }

    #endregion
}
