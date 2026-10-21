using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Domain;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Stores;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Plugin.ExternalAuth.SSO.Factories;

/// <summary>
/// Default implementation of <see cref="ISsoGroupRoleMappingModelFactory"/>.
/// </summary>
public class SsoGroupRoleMappingModelFactory : ISsoGroupRoleMappingModelFactory
{
    #region Fields

    protected readonly ICustomerService _customerService;
    protected readonly ILocalizationService _localizationService;
    protected readonly ISsoGroupRoleMappingService _mappingService;
    protected readonly IStoreService _storeService;

    #endregion

    #region Ctor

    public SsoGroupRoleMappingModelFactory(
        ICustomerService customerService,
        ILocalizationService localizationService,
        ISsoGroupRoleMappingService mappingService,
        IStoreService storeService)
    {
        _customerService = customerService;
        _localizationService = localizationService;
        _mappingService = mappingService;
        _storeService = storeService;
    }

    #endregion

    #region Utilities

    private async Task<IList<SelectListItem>> BuildStoreOptionsAsync(bool includeAny)
    {
        var options = new List<SelectListItem>();
        if (includeAny)
        {
            var anyLabel = await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Filters.AllStores");
            options.Add(new SelectListItem { Value = "0", Text = anyLabel });
        }

        foreach (var store in await _storeService.GetAllStoresAsync())
            options.Add(new SelectListItem { Value = store.Id.ToString(), Text = store.Name });

        return options;
    }

    private async Task<IList<SelectListItem>> BuildCustomerRoleOptionsAsync()
    {
        var allRoles = await _customerService.GetAllCustomerRolesAsync(showHidden: true);
        return allRoles
            .Where(r => !SsoRoles.IsPrivileged(r.SystemName))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Name })
            .ToList();
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<SsoGroupRoleMappingSearchModel> PrepareSearchModelAsync(SsoGroupRoleMappingSearchModel searchModel)
    {
        searchModel.AvailableStores = await BuildStoreOptionsAsync(includeAny: true);
        searchModel.SetGridPageSize();
        return searchModel;
    }

    /// <inheritdoc />
    public async Task<SsoGroupRoleMappingListModel> PrepareListModelAsync(SsoGroupRoleMappingSearchModel searchModel)
    {
        var paged = await _mappingService.GetAllPagedAsync(
            storeId: searchModel.SearchStoreId,
            pageIndex: searchModel.Page - 1,
            pageSize: searchModel.PageSize);

        // Resolve store and role names once per page so we don't N+1 per row.
        var stores = (await _storeService.GetAllStoresAsync()).ToDictionary(s => s.Id, s => s.Name);
        var roleIds = paged.Select(m => m.CustomerRoleId).Distinct().ToList();
        var roleNames = new Dictionary<int, string>();
        foreach (var id in roleIds)
        {
            var r = await _customerService.GetCustomerRoleByIdAsync(id);
            roleNames[id] = r?.Name ?? $"#{id}";
        }

        // Label used for mappings whose StoreId is 0 ("applies to every store").
        var allStoresLabel = await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Filters.AllStores");

        return await new SsoGroupRoleMappingListModel().PrepareToGridAsync(searchModel, paged, () =>
            paged.SelectAwait(entity => ValueTask.FromResult(new SsoGroupRoleMappingModel
            {
                Id = entity.Id,
                StoreId = entity.StoreId,
                StoreName = stores.TryGetValue(entity.StoreId, out var sn) ? sn : (entity.StoreId == 0 ? allStoresLabel : $"#{entity.StoreId}"),
                IdpGroupId = entity.IdpGroupId,
                IdpGroupDisplayName = entity.IdpGroupDisplayName,
                CustomerRoleId = entity.CustomerRoleId,
                CustomerRoleName = roleNames.TryGetValue(entity.CustomerRoleId, out var rn) ? rn : $"#{entity.CustomerRoleId}",
                DisplayOrder = entity.DisplayOrder,
                IsActive = entity.IsActive
            })));
    }

    /// <inheritdoc />
    public async Task<SsoGroupRoleMappingModel> PrepareMappingModelAsync(
        SsoGroupRoleMappingModel? model,
        SsoGroupRoleMapping? entity)
    {
        model ??= new SsoGroupRoleMappingModel();

        if (entity is not null)
        {
            model.Id = entity.Id;
            model.StoreId = entity.StoreId;
            model.IdpGroupId = entity.IdpGroupId;
            model.IdpGroupDisplayName = entity.IdpGroupDisplayName;
            model.CustomerRoleId = entity.CustomerRoleId;
            model.DisplayOrder = entity.DisplayOrder;
            model.IsActive = entity.IsActive;
        }

        model.AvailableStores = await BuildStoreOptionsAsync(includeAny: false);
        model.AvailableCustomerRoles = await BuildCustomerRoleOptionsAsync();

        return model;
    }

    #endregion
}
