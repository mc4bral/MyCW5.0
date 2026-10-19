using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Services.Authentication;
using Nop.Services.Common;
using Nop.Services.Customers;

namespace Nop.Plugin.ExternalAuth.SSO.Filters;

/// <summary>
/// Intercepts <c>/Customer/Logout</c> for customers signed in via SSO and routes the
/// logout through the appropriate federated SLO flow before nop's local sign-out runs.
/// </summary>
/// <remarks>
/// Branches by <see cref="SsoCustomerAttributes.SignInProtocol"/> stored on the customer
/// at login time:
/// <list type="bullet">
///   <item><b>SAML</b>: redirect to <c>/saml/logout</c>. The <see cref="Services.Saml.ISamlLogoutHandler"/>
///         reads <c>NameID</c> + <c>SessionIndex</c> (still readable because the local
///         session cookie is still intact at filter-time), builds the <c>LogoutRequest</c>,
///         then signs out locally and redirects to the IdP SLO endpoint.</item>
///   <item><b>OIDC</b>: yield a multi-scheme <see cref="SignOutResult"/> targeting both
///         the standard nop cookie scheme and the OIDC scheme. The OIDC handler intercepts
///         and redirects to the discovered <c>end_session_endpoint</c> with the
///         <c>id_token_hint</c>; Entra clears its session and redirects back to the
///         configured <see cref="SSOAuthenticationDefaults.OidcSignedOutCallbackPath"/>.</item>
///   <item><b>Neither</b> (local-only login): no-op, the standard logout action runs.</item>
/// </list>
/// </remarks>
public class LogoutActionsFilter : IAsyncActionFilter
{
    #region Fields

    private readonly ICustomerService _customerService;
    private readonly IGenericAttributeService _genericAttributeService;
    private readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public LogoutActionsFilter(
        ICustomerService customerService,
        IGenericAttributeService genericAttributeService,
        IWorkContext workContext)
    {
        _customerService = customerService;
        _genericAttributeService = genericAttributeService;
        _workContext = workContext;
    }

    #endregion

    #region IAsyncActionFilter

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!IsCustomerLogout(context))
        {
            await next();
            return;
        }

        var customer = await _workContext.GetCurrentCustomerAsync();
        if (customer is null || !await _customerService.IsRegisteredAsync(customer))
        {
            await next();
            return;
        }

        var protocol = await _genericAttributeService.GetAttributeAsync<string>(
            customer, SsoCustomerAttributes.SignInProtocol);

        if (string.Equals(protocol, SsoProtocolNames.Saml2, StringComparison.OrdinalIgnoreCase))
        {
            // SAML: divert to the plugin's /saml/logout endpoint. The handler will read the
            // customer's NameID/SessionIndex, sign them out locally, and redirect to the IdP.
            context.Result = new RedirectResult(SSOAuthenticationDefaults.SamlLogoutPath);
            return;
        }

        if (string.Equals(protocol, SsoProtocolNames.Oidc, StringComparison.OrdinalIgnoreCase))
        {
            // OIDC: federated sign-out via the handler. The framework signs out of both
            // schemes; the OIDC handler issues the redirect to end_session_endpoint.
            context.Result = new SignOutResult(
                new[]
                {
                    NopAuthenticationDefaults.AuthenticationScheme,
                    SSOAuthenticationDefaults.OidcAuthenticationScheme
                },
                new AuthenticationProperties { RedirectUri = "/" });
            return;
        }

        // No SSO state — let the standard nop logout action run.
        await next();
    }

    #endregion

    #region Utilities

    private static bool IsCustomerLogout(ActionExecutingContext context)
    {
        var routeValues = context.RouteData.Values;
        var controller = routeValues["controller"] as string;
        var action = routeValues["action"] as string;

        return string.Equals(controller, "Customer", StringComparison.OrdinalIgnoreCase)
            && string.Equals(action, "Logout", StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
