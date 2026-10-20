using FluentMigrator;
using Nop.Data.Extensions;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.ExternalAuth.SSO.Domain;

namespace Nop.Plugin.ExternalAuth.SSO.Data.Migrations;

/// <summary>
/// Initial schema migration for the ExternalAuth.SSO plugin.
/// </summary>
/// <remarks>
/// Creates the audit log and group→role mapping tables and their supporting indexes
/// (the audit grid pages by store and date).
/// Reversal of all <c>Create.*</c> expressions is handled by
/// <see cref="AutoReversingMigration"/>; on plugin uninstall the indexes and tables
/// are dropped in reverse order.
/// </remarks>
[NopMigration(MigrationVersions.InitialSchema, "ExternalAuth.SSO base schema", MigrationProcessType.Installation)]
public class SchemaMigration : AutoReversingMigration
{
    /// <summary>
    /// Apply schema changes.
    /// </summary>
    public override void Up()
    {
        var authEventTable = NameCompatibilityManager.GetTableName(typeof(SsoAuthEvent));
        var mappingTable = NameCompatibilityManager.GetTableName(typeof(SsoGroupRoleMapping));

        this.CreateTableIfNotExists<SsoAuthEvent>();

        this.CreateTableIfNotExists<SsoGroupRoleMapping>();

        // Audit-log indexes — support the admin grid's date-range filter and per-store scoping.
        Create.Index("IX_SSO_AuthEvent_OccurredOnUtc")
            .OnTable(authEventTable)
            .OnColumn(nameof(SsoAuthEvent.OccurredOnUtc)).Ascending();

        Create.Index("IX_SSO_AuthEvent_StoreId_OccurredOnUtc")
            .OnTable(authEventTable)
            .OnColumn(nameof(SsoAuthEvent.StoreId)).Ascending()
            .OnColumn(nameof(SsoAuthEvent.OccurredOnUtc)).Ascending();

        // Group→role lookup index — every successful login resolves roles by (StoreId, IdpGroupId).
        Create.Index("IX_SSO_GroupRoleMapping_StoreId_IdpGroupId")
            .OnTable(mappingTable)
            .OnColumn(nameof(SsoGroupRoleMapping.StoreId)).Ascending()
            .OnColumn(nameof(SsoGroupRoleMapping.IdpGroupId)).Ascending();
    }
}
