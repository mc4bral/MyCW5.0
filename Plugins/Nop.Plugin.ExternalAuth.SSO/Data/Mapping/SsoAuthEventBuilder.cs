using FluentMigrator.Builders.Create.Table;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.ExternalAuth.SSO.Domain;

namespace Nop.Plugin.ExternalAuth.SSO.Data.Mapping;

/// <summary>
/// FluentMigrator builder for the <see cref="SsoAuthEvent"/> table (<c>SSO_AuthEvent</c>).
/// <c>StoreId</c> has no foreign key: system-wide rows use store 0, and the audit log must outlive a
/// deleted store (see <c>DropAuditStoreForeignKeyMigration</c>).
/// </summary>
public class SsoAuthEventBuilder : NopEntityBuilder<SsoAuthEvent>
{
    #region Methods

    /// <summary>
    /// Apply entity configuration.
    /// </summary>
    /// <param name="table">Create table expression builder.</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(SsoAuthEvent.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(SsoAuthEvent.StoreId)).AsInt32().NotNullable()
            .WithColumn(nameof(SsoAuthEvent.CustomerId)).AsInt32().Nullable()
            .WithColumn(nameof(SsoAuthEvent.CustomerEmail)).AsString(200).NotNullable()
            .WithColumn(nameof(SsoAuthEvent.EventType)).AsString(50).NotNullable()
            .WithColumn(nameof(SsoAuthEvent.Protocol)).AsString(20).NotNullable()
            .WithColumn(nameof(SsoAuthEvent.IdpSubject)).AsString(500).NotNullable()
            .WithColumn(nameof(SsoAuthEvent.OccurredOnUtc)).AsDateTime().NotNullable()
            .WithColumn(nameof(SsoAuthEvent.IpAddress)).AsString(50).NotNullable()
            .WithColumn(nameof(SsoAuthEvent.Notes)).AsString(int.MaxValue).Nullable();
    }

    #endregion
}
