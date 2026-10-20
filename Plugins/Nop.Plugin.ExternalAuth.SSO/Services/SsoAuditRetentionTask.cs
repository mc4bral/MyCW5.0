using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Daily nop scheduled task ("SSO audit log retention") that deletes audit rows older than
/// <see cref="SsoSystemSettings.AuditRetentionDays"/>. 0 days = keep forever.
/// </summary>
/// <remarks>
/// Registered by the plugin's install and update (<c>SSOAuthenticationMethod</c>) and removed on
/// uninstall; nop finds it by its type name (<c>SSOAuthenticationDefaults.AuditRetentionTaskType</c>).
/// The run is visible under System → Schedule tasks; deletions are logged to nop's log.
/// </remarks>
public class SsoAuditRetentionTask : IScheduleTask
{
    #region Fields

    protected readonly ILogger _logger;
    protected readonly ISettingService _settingService;
    protected readonly ISsoEventService _eventService;

    #endregion

    #region Ctor

    public SsoAuditRetentionTask(
        ILogger logger,
        ISettingService settingService,
        ISsoEventService eventService)
    {
        _logger = logger;
        _settingService = settingService;
        _eventService = eventService;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task ExecuteAsync()
    {
        var settings = await _settingService.LoadSettingAsync<SsoSystemSettings>(storeId: 0);
        if (settings.AuditRetentionDays <= 0)
            return;

        var cutoffUtc = DateTime.UtcNow.AddDays(-settings.AuditRetentionDays);
        var deleted = await _eventService.PurgeOlderThanAsync(cutoffUtc);

        if (deleted > 0)
            await _logger.InformationAsync($"SSO audit log retention: deleted {deleted} row(s) older than {settings.AuditRetentionDays} days.");
    }

    #endregion
}
