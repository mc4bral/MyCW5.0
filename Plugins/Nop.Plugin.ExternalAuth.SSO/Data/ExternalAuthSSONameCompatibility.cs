using Nop.Data.Mapping;
using Nop.Plugin.ExternalAuth.SSO.Domain;

namespace Nop.Plugin.ExternalAuth.SSO.Data;

/// <summary>
/// Maps this plugin's entity types to their physical table names, enforcing the
/// <c>SSO_{EntityName}</c> convention regardless of the entity class name.
/// </summary>
/// <remarks>
/// Auto-discovered by nopCommerce — no DI registration required. The <c>SSO_</c>
/// prefix follows AGENTS.md §6 (table-name convention <c>SSO_{EntityName}</c>)
/// and matches the schema produced by the legacy <c>Nop.Plugin.Auth.SSO</c> plugin so the
/// abbreviation is reusable across plugins addressing the same domain.
/// </remarks>
public class ExternalAuthSSONameCompatibility : INameCompatibility
{
    /// <inheritdoc />
    public Dictionary<Type, string> TableNames => new()
    {
        { typeof(SsoAuthEvent), "SSO_AuthEvent" },
        { typeof(SsoGroupRoleMapping), "SSO_GroupRoleMapping" },
    };

    /// <inheritdoc />
    public Dictionary<(Type, string), string> ColumnName => new();
}
