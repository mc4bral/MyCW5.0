using Microsoft.AspNetCore.Mvc.Razor;

namespace Nop.Plugin.ExternalAuth.SSO.Infrastructure;

/// <summary>
/// Adds the plugin's <c>Views</c> folder to the Razor view-location search list so admin
/// controllers can use <c>View("Configure", model)</c> without an absolute path.
/// </summary>
/// <remarks>
/// Registered in <see cref="NopStartup"/> via
/// <c>services.Configure&lt;RazorViewEngineOptions&gt;(o =&gt; o.ViewLocationExpanders.Add(...))</c>.
/// </remarks>
public class ViewLocationExpander : IViewLocationExpander
{
    private const string PLUGIN_FOLDER = "ExternalAuth.SSO";

    public void PopulateValues(ViewLocationExpanderContext context)
    {
    }

    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context,
        IEnumerable<string> viewLocations)
    {
        var pluginLocations = new[]
        {
            $"~/Plugins/{PLUGIN_FOLDER}/Views/{{1}}/{{0}}.cshtml",
            $"~/Plugins/{PLUGIN_FOLDER}/Views/{{0}}.cshtml"
        };

        return pluginLocations.Concat(viewLocations);
    }
}
