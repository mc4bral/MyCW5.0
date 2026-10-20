using Nop.Plugin.ExternalAuth.SSO.Models;

namespace Nop.Plugin.ExternalAuth.SSO.Factories;

/// <summary>
/// Prepares view models for the SSO Accounts admin pages.
/// </summary>
public interface ISsoStoreSummaryModelFactory
{
    /// <summary>Prepare the search/filter model (page sizes).</summary>
    Task<SsoStoreSummarySearchModel> PrepareSearchModelAsync(SsoStoreSummarySearchModel searchModel);

    /// <summary>Build the paged DataTables grid response — rows ordered by DisplayOrder then StoreName.</summary>
    Task<SsoStoreSummaryListModel> PrepareListModelAsync(SsoStoreSummarySearchModel searchModel);

    /// <summary>Build the Add-New picker model (dropdown of stores not yet SSO-configured).</summary>
    Task<AddSsoAccountModel> PrepareAddModelAsync();
}
