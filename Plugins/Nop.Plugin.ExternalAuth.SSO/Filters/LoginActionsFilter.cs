using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Services;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Authentication.External;
using Nop.Services.Configuration;

namespace Nop.Plugin.ExternalAuth.SSO.Filters;

/// <summary>
/// Front-door filter that intercepts <c>GET /Customer/Login</c> when SSO is on for the
/// current store and redirects to the external-auth challenge — unless the request carries
/// a valid <c>?ssobypass=&lt;token&gt;</c> or the bypass cookie is already present.
/// </summary>
/// <remarks>
/// See References/SSO_Plugin_Reference.md §6. Controller/action are matched by string so the plugin does not
/// need a project reference to <c>Nop.Web</c> (designs out legacy P0-7/P0-8). All
/// dependencies are constructor-injected per AGENTS.md §4.
/// </remarks>
public class LoginActionsFilter : IAsyncActionFilter
{
    #region Fields

    protected readonly IAuthenticationPluginManager _authenticationPluginManager;
    protected readonly ISettingService _settingService;
    protected readonly ISsoSecretsService _secretsService;
    protected readonly IStoreContext _storeContext;
    protected readonly IUrlHelperFactory _urlHelperFactory;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public LoginActionsFilter(
        IAuthenticationPluginManager authenticationPluginManager,
        ISettingService settingService,
        ISsoSecretsService secretsService,
        IStoreContext storeContext,
        IUrlHelperFactory urlHelperFactory,
        IWorkContext workContext)
    {
        _authenticationPluginManager = authenticationPluginManager;
        _settingService = settingService;
        _secretsService = secretsService;
        _storeContext = storeContext;
        _urlHelperFactory = urlHelperFactory;
        _workContext = workContext;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// True when the current request is the standard nop login GET (<c>/Customer/Login</c>).
    /// Matched by controller + action + HTTP method strings to avoid an explicit reference
    /// to <c>Nop.Web</c>.
    /// </summary>
    private static bool IsCustomerLoginGet(ActionExecutingContext context)
    {
        var routeValues = context.RouteData.Values;
        var controller = routeValues["controller"] as string;
        var action = routeValues["action"] as string;

        return HttpMethods.IsGet(context.HttpContext.Request.Method)
            && string.Equals(controller, "Customer", StringComparison.OrdinalIgnoreCase)
            && string.Equals(action, "Login", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Pulls the <c>?ssobypass=&lt;token&gt;</c> value off the query string if present.
    /// </summary>
    private static bool TryReadBypassToken(HttpRequest request, out string token)
    {
        if (request.Query.TryGetValue(SSOAuthenticationDefaults.BypassQueryStringKey, out var v))
        {
            token = v.ToString();
            return !string.IsNullOrEmpty(token);
        }

        token = string.Empty;
        return false;
    }

    /// <summary>
    /// Sets the short-lived bypass cookie. <see cref="CookieOptions.HttpOnly"/> + SameSite=Lax
    /// keeps it inaccessible to scripts and immune to most CSRF vectors.
    /// </summary>
    private static void IssueBypassCookie(HttpResponse response)
    {
        response.Cookies.Append(
            SSOAuthenticationDefaults.BypassCookieName,
            "1",
            new CookieOptions
            {
                HttpOnly = true,
                Secure = response.HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.Add(SSOAuthenticationDefaults.BypassCookieLifetime),
                IsEssential = true
            });
    }

    /// <summary>
    /// Compare the provided bypass token with the stored one without leaking either through timing.
    /// Both are hashed first, so the comparison is over two equal-length digests: the length of the
    /// stored token does not show, and <see cref="CryptographicOperations.FixedTimeEquals"/> takes the
    /// same time whichever byte differs.
    /// </summary>
    internal static bool TokensMatch(string provided, string stored)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var storedHash = SHA256.HashData(Encoding.UTF8.GetBytes(stored));

        return CryptographicOperations.FixedTimeEquals(providedHash, storedHash);
    }

    #endregion

    #region IAsyncActionFilter

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!IsCustomerLoginGet(context))
        {
            await next();
            return;
        }

        var store = await _storeContext.GetCurrentStoreAsync();
        var perStore = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);

        // SSO not active for this store — standard login form renders normally.
        // Or the admin opted out of the auto-redirect via RedirectLoginToSso —
        // SSO is still available via the public "Sign in with Microsoft" button on the
        // login page, but /login itself stays on the standard local form.
        if (!perStore.IsEnabled || perStore.Protocol == SsoProtocol.Off || !perStore.RedirectLoginToSso)
        {
            await next();
            return;
        }

        // The plugin switched off in nop's external sign-in methods (or not allowed for this
        // customer / store): the challenge would refuse and send the browser back to /login, which
        // would redirect again — an endless loop and no login page. Show the local form instead.
        if (!await _authenticationPluginManager.IsPluginActiveAsync(
                SSOAuthenticationDefaults.SystemName, await _workContext.GetCurrentCustomerAsync(), store.Id))
        {
            await next();
            return;
        }

        // Cookie set on a previous turn — admin is mid-bypass, let the local form render.
        if (context.HttpContext.Request.Cookies.ContainsKey(SSOAuthenticationDefaults.BypassCookieName))
        {
            await next();
            return;
        }

        // Fresh bypass URL — validate the token and stamp the cookie.
        if (TryReadBypassToken(context.HttpContext.Request, out var provided))
        {
            var systemWide = await _settingService.LoadSettingAsync<SsoSystemSettings>(storeId: 0);
            var stored = _secretsService.TryUnprotect(systemWide.SsoBypassToken, out var t) ? t : null;

            if (!string.IsNullOrEmpty(stored) && TokensMatch(provided, stored))
            {
                IssueBypassCookie(context.HttpContext.Response);
                await next();
                return;
            }
            // Token didn't match — fall through to SSO redirect (don't leak whether the
            // bypass is even configured).
        }

        // Default path: redirect to the plugin's external-auth challenge.
        // ActionExecutingContext inherits from ActionContext so it can be passed directly.
        var urlHelper = _urlHelperFactory.GetUrlHelper(context);
        var returnUrl = context.HttpContext.Request.Query["ReturnUrl"].ToString();
        var challengeUrl = urlHelper.Action("Login", "ExternalAuthentication", new { returnUrl });
        if (string.IsNullOrEmpty(challengeUrl))
        {
            // Couldn't resolve the route — let the request continue rather than break login.
            await next();
            return;
        }

        context.Result = new RedirectResult(challengeUrl);
    }

    #endregion
}
