using Nop.Web.Framework.Models;

namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>
/// DataTables grid response model for the audit-log page.
/// </summary>
public record SsoAuthEventListModel : BasePagedListModel<SsoAuthEventModel>;
