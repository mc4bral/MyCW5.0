using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>
/// Edit-form + grid row model for the Group → Role Mapping admin.
/// </summary>
public record SsoGroupRoleMappingModel : BaseNopEntityModel
{
    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.Store")]
    public int StoreId { get; set; }

    /// <summary>Display name used in the admin grid; populated by the factory.</summary>
    public string StoreName { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId")]
    public string IdpGroupId { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupDisplayName")]
    public string IdpGroupDisplayName { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.CustomerRole")]
    public int CustomerRoleId { get; set; }

    /// <summary>Customer-role name used in the admin grid; populated by the factory.</summary>
    public string CustomerRoleName { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.DisplayOrder")]
    public int DisplayOrder { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IsActive")]
    public bool IsActive { get; set; } = true;

    public IList<SelectListItem> AvailableStores { get; set; } = new List<SelectListItem>();
    public IList<SelectListItem> AvailableCustomerRoles { get; set; } = new List<SelectListItem>();
}
