using Nop.Core.Domain.Customers;
using Nop.Services.Security;

namespace Nop.Plugin.ExternalAuth.SSO.Security;

/// <summary>
/// Declares the plugin's permissions in the nopCommerce 5.00 permission model.
/// </summary>
/// <remarks>
/// Auto-discovered by <c>PermissionService.InsertPermissionsAsync</c> via
/// <c>ITypeFinder.FindClassesOfType&lt;IPermissionConfigManager&gt;</c>; no DI registration required.
/// Default-granted only to the <c>Administrators</c> role per SSOPlan.v2.md §3.10 (P1-20).
/// Plugin install/uninstall calls <c>InsertPermissionsAsync</c> / removes these system names
/// from the <c>PermissionRecord</c> table.
/// </remarks>
public partial class ExternalAuthSSOPermissionConfigManager : IPermissionConfigManager
{
    /// <summary>
    /// Category column on the admin "Access control list" screen.
    /// </summary>
    public const string Category = "ExternalAuth.SSO";

    /// <inheritdoc />
    public IList<PermissionConfig> AllConfigs => new List<PermissionConfig>
    {
        new (
            name: "Plugins. ExternalAuth.SSO. Access plugin features",
            systemName: ExternalAuthSSOPermissions.ACCESS_SSO_PLUGIN,
            category: Category,
            NopCustomerDefaults.AdministratorsRoleName),

        new (
            name: "Plugins. ExternalAuth.SSO. Configure",
            systemName: ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS,
            category: Category,
            NopCustomerDefaults.AdministratorsRoleName)
    };
}
