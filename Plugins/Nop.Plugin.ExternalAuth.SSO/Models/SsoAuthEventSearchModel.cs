using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>
/// Filter form + DataTables search arguments for the audit-log admin page.
/// </summary>
public record SsoAuthEventSearchModel : BaseSearchModel
{
    public SsoAuthEventSearchModel()
    {
        AvailableStores = new List<SelectListItem>();
        AvailableEventTypes = new List<SelectListItem>();
        AvailableProtocols = new List<SelectListItem>();
    }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.AuditLog.Search.Store")]
    public int StoreId { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.AuditLog.Search.EventType")]
    public string EventType { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.AuditLog.Search.Protocol")]
    public string Protocol { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.AuditLog.Search.Email")]
    public string CustomerEmail { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.AuditLog.Search.OccurredFrom")]
    [UIHint("DateNullable")]
    public DateTime? OccurredFrom { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.AuditLog.Search.OccurredTo")]
    [UIHint("DateNullable")]
    public DateTime? OccurredTo { get; set; }

    public IList<SelectListItem> AvailableStores { get; set; } = new List<SelectListItem>();
    public IList<SelectListItem> AvailableEventTypes { get; set; } = new List<SelectListItem>();
    public IList<SelectListItem> AvailableProtocols { get; set; } = new List<SelectListItem>();
}
