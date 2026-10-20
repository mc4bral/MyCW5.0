using Nop.Plugin.ExternalAuth.SSO.Models;

namespace Nop.Plugin.ExternalAuth.SSO.Factories;

/// <summary>
/// Prepares view models for the audit-log admin page.
/// </summary>
public interface ISsoAuthEventModelFactory
{
    /// <summary>Prepare the filter form (populate dropdowns + page sizes).</summary>
    Task<SsoAuthEventSearchModel> PrepareSearchModelAsync(SsoAuthEventSearchModel searchModel);

    /// <summary>Prepare the paged DataTables grid response.</summary>
    Task<SsoAuthEventListModel> PrepareListModelAsync(SsoAuthEventSearchModel searchModel);
}
