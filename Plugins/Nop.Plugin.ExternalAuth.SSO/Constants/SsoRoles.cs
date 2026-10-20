using Nop.Core.Domain.Customers;

namespace Nop.Plugin.ExternalAuth.SSO.Constants;

/// <summary>
/// Customer roles SSO treats specially.
/// </summary>
public static class SsoRoles
{
    /// <summary>
    /// Role system names SSO never grants (group mappings, default roles) and whose holders it never
    /// signs in (an existing admin account cannot be linked to an IdP identity).
    /// </summary>
    public static IReadOnlySet<string> Privileged { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { NopCustomerDefaults.AdministratorsRoleName };

    /// <summary>
    /// Whether the role with this system name is privileged.
    /// </summary>
    /// <param name="systemName">Role system name</param>
    public static bool IsPrivileged(string? systemName)
        => !string.IsNullOrEmpty(systemName) && Privileged.Contains(systemName);
}
