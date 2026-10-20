using FluentMigrator;
using Nop.Data.Mapping;
using Nop.Data.Migrations;
using Nop.Plugin.ExternalAuth.SSO.Domain;

namespace Nop.Plugin.ExternalAuth.SSO.Data.Migrations;

/// <summary>
/// Drops the foreign key from <c>SSO_AuthEvent.StoreId</c> to <c>Store.Id</c>.
/// </summary>
/// <remarks>
/// <para>
/// The plugin writes store <b>0</b> for system-wide audit rows (a Configure save at "All stores",
/// the <c>LogCleared</c> breadcrumb). Store 0 is not a row in <c>Store</c>, so the key rejected
/// those inserts — the save's audit row was lost and Clear audit log failed. An audit log should
/// also outlive a deleted store rather than block its deletion.
/// </para>
/// <para>
/// An <see cref="MigrationProcessType.Update"/> migration: nop runs it when the plugin's version
/// changes on an existing install. A fresh install creates the table from
/// <c>SsoAuthEventBuilder</c>, which no longer declares the key, and only records this migration
/// as applied. <see cref="Down"/> does nothing: restoring the key would fail on the store-0 rows
/// it exists to allow.
/// </para>
/// </remarks>
[NopMigration(MigrationVersions.DropAuditStoreForeignKey, "ExternalAuth.SSO drop SSO_AuthEvent store foreign key", MigrationProcessType.Update)]
public class DropAuditStoreForeignKeyMigration : Migration
{
    /// <summary>Name nop generated for the key in the initial schema.</summary>
    private const string ForeignKeyName = "FK_SSO_AuthEvent_StoreId_Store_Id";

    /// <inheritdoc />
    public override void Up()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(SsoAuthEvent));

        if (Schema.Table(table).Exists() && Schema.Table(table).Constraint(ForeignKeyName).Exists())
            Delete.ForeignKey(ForeignKeyName).OnTable(table);
    }

    /// <inheritdoc />
    public override void Down()
    {
    }
}
