namespace Nop.Plugin.ExternalAuth.SSO.Data;

/// <summary>
/// Centralised timestamp constants for FluentMigrator migrations in this plugin.
/// </summary>
/// <remarks>
/// Format <c>yyyy/MM/dd HH:mm:ss</c> per the convention used across the solution
/// (see e.g. <c>Nop.Plugin.CW.Freight.Data.MigrationVersions</c>). New migrations
/// append a new <c>const string</c> here and reference it from their
/// <c>[NopMigration]</c> attribute.
/// </remarks>
public static class MigrationVersions
{
    /// <summary>
    /// Initial schema for the ExternalAuth.SSO plugin
    /// (<c>SSO_AuthEvent</c> + <c>SSO_GroupRoleMapping</c>).
    /// </summary>
    public const string InitialSchema = "2026/06/01 00:00:00";

    /// <summary>
    /// Drop the <c>SSO_AuthEvent.StoreId</c> → <c>Store.Id</c> foreign key: system-wide audit rows
    /// use store 0, which is not a store, and an audit log must outlive a deleted store.
    /// </summary>
    public const string DropAuditStoreForeignKey = "2026/10/19 12:00:00";
}
