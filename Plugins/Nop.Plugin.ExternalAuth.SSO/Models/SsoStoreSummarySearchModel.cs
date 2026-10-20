using Nop.Web.Framework.Models;

namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>
/// DataTables search arguments for the SSO Accounts list. No filter fields yet —
/// the grid is small (one row per SSO-enabled store) so pagination is enough.
/// </summary>
public record SsoStoreSummarySearchModel : BaseSearchModel;
