using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.ExternalAuth.SSO.Domain;
using Nop.Plugin.ExternalAuth.SSO.Factories;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Security;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.ExternalAuth.SSO.Controllers;

/// <summary>
/// Group → role mappings: list, create, edit, delete.
/// </summary>
public class SsoGroupRoleMappingsController : SsoAdminControllerBase
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly INotificationService _notificationService;
    protected readonly ISsoGroupRoleMappingModelFactory _groupRoleMappingFactory;
    protected readonly ISsoGroupRoleMappingService _groupRoleMappingService;

    #endregion

    #region Ctor

    public SsoGroupRoleMappingsController(
        ILocalizationService localizationService,
        INotificationService notificationService,
        ISsoGroupRoleMappingModelFactory groupRoleMappingFactory,
        ISsoGroupRoleMappingService groupRoleMappingService)
    {
        _localizationService = localizationService;
        _notificationService = notificationService;
        _groupRoleMappingFactory = groupRoleMappingFactory;
        _groupRoleMappingService = groupRoleMappingService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Whether another mapping already uses this (store, IdP group) pair; adds the model error.
    /// </summary>
    protected virtual async Task<bool> IsDuplicateAsync(SsoGroupRoleMappingModel model, int? exceptId)
    {
        var existing = await _groupRoleMappingService.GetByStoreAndIdpGroupAsync(model.StoreId, model.IdpGroupId);
        if (existing is null || existing.Id == exceptId)
            return false;

        ModelState.AddModelError(nameof(model.IdpGroupId),
            await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId.Duplicate"));
        return true;
    }

    protected virtual void CopyToEntity(SsoGroupRoleMappingModel model, SsoGroupRoleMapping entity)
    {
        entity.StoreId = model.StoreId;
        entity.IdpGroupId = model.IdpGroupId.Trim();
        entity.IdpGroupDisplayName = model.IdpGroupDisplayName.Trim();
        entity.CustomerRoleId = model.CustomerRoleId;
        entity.DisplayOrder = model.DisplayOrder;
        entity.IsActive = model.IsActive;
    }

    protected virtual async Task<IActionResult> SavedAsync(SsoGroupRoleMapping entity, bool continueEditing)
    {
        _notificationService.SuccessNotification(
            await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.GroupRoleMapping.Saved"));

        return continueEditing
            ? RedirectToAction(nameof(GroupRoleMappingEdit), new { id = entity.Id })
            : RedirectToAction(nameof(GroupRoleMappings));
    }

    #endregion

    #region Methods

    /// <summary>
    /// Mapping list with a per-store filter.
    /// </summary>
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> GroupRoleMappings()
    {
        var model = await _groupRoleMappingFactory.PrepareSearchModelAsync(new SsoGroupRoleMappingSearchModel());
        return PluginView("GroupRoleMappings", model);
    }

    /// <summary>
    /// DataTables JSON for the mappings grid.
    /// </summary>
    [HttpPost]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> GroupRoleMappingsList(SsoGroupRoleMappingSearchModel searchModel)
        => Json(await _groupRoleMappingFactory.PrepareListModelAsync(searchModel));

    /// <summary>
    /// Create form.
    /// </summary>
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> GroupRoleMappingCreate()
        => PluginView("CreateOrUpdateGroupRoleMapping", await _groupRoleMappingFactory.PrepareMappingModelAsync(null, null));

    /// <summary>
    /// Save a new mapping; one per (store, IdP group).
    /// </summary>
    [HttpPost]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    [ParameterBasedOnFormName("save-continue", "continueEditing")]
    public virtual async Task<IActionResult> GroupRoleMappingCreate(SsoGroupRoleMappingModel model, bool continueEditing)
    {
        if (!ModelState.IsValid || await IsDuplicateAsync(model, exceptId: null))
            return PluginView("CreateOrUpdateGroupRoleMapping", await _groupRoleMappingFactory.PrepareMappingModelAsync(model, null));

        var entity = new SsoGroupRoleMapping();
        CopyToEntity(model, entity);
        await _groupRoleMappingService.InsertAsync(entity);

        return await SavedAsync(entity, continueEditing);
    }

    /// <summary>
    /// Edit form.
    /// </summary>
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> GroupRoleMappingEdit(int id)
    {
        var entity = await _groupRoleMappingService.GetByIdAsync(id);
        if (entity is null)
            return RedirectToAction(nameof(GroupRoleMappings));

        return PluginView("CreateOrUpdateGroupRoleMapping", await _groupRoleMappingFactory.PrepareMappingModelAsync(null, entity));
    }

    /// <summary>
    /// Save edits; the (store, IdP group) pair stays unique.
    /// </summary>
    [HttpPost]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    [ParameterBasedOnFormName("save-continue", "continueEditing")]
    public virtual async Task<IActionResult> GroupRoleMappingEdit(SsoGroupRoleMappingModel model, bool continueEditing)
    {
        var entity = await _groupRoleMappingService.GetByIdAsync(model.Id);
        if (entity is null)
            return RedirectToAction(nameof(GroupRoleMappings));

        if (!ModelState.IsValid || await IsDuplicateAsync(model, exceptId: entity.Id))
            return PluginView("CreateOrUpdateGroupRoleMapping", await _groupRoleMappingFactory.PrepareMappingModelAsync(model, entity));

        CopyToEntity(model, entity);
        await _groupRoleMappingService.UpdateAsync(entity);

        return await SavedAsync(entity, continueEditing);
    }

    /// <summary>
    /// Delete a mapping.
    /// </summary>
    [HttpPost]
    [CheckPermission(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS)]
    public virtual async Task<IActionResult> GroupRoleMappingDelete(int id)
    {
        var entity = await _groupRoleMappingService.GetByIdAsync(id);
        if (entity is null)
            return RedirectToAction(nameof(GroupRoleMappings));

        await _groupRoleMappingService.DeleteAsync(entity);

        _notificationService.SuccessNotification(
            await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.GroupRoleMapping.Deleted"));

        return RedirectToAction(nameof(GroupRoleMappings));
    }

    #endregion
}
