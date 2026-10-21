using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.ExternalAuth.SSO.Components;

/// <summary>
/// Represents a store scope configuration model
/// </summary>
public partial record StoreScopeConfigurationModel : BaseNopModel
{
    #region Ctor

    public StoreScopeConfigurationModel()
    {
        Stores = new List<StoreModel>();
    }

    #endregion

    #region Properties

    public int StoreId { get; set; }

    public IList<StoreModel> Stores { get; set; }

    #endregion

}
/// <summary>
/// Represents a store model
/// </summary>
public partial record StoreModel : BaseNopEntityModel
    {
        #region Ctor

        public StoreModel()
        {
        }

        #endregion

        #region Properties

        [NopResourceDisplayName("Admin.Configuration.Stores.Fields.Name")]
        public string Name { get; set; } = string.Empty;

        [NopResourceDisplayName("Admin.Configuration.Stores.Fields.Url")]
        public string Url { get; set; } = string.Empty;

        [NopResourceDisplayName("Admin.Configuration.Stores.Fields.SslEnabled")]
        public virtual bool SslEnabled { get; set; }

        [NopResourceDisplayName("Admin.Configuration.Stores.Fields.Hosts")]
        public string Hosts { get; set; } = string.Empty;

        [NopResourceDisplayName("Admin.Configuration.Stores.Fields.DisplayOrder")]
        public int DisplayOrder { get; set; }

        #endregion
    }

