using Nop.Core.Infrastructure;
using Nop.Services.Plugins;

namespace CW.Common.Helpers;

public class PluginHelper
{
    public static async Task<bool> IsPluginInstalledAsync(string pluginSystemName)
    {
        var pluginService = EngineContext.Current.Resolve<IPluginService>();

        if (pluginService == null)
            return false;

        var pluginDescriptorResult = await pluginService.GetPluginDescriptorBySystemNameAsync<IPlugin>(pluginSystemName, LoadPluginsMode.All);

        return pluginDescriptorResult != null && pluginDescriptorResult.Installed;
    }

    public static bool IsPluginInstalled(string pluginSystemName)
    {
        var pluginService = EngineContext.Current.Resolve<IPluginService>();

        if (pluginService == null)
            return false;

        var pluginDescriptorResult = pluginService.GetPluginDescriptorBySystemNameAsync<IPlugin>(pluginSystemName, LoadPluginsMode.All).Result;

        return pluginDescriptorResult != null && pluginDescriptorResult.Installed;
    }
}