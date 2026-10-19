using Nop.Core;
using Nop.Data;
using Nop.Plugin.ExternalAuth.SSO.Domain;
using Nop.Services.Customers;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Default implementation of <see cref="ISsoGroupRoleMappingService"/>.
/// </summary>
public class SsoGroupRoleMappingService : ISsoGroupRoleMappingService
{
    /// <summary>
    /// Role system names that can never be granted via SSO. Hard-coded — these are not
    /// admin-configurable, by design. Matches the SSOPlan.v2.md §3.8 privileged-role guard.
    /// </summary>
    private static readonly HashSet<string> PrivilegedRoleSystemNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Administrators"
    };

    #region Fields

    private readonly ICustomerService _customerService;
    private readonly IRepository<SsoGroupRoleMapping> _repository;

    #endregion

    #region Ctor

    public SsoGroupRoleMappingService(
        ICustomerService customerService,
        IRepository<SsoGroupRoleMapping> repository)
    {
        _customerService = customerService;
        _repository = repository;
    }

    #endregion

    #region CRUD

    public async Task<SsoGroupRoleMapping?> GetByIdAsync(int id)
        => id <= 0 ? null : await _repository.GetByIdAsync(id);

    public async Task<SsoGroupRoleMapping?> GetByStoreAndIdpGroupAsync(int storeId, string idpGroupId)
    {
        if (string.IsNullOrEmpty(idpGroupId)) return null;

        var all = await _repository.GetAllAsync(q => q
            .Where(m => m.StoreId == storeId && m.IdpGroupId == idpGroupId));

        return all.FirstOrDefault();
    }

    public async Task<IPagedList<SsoGroupRoleMapping>> GetAllPagedAsync(
        int storeId = 0,
        int pageIndex = 0,
        int pageSize = int.MaxValue)
    {
        return await _repository.GetAllPagedAsync(query =>
        {
            if (storeId > 0)
                query = query.Where(m => m.StoreId == storeId);

            return query
                .OrderBy(m => m.StoreId)
                .ThenBy(m => m.DisplayOrder)
                .ThenBy(m => m.IdpGroupDisplayName);
        }, pageIndex, pageSize);
    }

    public Task InsertAsync(SsoGroupRoleMapping mapping) => _repository.InsertAsync(mapping);

    public Task UpdateAsync(SsoGroupRoleMapping mapping) => _repository.UpdateAsync(mapping);

    public Task DeleteAsync(SsoGroupRoleMapping mapping) => _repository.DeleteAsync(mapping);

    #endregion

    #region Runtime resolution

    /// <inheritdoc />
    public async Task<IList<int>> ResolveCustomerRoleIdsAsync(int storeId, IEnumerable<string> idpGroupIds)
    {
        var groupIds = (idpGroupIds ?? Array.Empty<string>())
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (groupIds.Count == 0)
            return Array.Empty<int>();

        var mappings = await _repository.GetAllAsync(q => q
            .Where(m => m.StoreId == storeId && m.IsActive && groupIds.Contains(m.IdpGroupId)));

        if (mappings.Count == 0)
            return Array.Empty<int>();

        var roleIds = mappings.Select(m => m.CustomerRoleId).Distinct().ToList();

        // Defence-in-depth: even if a privileged role somehow ended up in a mapping row
        // (data corruption, direct SQL edit, etc.), filter it out before returning.
        var safe = new List<int>(roleIds.Count);
        foreach (var roleId in roleIds)
        {
            var role = await _customerService.GetCustomerRoleByIdAsync(roleId);
            if (role is null) continue;
            if (PrivilegedRoleSystemNames.Contains(role.SystemName)) continue;
            safe.Add(role.Id);
        }

        return safe;
    }

    /// <inheritdoc />
    public async Task<IList<int>> GetOwnedRoleIdsForStoreAsync(int storeId)
    {
        var mappings = await _repository.GetAllAsync(q => q
            .Where(m => m.StoreId == storeId && m.IsActive));

        if (mappings.Count == 0)
            return Array.Empty<int>();

        return mappings.Select(m => m.CustomerRoleId).Distinct().ToList();
    }

    #endregion
}
