namespace Nop.Plugin.ExternalAuth.SSO.Security;

/// <summary>
/// Permission <c>SystemName</c> constants for the ExternalAuth.SSO plugin.
/// </summary>
/// <remarks>
/// Reference these constants from <c>[CheckPermission(...)]</c> attributes on admin actions —
/// e.g. <c>[CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]</c>.
/// The corresponding <see cref="ExternalAuthSSOPermissionConfigManager"/> seeds the
/// permission records into <c>PermissionRecord</c> with these system names.
/// </remarks>
public static class ExternalAuthSSOPermissions
{
    /// <summary>
    /// Required to use plugin-exposed features (audit log view, group→role mapping list, etc.).
    /// </summary>
    public const string ACCESS_SSO_PLUGIN = "AccessSsoPlugin";

    /// <summary>
    /// Required to view or change the plugin's configuration (the Configure page and write operations).
    /// </summary>
    public const string ACCESS_SSO_SETTINGS = "AccessSsoSettings";
}
