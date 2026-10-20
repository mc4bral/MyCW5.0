using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>
/// Grid row model for the audit-log admin page.
/// </summary>
public record SsoAuthEventModel : BaseNopEntityModel
{
    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.AuditLog.Columns.OccurredOn")]
    public string OccurredOn { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.AuditLog.Columns.EventType")]
    public string EventType { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.AuditLog.Columns.Protocol")]
    public string Protocol { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.AuditLog.Columns.Store")]
    public string Store { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.AuditLog.Columns.Email")]
    public string CustomerEmail { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.AuditLog.Columns.IdpSubject")]
    public string IdpSubject { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.AuditLog.Columns.IpAddress")]
    public string IpAddress { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.AuditLog.Columns.Notes")]
    public string Notes { get; set; } = string.Empty;
}
