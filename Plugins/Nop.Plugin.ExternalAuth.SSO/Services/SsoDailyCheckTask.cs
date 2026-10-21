using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Configuration;
using Nop.Services.Logging;
using Nop.Services.ScheduleTasks;
using Nop.Services.Stores;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Daily nop scheduled task ("SSO certificate and metadata check").
/// </summary>
/// <remarks>
/// <list type="number">
///   <item>For every SAML store with SSO enabled and a metadata URL: compare the stored IdP fields
///         with the published metadata and record the result; a store that opted in
///         (<see cref="SsoConfigurationSettings.AutoApplyIdpMetadata"/>) takes the IdP's values.</item>
///   <item>Write one <see cref="SsoAuthEventTypes.ScheduledCheck"/> audit row per finding — certificates
///         and client secrets that expire soon or have expired, metadata that still differs.</item>
/// </list>
/// The same findings appear under System → Warnings at any time; this task adds the metadata
/// comparison (which needs the network) and the audit trail. Registered by the plugin's install and
/// update, removed on uninstall; nop finds it by <see cref="SSOAuthenticationDefaults.DailyCheckTaskType"/>.
/// </remarks>
public class SsoDailyCheckTask : IScheduleTask
{
    #region Fields

    protected readonly ILogger _logger;
    protected readonly ISettingService _settingService;
    protected readonly ISsoEventService _eventService;
    protected readonly ISsoMonitoringService _monitoringService;
    protected readonly ISsoTestConnectionService _testConnectionService;
    protected readonly IStoreService _storeService;

    #endregion

    #region Ctor

    public SsoDailyCheckTask(
        ILogger logger,
        ISettingService settingService,
        ISsoEventService eventService,
        ISsoMonitoringService monitoringService,
        ISsoTestConnectionService testConnectionService,
        IStoreService storeService)
    {
        _logger = logger;
        _settingService = settingService;
        _eventService = eventService;
        _monitoringService = monitoringService;
        _testConnectionService = testConnectionService;
        _storeService = storeService;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task ExecuteAsync()
    {
        // 1. Metadata comparison (and opt-in apply) for every enabled SAML store with a metadata URL.
        foreach (var store in await _storeService.GetAllStoresAsync())
        {
            var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);
            if (!settings.IsEnabled || settings.Protocol != SsoProtocol.Saml2)
                continue;

            try
            {
                await _testConnectionService.CheckMetadataAsync(store.Id, allowAutoApply: true);
            }
            catch (Exception ex)
            {
                // One store's failure must not stop the others.
                await _logger.WarningAsync($"SSO daily check: metadata comparison failed for store {store.Id}.", ex);
            }
        }

        // 2. One audit row per finding (expiry or metadata difference).
        var findings = await _monitoringService.GetFindingsAsync();
        foreach (var finding in findings)
        {
            await _eventService.LogAsync(
                eventType: SsoAuthEventTypes.ScheduledCheck,
                protocol: string.Empty,
                storeId: finding.StoreId,
                notes: finding.Text);
        }

        if (findings.Count > 0)
            await _logger.InformationAsync($"SSO daily check: {findings.Count} finding(s); see System → Warnings and the SSO audit log.");
    }

    #endregion
}
