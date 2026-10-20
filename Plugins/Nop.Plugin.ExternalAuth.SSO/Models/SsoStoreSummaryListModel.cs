using Nop.Web.Framework.Models;

namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>
/// DataTables grid response for the SSO Accounts admin list.
/// </summary>
public record SsoStoreSummaryListModel : BasePagedListModel<SsoStoreSummaryModel>;
