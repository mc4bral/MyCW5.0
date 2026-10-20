namespace Nop.Plugin.ExternalAuth.SSO.Constants;

/// <summary>
/// Route names of the plugin's admin controllers, for links and redirects that cross from one
/// controller to another (<c>asp-controller</c>, <c>Url.Action</c>, <c>RedirectToAction</c>).
/// </summary>
public static class SsoAdminControllers
{
    /// <summary>Configure page, store scope, metadata import, onboarding report.</summary>
    public const string Configuration = "SsoConfiguration";

    /// <summary>SSO Accounts list, add, test connection, delete.</summary>
    public const string Accounts = "SsoAccounts";

    /// <summary>Group → role mappings.</summary>
    public const string GroupRoleMappings = "SsoGroupRoleMappings";

    /// <summary>Audit log.</summary>
    public const string AuditLog = "SsoAuditLog";

    /// <summary>Folder of the plugin's views, for explicit view paths.</summary>
    public const string ViewsPath = "~/Plugins/ExternalAuth.SSO/Views/";
}
