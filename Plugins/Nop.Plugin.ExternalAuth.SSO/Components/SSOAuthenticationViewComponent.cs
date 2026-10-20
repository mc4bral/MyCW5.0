using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Configuration;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.ExternalAuth.SSO.Components;

/// <summary>
/// Renders the public-facing "Sign in with Microsoft" button on the standard login page.
/// </summary>
/// <remarks>
/// Returns empty content when SSO is disabled for the current store
/// (<see cref="SsoConfigurationSettings.IsEnabled"/> = <c>false</c> or
/// <see cref="SsoConfigurationSettings.Protocol"/> = <c>Off</c>) so the button only appears
/// on stores where it can actually work.
/// </remarks>
public class SSOAuthenticationViewComponent : NopViewComponent
{
    #region Fields

    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;

    #endregion

    #region Ctor

    public SSOAuthenticationViewComponent(
        ISettingService settingService,
        IStoreContext storeContext)
    {
        _settingService = settingService;
        _storeContext = storeContext;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Invoke the view component.
    /// </summary>
    /// <param name="widgetZone">Widget zone name (supplied by nop's external-auth plumbing).</param>
    /// <param name="additionalData">Additional data (not used).</param>
    public async Task<IViewComponentResult> InvokeAsync(string widgetZone, object additionalData)
    {
        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);

        if (!settings.IsEnabled || settings.Protocol == SsoProtocol.Off)
            return Content(string.Empty);

        // admin-configurable IdP display name drives the button label.
        // Falls back to "Microsoft" so existing installations without the new field still
        // render a sensible button.
        ViewData["IdpDisplayName"] = string.IsNullOrWhiteSpace(settings.IdpDisplayName)
            ? "Microsoft"
            : settings.IdpDisplayName.Trim();

        return View("~/Plugins/ExternalAuth.SSO/Views/PublicInfo.cshtml");
    }

    #endregion
}
