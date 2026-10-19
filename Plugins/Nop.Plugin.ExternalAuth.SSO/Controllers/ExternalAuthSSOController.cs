using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Domain;
using Nop.Plugin.ExternalAuth.SSO.Factories;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Security;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Services.Saml;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Common;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.ExternalAuth.SSO.Controllers;

/// <summary>
/// Admin-area controller for the ExternalAuth.SSO plugin's configuration.
/// </summary>
/// <remarks>
/// Patterned after nop's core <c>SettingController.Blog</c> action pair:
/// GET prepares a model via the factory; POST validates, persists, clears the settings
/// cache, and redirects back to GET so the per-store override flags reflect the new state.
/// </remarks>
[AutoValidateAntiforgeryToken]
public class ExternalAuthSSOController : BasePluginController
{
    #region Fields

    private readonly IConfigurationModelFactory _configurationModelFactory;
    private readonly ISsoAuthEventModelFactory _auditLogFactory;
    private readonly ISsoStoreSummaryModelFactory _storeSummaryFactory;
    private readonly ISsoGroupRoleMappingModelFactory _groupRoleMappingFactory;
    private readonly ISsoGroupRoleMappingService _groupRoleMappingService;
    private readonly IIdpMetadataImporter _idpMetadataImporter;
    private readonly ILocalizationService _localizationService;
    private readonly INotificationService _notificationService;
    private readonly IOnboardingReportService _onboardingReportService;
    private readonly ISsoEventService _eventService;
    private readonly ISsoSecretsService _secretsService;
    private readonly ISsoTestConnectionService _testConnectionService;
    private readonly IStoreContext _storeContext;
    private readonly IStoreService _storeService;
    #endregion

    #region Ctor

    public ExternalAuthSSOController(
        IConfigurationModelFactory modelFactory,
        ISsoAuthEventModelFactory auditLogFactory,
        ISsoStoreSummaryModelFactory storeSummaryFactory,
        ISsoGroupRoleMappingModelFactory groupRoleMappingFactory,
        ISsoGroupRoleMappingService groupRoleMappingService,
        IIdpMetadataImporter idpMetadataImporter,
        ILocalizationService localizationService,
        INotificationService notificationService,
        IOnboardingReportService onboardingReportService,
        ISsoEventService eventService,
        ISsoSecretsService secretsService,
        ISsoTestConnectionService testConnectionService,
        IStoreContext storeContext,
        IStoreService storeService)
    {
        _configurationModelFactory = modelFactory;
        _auditLogFactory = auditLogFactory;
        _storeSummaryFactory = storeSummaryFactory;
        _groupRoleMappingFactory = groupRoleMappingFactory;
        _groupRoleMappingService = groupRoleMappingService;
        _idpMetadataImporter = idpMetadataImporter;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _onboardingReportService = onboardingReportService;
        _eventService = eventService;
        _secretsService = secretsService;
        _testConnectionService = testConnectionService;
        _storeContext = storeContext;
        _storeService = storeService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Render the Configure form for the current store scope.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Scope rules:
    /// </para>
    /// <list type="bullet">
    ///   <item>URL carries <c>?storeid=N</c> (e.g. from the SSO Accounts grid's Edit
    ///   button) — that becomes the active scope.</item>
    ///   <item>URL has no <c>storeid</c> (a direct visit to <c>/Configure</c>) — scope
    ///   resets to 0 ("All stores"), system-wide editor.</item>
    /// </list>
    /// <para>
    /// We sync this onto the admin's <c>AdminAreaStoreScopeConfiguration</c> generic
    /// attribute because nop's <c>StoreScopeConfigurationViewComponent</c> reads from
    /// that attribute, not from the query string. Without this sync, the URL is ignored
    /// and the page renders with whatever scope the admin last selected via the dropdown.
    /// </para>
    /// </remarks>
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> Configure()
    {
        var model = await _configurationModelFactory.PrepareConfigurationModelAsync();
        return View("~/Plugins/ExternalAuth.SSO/Views/Configure.cshtml", model);
    }

    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> ConfigureStore(int Id = 0)
    {
        var admin = await _configurationModelFactory.GetCurrentCustomer();

        if (Id < 0)
            await _configurationModelFactory.ChangeSSOSope(0);
        if (Id > 0)
            await _configurationModelFactory.ChangeSSOSope(Id);

        return RedirectToAction(nameof(Configure));
    }

    /// <summary>
    /// Persist a posted Configure form for the current store scope.
    /// </summary>
    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> Configure(ConfigurationModel model)
    {
        if (!ModelState.IsValid)
        {
            // Re-prepare so per-store override flags + Has* indicators are current.
            var rerendered = await _configurationModelFactory.PrepareConfigurationModelAsync();
            // Carry posted values back into the rerendered model so the admin doesn't lose typing.
            rerendered = model with
            {
                ActiveStoreScopeConfiguration = rerendered.ActiveStoreScopeConfiguration,
                HasClientSecret = rerendered.HasClientSecret,
                HasIdpCertificate = rerendered.HasIdpCertificate,
                HasSpPfx = rerendered.HasSpPfx,
                HasSpPfxPassword = rerendered.HasSpPfxPassword,
                HasSsoBypassToken = rerendered.HasSsoBypassToken
            };
            return View("~/Plugins/ExternalAuth.SSO/Views/Configure.cshtml", rerendered);
        }

        await _configurationModelFactory.ApplyModelToSettingsAsync(model);

        // M8 — write a ConfigChanged audit row so admins can see who changed settings and
        // when. Notes carries the admin's email + the saved store scope.
        var admin = await _configurationModelFactory.GetCurrentCustomer();
        var savedScope = model.ActiveStoreScopeConfiguration;
        await _eventService.LogAsync(
            eventType: SsoAuthEventTypes.ConfigChanged,
            protocol: string.Empty,
            storeId: savedScope,
            customerId: admin?.Id,
            customerEmail: admin?.Email ?? string.Empty,
            idpSubject: string.Empty,
            notes: $"Configure saved by {admin?.Email ?? "<unknown admin>"} for store scope {savedScope}.");

        _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

        return RedirectToAction(nameof(Configure));
    }

    /// <summary>
    /// M7 — IdP metadata import endpoint. Accepts either a URL (fetched + parsed server-side
    /// with SSRF guard) or an uploaded XML file. Returns the parsed <c>IdpMetadataImportResult</c>
    /// as JSON for the Configure page's client-side script to consume.
    /// </summary>
    /// <remarks>
    /// Permission gate is the same as Configure — only admins with <c>AccessSsoSettings</c>
    /// can import metadata. The returned values are not persisted; the admin reviews the
    /// prefilled fields and clicks Save on the Configure form to commit them.
    /// </remarks>
    /// <summary>
    /// M9 — IdP-onboarding report. Returns a plain-text document the admin can paste into
    /// an email to the customer's IT team (SP values to paste into Entra plus labelled
    /// blanks for the values needs back).
    /// </summary>
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> OnboardingReport()
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var storeScope = await _configurationModelFactory.GetActiveStoreScopeSSOAsync();
        var scheme = Request.Scheme ?? "https";
        var host = Request.Host.Value ?? string.Empty;

        var report = await _onboardingReportService.BuildReportAsync(storeScope, scheme, host);
        var filename = $"sso-onboarding-store{(storeScope > 0 ? storeScope : store.Id)}.txt";
        return File(System.Text.Encoding.UTF8.GetBytes(report), "text/plain", filename);
    }

    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> ImportIdpMetadata(string? url, IFormFile? file)
    {
        try
        {
            IdpMetadataImportResult result;

            if (file is not null && file.Length > 0)
            {
                using var reader = new StreamReader(file.OpenReadStream());
                var xml = await reader.ReadToEndAsync();
                result = await _idpMetadataImporter.ImportFromXmlAsync(xml);
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
    /// M12 — SSO Accounts list. One row per nopCommerce store that has SSO enabled,
    /// ordered by DisplayOrder then StoreName.
    /// </summary>
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> SsoAccounts()
    {
        var model = await _storeSummaryFactory.PrepareSearchModelAsync(new SsoStoreSummarySearchModel());
        return View("~/Plugins/ExternalAuth.SSO/Views/SsoAccounts.cshtml", model);
    }

    /// <summary>
    /// M12 — DataTables JSON endpoint for the SSO Accounts grid.
    /// </summary>
    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> SsoAccountsList(SsoStoreSummarySearchModel searchModel)
    {
        var model = await _storeSummaryFactory.PrepareListModelAsync(searchModel);
        return Json(model);
    }

    /// <summary>
    /// M12 — Add-New picker. Renders a dropdown of stores that don't yet have SSO enabled.
    /// </summary>
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> AddSsoAccount()
    {
        var model = await _storeSummaryFactory.PrepareAddModelAsync();
        return View("~/Plugins/ExternalAuth.SSO/Views/AddSsoAccount.cshtml", model);
    }

    /// <summary>
    /// M12 — Add-New picker POST. Redirects to Configure scoped to the chosen store. The
    /// settings row is created by SettingService on first save; no explicit insert needed.
    /// </summary>
    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> AddSsoAccount(AddSsoAccountModel model)
    {
        if (model.StoreId <= 0)
        {
            _notificationService.WarningNotification(
                "Please pick a store from the dropdown.");
            return RedirectToAction(nameof(AddSsoAccount));
        }

        await _configurationModelFactory.ChangeSSOSope(model.StoreId);

        // Test-only auto-seed: when the picked store is literally named "Goodtest" we
        // pre-fill its SSO config with SAML 2.0 + the values from SAMLSSOConfig_StoreID_110.json
        // so the QA admin doesn't have to retype them. Hardcoded inline because the
        // Goodtest store is dev/test only and will not exist in production — remove this
        // block when the test tenant is decommissioned.
        var store = await _storeService.GetStoreByIdAsync(model.StoreId);
        if (store is not null
            && string.Equals(store.Name, "Goodtest", StringComparison.OrdinalIgnoreCase))
        {
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
                // The JSON's idpcert field is a placeholder string in this test file. Encrypted at rest.
                IdpCertificateBase64 = _secretsService.Protect("CERTIFICATEKEYHEREGTxD+JqsjvOsvEND"),
                IdpMetadataUrl = "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/federationmetadata/2007-06/federationmetadata.xml",

                // SAML — SP (from the JSON's ServiceProviderSettings block)
                SpEntityId = "https://goodtest.totalimagegroup.com.au",
                AcsUrl = "https://goodtest.totalimagegroup.com.au/Admin/saml/acs",
                SpLogoutUrl = "https://goodtest.totalimagegroup.com.au/Admin/saml/logout",
                // Placeholder SP signing PFX (Base64) + password. Encrypted at rest. SignedRequest
                // and EncryptedAssertion are both false so the values are not actually used by the
                // SAML flow — they exist only to satisfy the "required" form validation.
                SpPfxBase64 = _secretsService.Protect("PFXBASE64PLACEHOLDER=="),
                SpPfxPassword = _secretsService.Protect("pfxtestpassword"),

                SignedRequest = false,
                SignLogoutRequest = false,
                EncryptedAssertion = false,
                LoginBinding = "HTTP-Redirect",
                LogoutBinding = "HTTP-Redirect",

                EmailClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
                UsernameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
                FirstNameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname",
                LastNameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname",
                GroupsClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/groups",

                AutoRegister = true,
                DefaultRoleSystemName = "Guests",
                AllowedEmailDomains = "totalimagegroup.com.au",
                RefreshRolesOnLogin = true,
                RedirectLoginToSso = true,

                PostLoginRedirectUrl = "/",
                PostLogoutRedirectUrl = "/",

                CustomAttributesJson = "{}"
            };
            await _configurationModelFactory.SeedSettingsForStoreAsync(seed, model.StoreId);
        }

        // The store-scope dropdown on Configure is driven by ?storeid=<id>, which nop's
        // standard StoreScopeConfigurationViewComponent reads from query.
        return RedirectToAction(nameof(Configure), new { storeid = model.StoreId });
    }

    /// <summary>
    /// M12 — Test connection for a specific store. Returns the structured
    /// <see cref="TestConnectionResult"/> as JSON for the inline status on the grid.
    /// </summary>
    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> TestConnection(int storeId)
    {
        var result = await _testConnectionService.TestAsync(storeId);
        return Json(result);
    }

    /// <summary>
    /// Delete every SSO setting row for a specific store. Posted from the Delete button
    /// in the SSO Accounts grid's Actions column. Refuses <paramref name="storeId"/> &lt;= 0.
    /// Writes a <c>ConfigChanged</c> audit row noting the deletion. Returns a JSON envelope
    /// so the grid's AJAX caller can reload the table or surface an error banner.
    /// </summary>
    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> DeleteSsoAccount(int storeId)
    {
        if (storeId <= 0)
            return Json(new { success = false, error = "Invalid store id." });

        var deleted = await _configurationModelFactory.DeleteSettingsForStoreAsync(storeId);

        var admin = await _configurationModelFactory.GetCurrentCustomer();
        await _eventService.LogAsync(
            eventType: SsoAuthEventTypes.ConfigChanged,
            protocol: string.Empty,
            storeId: storeId,
            customerId: admin?.Id,
            customerEmail: admin?.Email ?? string.Empty,
            idpSubject: string.Empty,
            notes: $"SSO configuration deleted by {admin?.Email ?? "<unknown admin>"} for store {storeId} ({deleted} setting rows removed).");

        var template = await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.SsoAccounts.Deleted");
        var message = string.Format(template, deleted);

        _notificationService.SuccessNotification(message);
        return Json(new
        {
            success = true,
            message = message,
            deleted
        });
    }



    /// <summary>
    /// M3 — Group → Role Mapping list. Per-store filter, paged DataTables grid.
    /// </summary>
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> GroupRoleMappings()
    {
        var model = await _groupRoleMappingFactory.PrepareSearchModelAsync(new SsoGroupRoleMappingSearchModel());
        return View("~/Plugins/ExternalAuth.SSO/Views/GroupRoleMappings.cshtml", model);
    }

    /// <summary>M3 — DataTables JSON for the mappings grid.</summary>
    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> GroupRoleMappingsList(SsoGroupRoleMappingSearchModel searchModel)
    {
        var model = await _groupRoleMappingFactory.PrepareListModelAsync(searchModel);
        return Json(model);
    }

    /// <summary>M3 — Render the Create form.</summary>
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> GroupRoleMappingCreate()
    {
        var model = await _groupRoleMappingFactory.PrepareMappingModelAsync(null, null);
        return View("~/Plugins/ExternalAuth.SSO/Views/CreateOrUpdateGroupRoleMapping.cshtml", model);
    }

    /// <summary>M3 — Persist a new mapping.</summary>
    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> GroupRoleMappingCreate(SsoGroupRoleMappingModel model, bool continueEditing)
    {
        if (!ModelState.IsValid)
            return View("~/Plugins/ExternalAuth.SSO/Views/CreateOrUpdateGroupRoleMapping.cshtml",
                await _groupRoleMappingFactory.PrepareMappingModelAsync(model, null));

        // Uniqueness within (StoreId, IdpGroupId).
        var existing = await _groupRoleMappingService.GetByStoreAndIdpGroupAsync(model.StoreId, model.IdpGroupId);
        if (existing is not null)
        {
            ModelState.AddModelError(nameof(model.IdpGroupId),
                await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId.Duplicate"));
            return View("~/Plugins/ExternalAuth.SSO/Views/CreateOrUpdateGroupRoleMapping.cshtml",
                await _groupRoleMappingFactory.PrepareMappingModelAsync(model, null));
        }

        var entity = new SsoGroupRoleMapping
        {
            StoreId = model.StoreId,
            IdpGroupId = model.IdpGroupId.Trim(),
            IdpGroupDisplayName = model.IdpGroupDisplayName.Trim(),
            CustomerRoleId = model.CustomerRoleId,
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive
        };
        await _groupRoleMappingService.InsertAsync(entity);

        _notificationService.SuccessNotification(
            await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.GroupRoleMapping.Saved"));

        return continueEditing
            ? RedirectToAction(nameof(GroupRoleMappingEdit), new { id = entity.Id })
            : RedirectToAction(nameof(GroupRoleMappings));
    }

    /// <summary>M3 — Render the Edit form for an existing mapping.</summary>
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> GroupRoleMappingEdit(int id)
    {
        var entity = await _groupRoleMappingService.GetByIdAsync(id);
        if (entity is null) return RedirectToAction(nameof(GroupRoleMappings));

        var model = await _groupRoleMappingFactory.PrepareMappingModelAsync(null, entity);
        return View("~/Plugins/ExternalAuth.SSO/Views/CreateOrUpdateGroupRoleMapping.cshtml", model);
    }

    /// <summary>M3 — Persist edits to an existing mapping.</summary>
    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> GroupRoleMappingEdit(SsoGroupRoleMappingModel model, bool continueEditing)
    {
        var entity = await _groupRoleMappingService.GetByIdAsync(model.Id);
        if (entity is null) return RedirectToAction(nameof(GroupRoleMappings));

        if (!ModelState.IsValid)
            return View("~/Plugins/ExternalAuth.SSO/Views/CreateOrUpdateGroupRoleMapping.cshtml",
                await _groupRoleMappingFactory.PrepareMappingModelAsync(model, entity));

        // Uniqueness — only re-check when the natural key changed.
        if (entity.StoreId != model.StoreId
            || !string.Equals(entity.IdpGroupId, model.IdpGroupId, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _groupRoleMappingService.GetByStoreAndIdpGroupAsync(model.StoreId, model.IdpGroupId);
            if (existing is not null && existing.Id != entity.Id)
            {
                ModelState.AddModelError(nameof(model.IdpGroupId),
                    await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId.Duplicate"));
                return View("~/Plugins/ExternalAuth.SSO/Views/CreateOrUpdateGroupRoleMapping.cshtml",
                    await _groupRoleMappingFactory.PrepareMappingModelAsync(model, entity));
            }
        }

        entity.StoreId = model.StoreId;
        entity.IdpGroupId = model.IdpGroupId.Trim();
        entity.IdpGroupDisplayName = model.IdpGroupDisplayName.Trim();
        entity.CustomerRoleId = model.CustomerRoleId;
        entity.DisplayOrder = model.DisplayOrder;
        entity.IsActive = model.IsActive;
        await _groupRoleMappingService.UpdateAsync(entity);

        _notificationService.SuccessNotification(
            await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.GroupRoleMapping.Saved"));

        return continueEditing
            ? RedirectToAction(nameof(GroupRoleMappingEdit), new { id = entity.Id })
            : RedirectToAction(nameof(GroupRoleMappings));
    }

    /// <summary>M3 — Delete a mapping.</summary>
    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> GroupRoleMappingDelete(int id)
    {
        var entity = await _groupRoleMappingService.GetByIdAsync(id);
        if (entity is null) return RedirectToAction(nameof(GroupRoleMappings));

        await _groupRoleMappingService.DeleteAsync(entity);

        _notificationService.SuccessNotification(
            await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.GroupRoleMapping.Deleted"));

        return RedirectToAction(nameof(GroupRoleMappings));
    }

    /// <summary>
    /// M10 — Audit-log admin page. Renders the filter form and the empty grid host;
    /// rows are fetched lazily via <see cref="AuditLogList"/>.
    /// </summary>
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> AuditLog()
    {
        var model = await _auditLogFactory.PrepareSearchModelAsync(new SsoAuthEventSearchModel());
        return View("~/Plugins/ExternalAuth.SSO/Views/AuditLog.cshtml", model);
    }

    /// <summary>
    /// M10 — DataTables JSON endpoint backing the audit-log grid.
    /// </summary>
    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> AuditLogList(SsoAuthEventSearchModel searchModel)
    {
        var model = await _auditLogFactory.PrepareListModelAsync(searchModel);
        return Json(model);
    }

    /// <summary>
    /// M10 — Wipe the audit log. Writes a single <c>LogCleared</c> breadcrumb row first,
    /// then bulk-deletes the rest.
    /// </summary>
    [HttpPost]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public async Task<IActionResult> ClearAuditLog()
    {
        var deletedCount = await _eventService.ClearAllAsync();

        var template = await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.AuditLog.Cleared");
        _notificationService.SuccessNotification(string.Format(template, deletedCount));

        return RedirectToAction(nameof(AuditLog));
    }

    [HttpGet]
    [AuthorizeAdmin]
    [Area(AreaNames.ADMIN)]
    public virtual async Task<IActionResult> ChangeStoreScopeSSOConfiguration(int storeid, string returnUrl = "")
    {
        var store = await _storeService.GetStoreByIdAsync(storeid);

        if (store != null)
            await _configurationModelFactory.ChangeSSOSope(storeid);

        //home page
        if (string.IsNullOrEmpty(returnUrl))
            returnUrl = Url.Action("Index", "Home", new { area = AreaNames.ADMIN });

        //prevent open redirection attack
        if (!Url.IsLocalUrl(returnUrl))
            return RedirectToAction("Index", "Home", new { area = AreaNames.ADMIN });

        return Redirect(returnUrl);
    }

    #endregion

}


