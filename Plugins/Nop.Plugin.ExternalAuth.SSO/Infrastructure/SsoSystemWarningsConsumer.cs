using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Services.Events;
using Nop.Services.Plugins;
using Nop.Web.Areas.Admin.Models.Common;

namespace Nop.Plugin.ExternalAuth.SSO.Infrastructure;

/// <summary>
/// Adds the SSO findings (<see cref="ISsoMonitoringService"/>) to System → Warnings, and so to the
/// admin dashboard's "there are warnings" notice.
/// </summary>
/// <remarks>
/// nop publishes <see cref="SystemWarningCreatedEvent"/> each time it builds the warnings list, so
/// they are always current. Found by nop through <see cref="IConsumer{T}"/>; nothing to register.
/// Silent while the plugin is not installed.
/// </remarks>
public class SsoSystemWarningsConsumer : IConsumer<SystemWarningCreatedEvent>
{
    #region Fields

    protected readonly IPluginService _pluginService;
    protected readonly ISsoMonitoringService _monitoringService;

    #endregion

    #region Ctor

    public SsoSystemWarningsConsumer(IPluginService pluginService, ISsoMonitoringService monitoringService)
    {
        _pluginService = pluginService;
        _monitoringService = monitoringService;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task HandleEventAsync(SystemWarningCreatedEvent eventMessage)
    {
        var descriptor = await _pluginService.GetPluginDescriptorBySystemNameAsync<IPlugin>(
            Constants.SSOAuthenticationDefaults.SystemName, LoadPluginsMode.InstalledOnly);
        if (descriptor is null)
            return;

        foreach (var finding in await _monitoringService.GetFindingsAsync())
        {
            eventMessage.SystemWarnings.Add(new SystemWarningModel
            {
                Level = finding.IsFailure ? SystemWarningLevel.Fail : SystemWarningLevel.Warning,
                Text = finding.Text
            });
        }
    }

    #endregion
}
