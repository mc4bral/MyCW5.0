using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Services.Helpers;
using Nop.Web.Framework.Models.Extensions;

namespace Nop.Plugin.ExternalAuth.SSO.Factories;

/// <summary>
/// Default implementation of <see cref="ISsoStoreSummaryModelFactory"/>.
/// </summary>
public class SsoStoreSummaryModelFactory : ISsoStoreSummaryModelFactory
{
    /// <summary>
    /// IdP certificate is highlighted as "near expiry" when its NotAfter is within this
    /// window. 30 days matches the legacy plan's P2-23 suggestion.
    /// </summary>
    private static readonly TimeSpan CertNearExpiryWindow = TimeSpan.FromDays(30);

    #region Fields

    private readonly IDateTimeHelper _dateTimeHelper;
    private readonly ISsoStoreSummaryService _summaryService;

    #endregion

    #region Ctor

    public SsoStoreSummaryModelFactory(
        IDateTimeHelper dateTimeHelper,
        ISsoStoreSummaryService summaryService)
    {
        _dateTimeHelper = dateTimeHelper;
        _summaryService = summaryService;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public Task<SsoStoreSummarySearchModel> PrepareSearchModelAsync(SsoStoreSummarySearchModel searchModel)
    {
        searchModel.SetGridPageSize();
        return Task.FromResult(searchModel);
    }

    /// <inheritdoc />
    public async Task<SsoStoreSummaryListModel> PrepareListModelAsync(SsoStoreSummarySearchModel searchModel)
    {
        var summaries = await _summaryService.ListAsync();

        var ordered = summaries
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.StoreName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var paged = new PagedList<SsoStoreSummary>(ordered, searchModel.Page - 1, searchModel.PageSize);

        return await new SsoStoreSummaryListModel().PrepareToGridAsync(searchModel, paged, () =>
            paged.SelectAwait(MapToRowAsync));
    }

    /// <inheritdoc />
    public async Task<AddSsoAccountModel> PrepareAddModelAsync()
    {
        var unconfigured = await _summaryService.ListUnconfiguredAsync();

        var model = new AddSsoAccountModel
        {
            AvailableStores = unconfigured
                .Select(s => new SelectListItem { Value = s.StoreId.ToString(), Text = s.StoreName })
                .ToList()
        };

        return model;
    }

    #endregion

    #region Utilities

    private async ValueTask<SsoStoreSummaryModel> MapToRowAsync(SsoStoreSummary s)
    {
        var dateSetup = await FormatLocalDateAsync(s.DateSetupUtc);
        var lastLogin = await FormatLocalDateAsync(s.LastSuccessfulLoginUtc);

        string certText;
        var certNearExpiry = false;
        var certExpired = false;
        if (!s.IdpCertificateNotAfter.HasValue)
        {
            certText = "—";
        }
        else
        {
            certText = s.IdpCertificateNotAfter.Value.ToString("yyyy-MM-dd");
            var remaining = s.IdpCertificateNotAfter.Value - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                certExpired = true;
            else if (remaining <= CertNearExpiryWindow)
                certNearExpiry = true;
        }

        return new SsoStoreSummaryModel
        {
            Id = s.StoreId,
            StoreId = s.StoreId,
            StoreName = s.StoreName,
            Protocol = s.Protocol.ToString(),
            IsEnabled = s.IsEnabled,
            DisplayOrder = s.DisplayOrder,
            DateSetup = dateSetup,
            NumberOfUsers = s.NumberOfUsers,
            LastSuccessfulLogin = lastLogin,
            CertificateNotAfter = certText,
            CertificateNearExpiry = certNearExpiry,
            CertificateExpired = certExpired
        };
    }

    private async ValueTask<string> FormatLocalDateAsync(DateTime? utc)
    {
        if (!utc.HasValue) return "—";
        var local = await _dateTimeHelper.ConvertToUserTimeAsync(utc.Value, DateTimeKind.Utc);
        return local.ToString("yyyy-MM-dd HH:mm");
    }

    #endregion
}
