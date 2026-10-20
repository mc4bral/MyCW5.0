using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Web.Framework.Events;
using Nop.Web.Framework.Menu;

namespace Nop.Plugin.ExternalAuth.SSO.Infrastructure;

/// <summary>
/// Attaches the plugin's admin-area sidebar menu node when <c>AdminMenuCreatedEvent</c> fires.
/// </summary>
/// <remarks>
/// Inherits <see cref="BaseAdminMenuCreatedEventConsumer"/> — the nopCommerce 5.00 helper that
/// checks the plugin is installed before invoking <see cref="GetAdminMenuItemAsync"/>, so the
/// menu disappears automatically on uninstall. Auto-discovered via the
/// <c>IConsumer&lt;AdminMenuCreatedEvent&gt;</c> registration in <see cref="BaseAdminMenuCreatedEventConsumer"/>
/// — no entry in <see cref="NopStartup.ConfigureServices"/> required.
///
/// Patterned after <c>Nop.Plugin.Integration.Odoo.Infrastructure.EventConsumer</c>.
/// Child nodes: SSO Accounts, Configure, Group → Role Mappings and Audit Log.
/// </remarks>
public class EventConsumer : BaseAdminMenuCreatedEventConsumer
{
    /// <summary>
    /// Plugin system name — must match <c>plugin.json</c> and the
    /// <c>PluginDescriptor.SystemName</c> used by the loader.
    /// </summary>
    public static string SystemName => "ExternalAuth.SSO";

    #region Fields

    protected readonly ILocalizationService _localizationService;

    #endregion

    #region Ctor

    public EventConsumer(
        IPluginManager<IPlugin> pluginManager,
        ILocalizationService localizationService)
        : base(pluginManager)
    {
        _localizationService = localizationService;
    }

    #endregion

    #region Overrides

    /// <inheritdoc />
    protected override async Task<AdminMenuItem> GetAdminMenuItemAsync(IPlugin plugin)
    {
        var rootTitle = await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Menu.Root");
        var configureTitle = await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Menu.Configure");

        var auditLogTitle = await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Menu.AuditLog");
        var ssoAccountsTitle = await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Menu.SsoAccounts");
        var groupRoleMappingsTitle = await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Menu.GroupRoleMappings");

        var ssoAccountsMenu = new AdminMenuItem
        {
            SystemName = "ExternalAuth.SSO.SsoAccounts",
            Title = ssoAccountsTitle,
            Url = "/Admin/SsoAccounts/SsoAccounts",
            Visible = true,
            IconClass = "fas fa-list"
        };

        var configureMenu = new AdminMenuItem
        {
            SystemName = "ExternalAuth.SSO.Configure",
            Title = configureTitle,
            Url = "/Admin/SsoConfiguration/ConfigureStore/-1",
            Visible = true,
            IconClass = "far fa-circle-dot"
        };

        var groupRoleMappingsMenu = new AdminMenuItem
        {
            SystemName = "ExternalAuth.SSO.GroupRoleMappings",
            Title = groupRoleMappingsTitle,
            Url = "/Admin/SsoGroupRoleMappings/GroupRoleMappings",
            Visible = true,
            IconClass = "fas fa-users-cog"
        };

        var auditLogMenu = new AdminMenuItem
        {
            SystemName = "ExternalAuth.SSO.AuditLog",
            Title = auditLogTitle,
            Url = "/Admin/SsoAuditLog/AuditLog",
            Visible = true,
            IconClass = "far fa-clipboard"
        };

        var root = new AdminMenuItem
        {
            SystemName = plugin.PluginDescriptor.SystemName,
            Title = rootTitle,
            Visible = true,
            IconClass = "fab fa-microsoft"
        };

        // List first — most admins start there and dive into Configure via the Edit button.
        root.ChildNodes.Add(ssoAccountsMenu);
        root.ChildNodes.Add(configureMenu);
        root.ChildNodes.Add(groupRoleMappingsMenu);
        root.ChildNodes.Add(auditLogMenu);

        return root;
    }

    /// <inheritdoc />
    protected override string PluginSystemName => SystemName;

    /// <inheritdoc />
    protected override MenuItemInsertType InsertType => MenuItemInsertType.TryBeforeThanAfter;

    /// <summary>
    /// Anchor menu system name for placement. <see cref="MenuItemInsertType.TryBeforeThanAfter"/>
    /// tries to insert before <c>Configuration</c>; if no such node exists it falls back to
    /// inserting after, then to the end of the list.
    /// </summary>
    protected override string BeforeMenuSystemName => "Configuration";

    #endregion
}
