using Nop.Core;
using Nop.Core.Domain.ScheduleTasks;
using Nop.Plugin.ExternalAuth.SSO.Components;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Infrastructure;
using Nop.Plugin.ExternalAuth.SSO.Security;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Authentication.External;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.ScheduleTasks;
using Nop.Services.Security;

namespace Nop.Plugin.ExternalAuth.SSO;

/// <summary>
/// Entry point for the ExternalAuth.SSO plugin. Authenticates nopCommerce customers
/// against Microsoft Entra ID via OpenID Connect (primary) or SAML 2.0 (secondary),
/// selectable per store.
/// </summary>
/// <remarks>
/// Structured after <see cref="Nop.Plugin.Integration.Odoo.OdooIntegrationPlugin"/> for the
/// install / uninstall / update lifecycle. The FluentMigrator schema migration
/// (<see cref="Data.Migrations.SchemaMigration"/>) is invoked by
/// <c>base.InstallAsync</c> / <c>base.UninstallAsync</c> — no explicit table create/drop
/// code lives in this class.
///
/// Implements <see cref="IExternalAuthenticationMethod"/> so nopCommerce recognises it as
/// an external authentication provider and surfaces it on the public login page via
/// <see cref="GetPublicViewComponent"/>.
///
/// Locale strings live in <see cref="SsoLocaleResources"/>. See
/// <c>References/SSO_Plugin_Reference.md</c> for the plugin as a whole.
/// </remarks>
public class SSOAuthenticationMethod : BasePlugin, IExternalAuthenticationMethod
{
    #region Fields

    protected readonly ICustomerService _customerService;
    protected readonly ILocalizationService _localizationService;
    protected readonly IPermissionService _permissionService;
    protected readonly IScheduleTaskService _scheduleTaskService;
    protected readonly ISettingService _settingService;
    protected readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public SSOAuthenticationMethod(
        ICustomerService customerService,
        ILocalizationService localizationService,
        IPermissionService permissionService,
        IScheduleTaskService scheduleTaskService,
        ISettingService settingService,
        IWebHelper webHelper)
    {
        _customerService = customerService;
        _localizationService = localizationService;
        _permissionService = permissionService;
        _scheduleTaskService = scheduleTaskService;
        _settingService = settingService;
        _webHelper = webHelper;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// The plugin's daily scheduled tasks: audit-log retention (<see cref="Services.SsoAuditRetentionTask"/>)
    /// and the certificate and metadata check (<see cref="Services.SsoDailyCheckTask"/>).
    /// </summary>
    private static readonly (string name, string type)[] _dailyTasks =
    [
        (SSOAuthenticationDefaults.AuditRetentionTaskName, SSOAuthenticationDefaults.AuditRetentionTaskType),
        (SSOAuthenticationDefaults.DailyCheckTaskName, SSOAuthenticationDefaults.DailyCheckTaskType)
    ];

    /// <summary>
    /// Register the plugin's daily tasks that are not there yet. Existing tasks keep their schedule
    /// and enabled state, as an admin may have changed them under System → Schedule tasks.
    /// </summary>
    private async Task EnsureScheduledTasksAsync()
    {
        foreach (var (name, type) in _dailyTasks)
        {
            if (await _scheduleTaskService.GetTaskByTypeAsync(type) is not null)
                continue;

            await _scheduleTaskService.InsertTaskAsync(new ScheduleTask
            {
                Name = name,
                Type = type,
                Seconds = 24 * 60 * 60,
                Enabled = true,
                LastEnabledUtc = DateTime.UtcNow,
                StopOnError = false
            });
        }
    }

    /// <summary>
    /// Rewrite setting rows stored before the settings were retyped (2026-10-19), for every store:
    /// <list type="bullet">
    ///   <item><c>LoginBinding</c> / <c>LogoutBinding</c>: <c>HTTP-Redirect</c> / <c>HTTP-POST</c> →
    ///         the <see cref="SamlBinding"/> name.</item>
    ///   <item><c>DefaultRoleSystemName</c> (a CSV of role ids from the Configure page, or one role
    ///         system name from the old default and the test seed) → <c>DefaultRoleIds</c>; the old
    ///         row is deleted. Unknown roles are dropped.</item>
    /// </list>
    /// Safe to run more than once: converted rows are left as they are. Until it runs, nop skips
    /// values it cannot convert and uses the defaults, so nothing fails in the meantime.
    /// </summary>
    private async Task ConvertRetypedSettingsAsync()
    {
        const string loginBindingKey = "ssoconfigurationsettings.loginbinding";
        const string logoutBindingKey = "ssoconfigurationsettings.logoutbinding";
        const string oldDefaultRoleKey = "ssoconfigurationsettings.defaultrolesystemname";
        const string newDefaultRoleKey = "ssoconfigurationsettings.defaultroleids";

        var settings = await _settingService.GetAllSettingsAsync();

        foreach (var row in settings.Where(s => s.Name.Equals(loginBindingKey, StringComparison.OrdinalIgnoreCase)
                                             || s.Name.Equals(logoutBindingKey, StringComparison.OrdinalIgnoreCase)))
        {
            var converted = SamlBindingExtensions.Parse(row.Value).ToString();
            if (!string.Equals(row.Value, converted, StringComparison.Ordinal))
                await _settingService.SetSettingAsync(row.Name, converted, row.StoreId, false);
        }

        foreach (var row in settings.Where(s => s.Name.Equals(oldDefaultRoleKey, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            var roleIds = new List<int>();
            foreach (var token in (row.Value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var role = int.TryParse(token, out var roleId)
                    ? await _customerService.GetCustomerRoleByIdAsync(roleId)
                    : await _customerService.GetCustomerRoleBySystemNameAsync(token);

                if (role is not null && !roleIds.Contains(role.Id))
                    roleIds.Add(role.Id);
            }

            await _settingService.SetSettingAsync(newDefaultRoleKey, string.Join(",", roleIds), row.StoreId, false);
            await _settingService.DeleteSettingAsync(row);
        }

        await _settingService.ClearCacheAsync();
    }

    /// <summary>
    /// Install or refresh the plugin's locale resources (<see cref="SsoLocaleResources"/>).
    /// </summary>
    private async Task AddOrUpdateLocaleResourcesAsync()
    {
        await _localizationService.AddOrUpdateLocaleResourceAsync(SsoLocaleResources.All);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Returns the URL of the plugin's configuration page in the admin area.
    /// </summary>
    public override string GetConfigurationPageUrl()
        => $"{_webHelper.GetStoreLocation()}Admin/SsoConfiguration/Configure";

    /// <summary>
    /// Public view component used by nopCommerce to render the "Sign in with Microsoft"
    /// button on the standard login page. Returns empty content when SSO is disabled or
    /// the protocol is set to <c>Off</c> for the current store (see
    /// <see cref="SSOAuthenticationViewComponent"/>).
    /// </summary>
    public Type GetPublicViewComponent()
        => typeof(SSOAuthenticationViewComponent);

    /// <summary>
    /// Install the plugin. Seeds default settings, registers permissions, adds locale
    /// resources, then defers to <c>base.InstallAsync</c> which runs the FluentMigrator
    /// schema migration.
    /// </summary>
    public override async Task InstallAsync()
    {
        // Default per-store config — disabled until an admin completes the Configure page.
        await _settingService.SaveSettingAsync(new SsoConfigurationSettings());

        // System-wide bypass settings — empty token disables the bypass entirely.
        await _settingService.SaveSettingAsync(new SsoSystemSettings());

        // Discover and insert any IPermissionConfigManager-declared permissions not yet in PermissionRecord.
        // This picks up ExternalAuthSSOPermissionConfigManager automatically.
        await _permissionService.InsertPermissionsAsync();

        await AddOrUpdateLocaleResourcesAsync();

        await EnsureScheduledTasksAsync();

        await base.InstallAsync();
    }

    /// <summary>
    /// Update the plugin. Refreshes locale resources when the target version includes a
    /// <c>DATA</c> marker (same convention as <c>OdooIntegrationPlugin</c>).
    /// </summary>
    /// <param name="currentVersion">Current installed version.</param>
    /// <param name="targetVersion">Target version being upgraded to.</param>
    public override async Task UpdateAsync(string currentVersion, string targetVersion)
    {
        if (targetVersion.Contains("DATA"))
            await AddOrUpdateLocaleResourcesAsync();

        // CustomAttributesJson was removed (no extension ever read it). Drop its strings and
        // its setting rows, which nop would otherwise keep as orphans.
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.ExternalAuth.SSO.Fields.CustomAttributesJson");
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.ExternalAuth.SSO.Cards.CustomAttributes");
        var orphanSettings = (await _settingService.GetAllSettingsAsync())
            .Where(s => s.Name.Equals("ssoconfigurationsettings.customattributesjson", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var orphan in orphanSettings)
            await _settingService.DeleteSettingAsync(orphan);

        // AccessSsoPlugin was declared but never checked; remove its record.
        await _permissionService.DeletePermissionAsync(ExternalAuthSSOPermissions.RETIRED_ACCESS_SSO_PLUGIN);

        // Settings retyped on 2026-10-19 (review item R8).
        await ConvertRetypedSettingsAsync();

        // Daily tasks: audit retention (2026-10-19), certificate and metadata check (2026-10-20).
        await EnsureScheduledTasksAsync();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.ExternalAuth.SSO.Fields.DefaultRoleSystemName");

        // Strings nothing displayed (removed from SsoLocaleResources, 2026-10-19).
        foreach (var deadKey in new[]
                 {
                     "Plugins.ExternalAuth.SSO.Permissions.AccessSsoPlugin",
                     "Plugins.ExternalAuth.SSO.Button.SignInWithMicrosoft",
                     "Plugins.ExternalAuth.SSO.Login.SamlNotYetAvailable",
                     "Plugins.ExternalAuth.SSO.Login.AdminAccountBlocked",
                     "Plugins.ExternalAuth.SSO.Login.DomainNotAllowed",
                     "Plugins.ExternalAuth.SSO.Saml.SignatureFailed",
                     "Plugins.ExternalAuth.SSO.Saml.AudienceMismatch",
                     "Plugins.ExternalAuth.SSO.Saml.InResponseToMismatch",
                     "Plugins.ExternalAuth.SSO.Cards.Saml",
                     "Plugins.ExternalAuth.SSO.Cards.Redirects",
                     "Plugins.ExternalAuth.SSO.Cards.System"
                 })
        {
            // DeleteLocaleResourcesAsync matches by prefix, so Cards.System also takes Cards.System.Hint.
            await _localizationService.DeleteLocaleResourcesAsync(deadKey);
        }

        await base.UpdateAsync(currentVersion, targetVersion);
    }

    /// <summary>
    /// Uninstall the plugin. Removes permissions, settings, locale resources, then defers
    /// to <c>base.UninstallAsync</c> which runs the reverse of the schema migration.
    /// </summary>
    public override async Task UninstallAsync()
    {
        await _permissionService.DeletePermissionAsync(ExternalAuthSSOPermissions.RETIRED_ACCESS_SSO_PLUGIN);
        await _permissionService.DeletePermissionAsync(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS);

        await _settingService.DeleteSettingAsync<SsoConfigurationSettings>();
        await _settingService.DeleteSettingAsync<SsoSystemSettings>();

        await _localizationService.DeleteLocaleResourcesAsync("Plugins.ExternalAuth.SSO");

        foreach (var (_, type) in _dailyTasks)
        {
            var task = await _scheduleTaskService.GetTaskByTypeAsync(type);
            if (task is not null)
                await _scheduleTaskService.DeleteTaskAsync(task);
        }

        await base.UninstallAsync();
    }

    #endregion
}
