using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>
/// Filter form + DataTables search arguments for the Group → Role Mapping admin.
/// </summary>
public record SsoGroupRoleMappingSearchModel : BaseSearchModel
{
    public SsoGroupRoleMappingSearchModel()
    {
        AvailableStores = new List<SelectListItem>();
    }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.GroupRoleMapping.Search.Store")]
    public int SearchStoreId { get; set; }

    public IList<SelectListItem> AvailableStores { get; set; } = new List<SelectListItem>();
}
