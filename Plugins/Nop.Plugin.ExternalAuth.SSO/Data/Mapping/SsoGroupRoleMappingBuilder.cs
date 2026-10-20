using FluentMigrator.Builders.Create.Table;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Stores;
using Nop.Data.Extensions;
using Nop.Data.Mapping.Builders;
using Nop.Plugin.ExternalAuth.SSO.Domain;

namespace Nop.Plugin.ExternalAuth.SSO.Data.Mapping;

/// <summary>
/// FluentMigrator builder for the <see cref="SsoGroupRoleMapping"/> table (<c>SSO_GroupRoleMapping</c>).
/// Columns: References/SSO_Plugin_Reference.md §3.
/// </summary>
public class SsoGroupRoleMappingBuilder : NopEntityBuilder<SsoGroupRoleMapping>
{
    #region Methods

    /// <summary>
    /// Apply entity configuration.
    /// </summary>
    /// <param name="table">Create table expression builder.</param>
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table.WithColumn(nameof(SsoGroupRoleMapping.Id)).AsInt32().PrimaryKey().Identity()
            .WithColumn(nameof(SsoGroupRoleMapping.StoreId)).AsInt32().ForeignKey<Store>().NotNullable()
            .WithColumn(nameof(SsoGroupRoleMapping.IdpGroupId)).AsString(100).NotNullable()
            .WithColumn(nameof(SsoGroupRoleMapping.IdpGroupDisplayName)).AsString(200).NotNullable()
            .WithColumn(nameof(SsoGroupRoleMapping.CustomerRoleId)).AsInt32().ForeignKey<CustomerRole>().NotNullable()
            .WithColumn(nameof(SsoGroupRoleMapping.DisplayOrder)).AsInt32().NotNullable()
            .WithColumn(nameof(SsoGroupRoleMapping.IsActive)).AsBoolean().WithDefaultValue(true).NotNullable();
    }

    #endregion
}
