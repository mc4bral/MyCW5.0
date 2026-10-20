using Nop.Core.Domain.Customers;
using Nop.Plugin.ExternalAuth.SSO.Models;

namespace Nop.Plugin.ExternalAuth.SSO.Factories;

/// <summary>
/// Builds and persists the admin Configure-page view model.
/// </summary>
public interface IConfigurationModelFactory
{
    /// <summary>
    /// Build the model for the current admin store scope.
    /// Sensitive fields are zeroed and their <c>Has*</c> flags are set per the round-trip
    /// rules in <see cref="ConfigurationModel"/>.
    /// </summary>
    Task<ConfigurationModel> PrepareConfigurationModelAsync();

    /// <summary>
    /// Persist a posted model. Per-store fields are saved via
    /// <c>SaveSettingOverridablePerStoreAsync</c>; the system-wide bypass settings are
    /// saved at <c>storeId = 0</c>. Sensitive fields with empty values preserve the
    /// existing stored value; non-empty values replace.
    /// </summary>
    Task ApplyModelToSettingsAsync(ConfigurationModel model);

    /// <summary>
    /// Wipe every per-store <c>SsoConfigurationSettings</c> row for the given store.
    /// Does not touch system-wide <c>SsoSystemSettings</c>. Used by the Delete button
    /// in the SSO Accounts grid.
    /// </summary>
    /// <param name="storeId">Store id to wipe. Must be &gt; 0; values &lt;= 0 are no-ops.</param>
    /// <returns>The number of Setting rows deleted.</returns>
    Task<int> DeleteSettingsForStoreAsync(int storeId);
    /// <summary>
    /// Set the admin's SSO store scope (kept in the plugin's own customer attribute, separate from
    /// nop's settings scope). 0 = "All stores".
    /// </summary>
    /// <param name="storeId">Store id, or 0</param>
    Task SetStoreScopeAsync(int storeId);

    /// <summary>
    /// The signed-in admin, for audit rows.
    /// </summary>
    Task<Customer> GetCurrentCustomerAsync();

    /// <summary>
    /// The admin's SSO store scope; 0 = "All stores".
    /// </summary>
    Task<int> GetActiveStoreScopeSSOAsync();

    /// <summary>
    /// Save every property on <paramref name="settings"/> at the given store with
    /// <c>overrideForStore: true</c>. One-shot seeding for stores like the Goodstart
    /// test tenant on the Add-New picker — caller builds a fully-populated
    /// <see cref="Nop.Plugin.ExternalAuth.SSO.Settings.SsoConfigurationSettings"/>
    /// (with sensitive fields already encrypted) and this writes every field for that
    /// store.
    /// </summary>
    Task SeedSettingsForStoreAsync(Nop.Plugin.ExternalAuth.SSO.Settings.SsoConfigurationSettings settings, int storeId);
}
