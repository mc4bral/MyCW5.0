using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Stores;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Plugin.ExternalAuth.SSO.Factories;

/// <summary>
/// Default implementation of <see cref="ISsoAuthEventModelFactory"/>.
/// </summary>
public class SsoAuthEventModelFactory : ISsoAuthEventModelFactory
{
    #region Fields

    protected readonly IDateTimeHelper _dateTimeHelper;
    protected readonly ILocalizationService _localizationService;
    protected readonly ISsoEventService _eventService;
    protected readonly IStoreService _storeService;

    #endregion

    #region Ctor

    public SsoAuthEventModelFactory(
        IDateTimeHelper dateTimeHelper,
        ILocalizationService localizationService,
        ISsoEventService eventService,
        IStoreService storeService)
    {
        _dateTimeHelper = dateTimeHelper;
        _localizationService = localizationService;
        _eventService = eventService;
        _storeService = storeService;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<SsoAuthEventSearchModel> PrepareSearchModelAsync(SsoAuthEventSearchModel searchModel)
    {
        searchModel.AvailableStores = await BuildStoreOptionsAsync();
        searchModel.AvailableEventTypes = await BuildEventTypeOptionsAsync();
        searchModel.AvailableProtocols = await BuildProtocolOptionsAsync();

        searchModel.SetGridPageSize();
        return searchModel;
    }

    /// <inheritdoc />
    public async Task<SsoAuthEventListModel> PrepareListModelAsync(SsoAuthEventSearchModel searchModel)
    {
        var paged = await _eventService.SearchAsync(
            storeId: searchModel.StoreId,
            eventType: string.IsNullOrWhiteSpace(searchModel.EventType) ? null : searchModel.EventType,
            protocol: string.IsNullOrWhiteSpace(searchModel.Protocol) ? null : searchModel.Protocol,
            customerEmail: string.IsNullOrWhiteSpace(searchModel.CustomerEmail) ? null : searchModel.CustomerEmail,
            occurredFromUtc: searchModel.OccurredFrom,
            occurredToUtc: searchModel.OccurredTo,
            pageIndex: searchModel.Page - 1,
            pageSize: searchModel.PageSize);

        var storeNamesById = (await _storeService.GetAllStoresAsync())
            .ToDictionary(s => s.Id, s => s.Name);

        var model = await new SsoAuthEventListModel().PrepareToGridAsync(searchModel, paged, () =>
        {
            return paged.SelectAwait(async e =>
            {
                var localTime = await _dateTimeHelper.ConvertToUserTimeAsync(e.OccurredOnUtc, DateTimeKind.Utc);
                return new SsoAuthEventModel
                {
                    Id = e.Id,
                    OccurredOn = localTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    EventType = e.EventType,
                    Protocol = e.Protocol,
                    Store = storeNamesById.TryGetValue(e.StoreId, out var name) ? name : (e.StoreId == 0 ? "(All stores)" : $"#{e.StoreId}"),
                    CustomerEmail = e.CustomerEmail,
                    IdpSubject = e.IdpSubject,
                    IpAddress = e.IpAddress,
                    Notes = e.Notes ?? string.Empty
                };
            });
        });

        return model;
    }

    #endregion

    #region Utilities

    private async Task<IList<SelectListItem>> BuildStoreOptionsAsync()
    {
        var anyLabel = await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Filters.AllStores");
        var options = new List<SelectListItem>
        {
            new() { Value = "0", Text = anyLabel }
        };
        foreach (var store in await _storeService.GetAllStoresAsync())
            options.Add(new SelectListItem { Value = store.Id.ToString(), Text = store.Name });
        return options;
    }

    private async Task<IList<SelectListItem>> BuildEventTypeOptionsAsync()
    {
        var anyLabel = await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Filters.AllEventTypes");
        return new List<SelectListItem>
        {
            new() { Value = string.Empty, Text = anyLabel },
            new() { Value = SsoAuthEventTypes.Login, Text = "Login started" },
            new() { Value = SsoAuthEventTypes.AutoRegistered, Text = "Auto-registered" },
            new() { Value = SsoAuthEventTypes.Linked, Text = "Linked" },
            new() { Value = SsoAuthEventTypes.SignedIn, Text = "Signed in" },
            new() { Value = SsoAuthEventTypes.Slo, Text = "SLO" },
            new() { Value = SsoAuthEventTypes.Failed, Text = "Failed" },
            new() { Value = SsoAuthEventTypes.BypassDenied, Text = "Bypass denied" },
            new() { Value = SsoAuthEventTypes.ConfigChanged, Text = "Config changed" },
            new() { Value = SsoAuthEventTypes.LogCleared, Text = "Log cleared" }
        };
    }

    private async Task<IList<SelectListItem>> BuildProtocolOptionsAsync()
    {
        var anyLabel = await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Filters.AllProtocols");
        return new List<SelectListItem>
        {
            new() { Value = string.Empty, Text = anyLabel },
            new() { Value = SsoProtocolNames.Oidc, Text = "OpenID Connect" },
            new() { Value = SsoProtocolNames.Saml2, Text = "SAML 2.0" }
        };
    }

    #endregion
}
