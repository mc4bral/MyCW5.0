using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Authentication;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Messages;

namespace Nop.Plugin.ExternalAuth.SSO.Filters;

/// <summary>
/// Back-door filter that runs after <c>POST /Customer/Login</c> when the bypass cookie is
/// set. Verifies the authenticated customer carries at least one role in
/// <see cref="SsoSystemSettings.LocalLoginAllowedRoles"/>; signs them out, audits, and
/// redirects back to the login page otherwise.
/// </summary>
/// <remarks>
/// See SSOPlan.v2.md §3.7. Controller/action matched by string (no <c>Nop.Web</c>
/// reference). All dependencies constructor-injected per AGENTS.md §4.
///
/// The bypass cookie is intentionally preserved after a rejected attempt so the user can
/// retry on the local form — without it the next GET would loop through the front-door
/// filter back into SSO.
/// </remarks>
public class LocalLoginEnforcementFilter : IAsyncActionFilter
{
    #region Fields

    private readonly IAuthenticationService _authenticationService;
    private readonly ICustomerService _customerService;
    private readonly ILocalizationService _localizationService;
    private readonly INotificationService _notificationService;
    private readonly ISettingService _settingService;
    private readonly ISsoEventService _eventService;
    private readonly IStoreContext _storeContext;
    private readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public LocalLoginEnforcementFilter(
        IAuthenticationService authenticationService,
        ICustomerService customerService,
        ILocalizationService localizationService,
        INotificationService notificationService,
        ISettingService settingService,
        ISsoEventService eventService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _authenticationService = authenticationService;
        _customerService = customerService;
        _localizationService = localizationService;
        _notificationService = notificationService;
        _settingService = settingService;
        _eventService = eventService;
        _storeContext = storeContext;
        _workContext = workContext;
    }

    #endregion

    #region IAsyncActionFilter

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Only interested in POST /Customer/Login and only when bypass is active.
        if (!IsCustomerLoginPost(context)
            || !context.HttpContext.Request.Cookies.ContainsKey(SSOAuthenticationDefaults.BypassCookieName))
        {
            await next();
            return;
        }

        // Let the local-login action run normally.
        var executed = await next();

        // If model state was invalid, the action redisplayed the form — no authenticated
        // customer to enforce against. Leave the cookie in place so the user can retry.
        var customer = await _workContext.GetCurrentCustomerAsync();
        if (customer is null || !await _customerService.IsRegisteredAsync(customer))
            return;

        // Authenticated. Check whether the customer's roles overlap the configured allowlist.
        var systemWide = await _settingService.LoadSettingAsync<SsoSystemSettings>(storeId: 0);
        var allowed = ParseRoleSystemNames(systemWide.LocalLoginAllowedRoles);
        var roles = await _customerService.GetCustomerRolesAsync(customer);

        if (allowed.Count > 0 && roles.Any(r => allowed.Contains(r.SystemName)))
        {
            // Authorised — bypass succeeded. Clear the cookie so the next /login defaults
            // back to the SSO redirect.
            ClearBypassCookie(context.HttpContext.Response);
            return;
        }

        // Not authorised — sign out, audit, notify, redirect back to /login.
        var store = await _storeContext.GetCurrentStoreAsync();
        var email = customer.Email ?? string.Empty;
        var customerId = customer.Id;

        await _authenticationService.SignOutAsync();

        await _eventService.LogAsync(
            eventType: SsoAuthEventTypes.BypassDenied,
            protocol: SsoProtocolNames.Oidc,
            storeId: store.Id,
            customerId: customerId,
            customerEmail: email,
            idpSubject: string.Empty,
            notes: SsoFailureReasons.Format(SsoFailureReasons.BypassNotInRole,
                $"Customer is not in any of the LocalLoginAllowedRoles ({systemWide.LocalLoginAllowedRoles})."));

        _notificationService.WarningNotification(
            await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Login.AccessDenied"));

        // Cookie preserved on purpose — see XML doc.
        executed.Result = new RedirectResult("/login");
    }

    #endregion

    #region Utilities

    private static bool IsCustomerLoginPost(ActionExecutingContext context)
    {
        var routeValues = context.RouteData.Values;
        var controller = routeValues["controller"] as string;
        var action = routeValues["action"] as string;

        return HttpMethods.IsPost(context.HttpContext.Request.Method)
            && string.Equals(controller, "Customer", StringComparison.OrdinalIgnoreCase)
            && string.Equals(action, "Login", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> ParseRoleSystemNames(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void ClearBypassCookie(HttpResponse response)
    {
        response.Cookies.Delete(
            SSOAuthenticationDefaults.BypassCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = response.HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            });
    }

    #endregion
}
