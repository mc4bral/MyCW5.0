using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Services.Saml;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;

namespace Nop.Plugin.ExternalAuth.SSO.Filters;

/// <summary>
/// Adds federated sign-out to nop's <c>Customer/Logout</c> for customers who signed in through SSO.
/// </summary>
/// <remarks>
/// <para>
/// nop's logout action runs <b>first</b> and does everything it normally does: ends an
/// impersonation, writes the <c>PublicStore.Logout</c> activity, signs out locally and publishes
/// <c>CustomerLoggedOutEvent</c>. Only then does this filter replace the action's redirect with
/// the IdP sign-out, chosen by <see cref="SsoCustomerAttributes.SignInProtocol"/> (recorded by the
/// provisioning service at login):
/// </para>
/// <list type="bullet">
///   <item><b>OIDC</b>: a <see cref="SignOutResult"/> on the OIDC scheme; the handler redirects to
///         the IdP's <c>end_session_endpoint</c>, which returns to the store's post-logout page.</item>
///   <item><b>SAML</b>: <see cref="ISamlLogoutHandler.InitiateAsync"/> with the NameID and
///         SessionIndex captured before nop's logout cleared the session.</item>
///   <item><b>Neither</b>, an <b>impersonation</b> ending, or the DEBUG <b>mock IdP</b>: nop's
///         result is left as it is.</item>
/// </list>
/// <para>
/// The OIDC handler looks for the <c>id_token</c> to send as <c>id_token_hint</c> in nop's
/// external-authentication cookie, which may be gone by logout time. Entra still ends the session
/// without it, but may show an account picker.
/// </para>
/// </remarks>
public class LogoutActionsFilter : IAsyncActionFilter
{
    #region Fields

    protected readonly ICustomerService _customerService;
    protected readonly IGenericAttributeService _genericAttributeService;
    protected readonly ISamlLogoutHandler _samlLogoutHandler;
    protected readonly ISettingService _settingService;
    protected readonly IStoreContext _storeContext;
    protected readonly IWorkContext _workContext;

    #endregion

    #region Ctor

    public LogoutActionsFilter(
        ICustomerService customerService,
        IGenericAttributeService genericAttributeService,
        ISamlLogoutHandler samlLogoutHandler,
        ISettingService settingService,
        IStoreContext storeContext,
        IWorkContext workContext)
    {
        _customerService = customerService;
        _genericAttributeService = genericAttributeService;
        _samlLogoutHandler = samlLogoutHandler;
        _settingService = settingService;
        _storeContext = storeContext;
        _workContext = workContext;
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

    private async Task ClearSsoAttributesAsync(Customer customer)
    {
        await _genericAttributeService.SaveAttributeAsync<string?>(customer, SsoCustomerAttributes.SamlNameId, null);
        await _genericAttributeService.SaveAttributeAsync<string?>(customer, SsoCustomerAttributes.SamlSessionIndex, null);
        await _genericAttributeService.SaveAttributeAsync<string?>(customer, SsoCustomerAttributes.SignInProtocol, null);
        await _genericAttributeService.SaveAttributeAsync<string?>(customer, SsoCustomerAttributes.OidcIdToken, null);
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

        // Capture everything before nop's logout clears the session.
        var customer = await _workContext.GetCurrentCustomerAsync();
        var endsImpersonation = _workContext.OriginalCustomerIfImpersonated is not null;
        var isRegistered = customer is not null && await _customerService.IsRegisteredAsync(customer);

        var protocol = isRegistered && !endsImpersonation
            ? await _genericAttributeService.GetAttributeAsync<string>(customer, SsoCustomerAttributes.SignInProtocol)
            : null;
        var nameId = !string.IsNullOrEmpty(protocol)
            ? await _genericAttributeService.GetAttributeAsync<string>(customer, SsoCustomerAttributes.SamlNameId)
            : null;
        var sessionIndex = !string.IsNullOrEmpty(protocol)
            ? await _genericAttributeService.GetAttributeAsync<string>(customer, SsoCustomerAttributes.SamlSessionIndex)
            : null;
        var idToken = !string.IsNullOrEmpty(protocol)
            ? await _genericAttributeService.GetAttributeAsync<string>(customer, SsoCustomerAttributes.OidcIdToken)
            : null;

        var executed = await next();

        if (string.IsNullOrEmpty(protocol) || customer is null || executed.Exception is not null && !executed.ExceptionHandled)
            return;

        await ClearSsoAttributesAsync(customer);

        // The DEBUG mock IdP has no session to end.
        var store = await _storeContext.GetCurrentStoreAsync();
        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);
        if (settings.EnableMockMode)
            return;

        if (string.Equals(protocol, SsoProtocolNames.Oidc, StringComparison.OrdinalIgnoreCase))
        {
            // nop's cookie is already gone; sign out of the OIDC scheme only. The handler
            // redirects to end_session_endpoint and the IdP returns to the post-logout page.
            // The id_token saved at sign-in rides along as id_token_hint (see
            // OidcAuthenticationRegistrar), so the IdP ends this session without asking.
            var properties = new AuthenticationProperties { RedirectUri = await _samlLogoutHandler.GetPostLogoutUrlAsync() };
            if (!string.IsNullOrEmpty(idToken))
                properties.Items[SSOAuthenticationDefaults.IdTokenHintItem] = idToken;

            executed.Result = new SignOutResult(SSOAuthenticationDefaults.OidcAuthenticationScheme, properties);
            return;
        }

        if (string.Equals(protocol, SsoProtocolNames.Saml2, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(nameId))
        {
            executed.Result = await _samlLogoutHandler.InitiateAsync(customer, nameId, sessionIndex);
        }
    }

    #endregion
}
