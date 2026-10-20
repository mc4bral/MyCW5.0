using Nop.Core;

namespace Nop.Plugin.ExternalAuth.SSO.Domain;

/// <summary>
/// Per-store mapping from an Identity Provider group to a nopCommerce customer role.
/// </summary>
/// <remarks>
/// Persisted to <c>SSO_GroupRoleMapping</c> (see <see cref="Data.ExternalAuthSSONameCompatibility"/>).
/// Keyed by the IdP's stable group identifier (Entra Object ID GUID for Microsoft Entra) so the mapping
/// survives group display-name changes. The privileged-role guard
/// (<see cref="Services.ISsoGroupRoleMappingService"/> &mdash; not yet implemented) hard-blocks
/// <c>Administrators</c> from this mapping regardless of configuration.
/// See References/SSO_Plugin_Reference.md §3 and §5.5.
/// </remarks>
public class SsoGroupRoleMapping : BaseEntity
{
    /// <summary>
    /// Identifier of the store this mapping applies to.
    /// </summary>
    public int StoreId { get; set; }

    /// <summary>
    /// Stable group identifier as issued by the IdP. For Microsoft Entra this is the group Object ID (GUID).
    /// </summary>
    public string IdpGroupId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable group name shown in the admin grid. Not used for matching.
    /// </summary>
    public string IdpGroupDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Identifier of the nopCommerce customer role granted when a user is a member of <see cref="IdpGroupId"/>.
    /// </summary>
    public int CustomerRoleId { get; set; }

    /// <summary>
    /// Tie-breaker order when multiple groups map to overlapping roles.
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Allows the mapping to be temporarily disabled without deleting the row.
    /// </summary>
    public bool IsActive { get; set; }
}
