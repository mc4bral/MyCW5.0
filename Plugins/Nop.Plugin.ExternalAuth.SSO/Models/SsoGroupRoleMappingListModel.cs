using Nop.Web.Framework.Models;

namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>DataTables grid response for the Group → Role Mapping admin (M3).</summary>
public record SsoGroupRoleMappingListModel : BasePagedListModel<SsoGroupRoleMappingModel>;
