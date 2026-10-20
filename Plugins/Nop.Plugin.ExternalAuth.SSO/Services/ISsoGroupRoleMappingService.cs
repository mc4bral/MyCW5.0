using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Domain;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// CRUD + role-resolution surface over <see cref="SsoGroupRoleMapping"/>.
/// </summary>
/// <remarks>
/// The runtime hook is <see cref="ResolveCustomerRoleIdsAsync"/>: called from the
/// provisioning pipeline on every successful SSO login, it translates the IdP-asserted
/// group identifiers (Entra Object IDs) into the set of <c>CustomerRole.Id</c> values
/// to grant. The privileged-role guard runs unconditionally here as defence-in-depth —
/// even if data is corrupt or admins somehow bypass the validator, <c>Administrators</c>
/// can never be granted via SSO.
/// </remarks>
public interface ISsoGroupRoleMappingService
{
    #region CRUD

    Task<SsoGroupRoleMapping?> GetByIdAsync(int id);

    Task<SsoGroupRoleMapping?> GetByStoreAndIdpGroupAsync(int storeId, string idpGroupId);

    Task<IPagedList<SsoGroupRoleMapping>> GetAllPagedAsync(
        int storeId = 0,
        int pageIndex = 0,
        int pageSize = int.MaxValue);

    Task InsertAsync(SsoGroupRoleMapping mapping);

    Task UpdateAsync(SsoGroupRoleMapping mapping);

    Task DeleteAsync(SsoGroupRoleMapping mapping);

    #endregion

    #region Runtime resolution

    /// <summary>
    /// Translate IdP-asserted group identifiers into a set of nopCommerce role IDs to
    /// grant the signing-in customer. Filters out <c>Administrators</c> 
    /// at the data layer regardless of mapping configuration.
    /// </summary>
    Task<IList<int>> ResolveCustomerRoleIdsAsync(int storeId, IEnumerable<string> idpGroupIds);

    /// <summary>
    /// The distinct <c>CustomerRole.Id</c> values that appear in any active mapping for
    /// the store. Combined with the configured <c>DefaultRoleIds</c> this is the
    /// plugin's "owned set" — the union of roles whose membership the plugin may add
    /// to or remove from on each login. Roles outside this set are never touched.
    /// </summary>
    Task<IList<int>> GetOwnedRoleIdsForStoreAsync(int storeId);

    #endregion
}
