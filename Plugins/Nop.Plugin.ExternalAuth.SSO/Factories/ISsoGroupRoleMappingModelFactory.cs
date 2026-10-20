using Nop.Plugin.ExternalAuth.SSO.Domain;
using Nop.Plugin.ExternalAuth.SSO.Models;

namespace Nop.Plugin.ExternalAuth.SSO.Factories;

/// <summary>
/// Prepares view models for the Group → Role Mapping admin pages.
/// </summary>
public interface ISsoGroupRoleMappingModelFactory
{
    Task<SsoGroupRoleMappingSearchModel> PrepareSearchModelAsync(SsoGroupRoleMappingSearchModel searchModel);

    Task<SsoGroupRoleMappingListModel> PrepareListModelAsync(SsoGroupRoleMappingSearchModel searchModel);

    /// <summary>
    /// Prepare a Create-or-Update model. When <paramref name="entity"/> is non-null the
    /// form is populated from it; otherwise empty defaults are returned for Create.
    /// </summary>
    Task<SsoGroupRoleMappingModel> PrepareMappingModelAsync(
        SsoGroupRoleMappingModel? model,
        SsoGroupRoleMapping? entity);
}
