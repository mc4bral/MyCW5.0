using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Factories;
using Nop.Services.Common;
using Nop.Services.Stores;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.ExternalAuth.SSO.Components;

/// <summary>
/// Represents a view component that displays the store scope configuration
/// </summary>
public partial class StoreScopeSSOViewComponent : NopViewComponent
{
    #region Fields

    private readonly IConfigurationModelFactory _configurationModelFactory;

    protected readonly IStoreService _storeService;

    #endregion

    #region Ctor

    public StoreScopeSSOViewComponent(IStoreService storeService, IConfigurationModelFactory configurationModelFactory)
    {
        _storeService = storeService;
        _configurationModelFactory = configurationModelFactory;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Invoke view component
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the view component result
    /// </returns>
    public async Task<IViewComponentResult> InvokeAsync()
    {
        //prepare model
        var model = await PrepareStoreScopeConfigurationModelAsync();

        if (model.Stores.Count < 2)
            return Content(string.Empty);

        return View(model);
    }

    /// <summary>
    /// Prepare store scope configuration model
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation
    /// The task result contains the store scope configuration model
    /// </returns>
    public virtual async Task<StoreScopeConfigurationModel> PrepareStoreScopeConfigurationModelAsync()
    {
        var model = new StoreScopeConfigurationModel
        {
            Stores = (await _storeService.GetAllStoresAsync()).Select(store => new StoreModel
            {
                Id = store.Id,
                Name = store.Name,
                Url = store.Url,
                SslEnabled = store.SslEnabled,
                Hosts = store.Hosts,
                DisplayOrder = store.DisplayOrder
            }).ToList(),
            StoreId = await _configurationModelFactory.GetActiveStoreScopeSSOAsync()
        };

        return model;
    }




    #endregion
}