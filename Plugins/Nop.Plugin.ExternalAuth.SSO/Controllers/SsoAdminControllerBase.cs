using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.ExternalAuth.SSO.Controllers;

/// <summary>
/// Base of the plugin's admin controllers: admin area, admin-only, antiforgery on every POST.
/// </summary>
/// <remarks>
/// The plugin does not reference CW.Core, so it cannot use nop's <c>BaseAdminController</c>
/// family; this applies the same three attributes once instead of on every action. nop's
/// <c>[CheckPermission]</c> can only be placed on methods, so each action still carries it.
/// </remarks>
[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public abstract class SsoAdminControllerBase : BasePluginController
{
    /// <summary>
    /// A view from the plugin's <c>Views</c> folder.
    /// </summary>
    /// <param name="name">View file name without extension</param>
    /// <param name="model">Model</param>
    protected ViewResult PluginView(string name, object? model = null)
        => View($"{SsoAdminControllers.ViewsPath}{name}.cshtml", model);
}
