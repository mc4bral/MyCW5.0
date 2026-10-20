using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>
/// View model for the Add-New picker reached from the SSO Accounts list.
/// Lists stores that don't yet have an enabled SSO configuration; the admin picks one
/// and lands on Configure scoped to that store.
/// </summary>
public record AddSsoAccountModel : BaseNopModel
{
    public AddSsoAccountModel()
    {
        AvailableStores = new List<SelectListItem>(); 
    }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.SsoAccounts.Add.StoreLabel")]
    public int StoreId { get; set; }

    public IList<SelectListItem> AvailableStores { get; set; } = new List<SelectListItem>();
}
