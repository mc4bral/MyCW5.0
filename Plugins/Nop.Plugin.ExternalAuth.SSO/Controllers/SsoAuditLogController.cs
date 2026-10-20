using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.ExternalAuth.SSO.Factories;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Security;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.ExternalAuth.SSO.Controllers;

/// <summary>
/// SSO audit log: grid and Clear.
/// </summary>
public class SsoAuditLogController : SsoAdminControllerBase
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly ISsoAuthEventModelFactory _auditLogFactory;
    protected readonly ISsoEventService _eventService;

    #endregion

    #region Ctor

    public SsoAuditLogController(
        ILocalizationService localizationService,
        INotificationService notificationService,
        ISsoAuthEventModelFactory auditLogFactory,
        ISsoEventService eventService)
    {
        _localizationService = localizationService;
        _notificationService = notificationService;
        _auditLogFactory = auditLogFactory;
        _eventService = eventService;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Audit-log page: filters and the grid host; rows come from <see cref="AuditLogList"/>.
    /// </summary>
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> AuditLog()
    {
        var model = await _auditLogFactory.PrepareSearchModelAsync(new SsoAuthEventSearchModel());
        return PluginView("AuditLog", model);
    }

    /// <summary>
    /// DataTables JSON for the audit-log grid.
    /// </summary>
    [HttpPost]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> AuditLogList(SsoAuthEventSearchModel searchModel)
        => Json(await _auditLogFactory.PrepareListModelAsync(searchModel));

    /// <summary>
    /// Delete every audit row, leaving a single <c>LogCleared</c> row that records who did it.
    /// </summary>
    [HttpPost]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> ClearAuditLog()
    {
        var deletedCount = await _eventService.ClearAllAsync();

        var template = await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.AuditLog.Cleared");
        _notificationService.SuccessNotification(string.Format(template, deletedCount));

        return RedirectToAction(nameof(AuditLog));
    }

    #endregion
}
