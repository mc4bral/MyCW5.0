using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Http;
using Nop.Plugin.ExternalAuth.SSO.Services.Saml;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.ExternalAuth.SSO.Controllers;

/// <summary>
/// Public-facing SAML 2.0 endpoints: ACS (assertion consumer), metadata, logout.
/// </summary>
/// <remarks>
/// Pattern: thin controller, all the SAML machinery lives in the <c>Services/Saml/</c>
/// services. Anonymous + <c>[IgnoreAntiforgeryToken]</c> on the ACS POST and SLO POST
/// because the IdP signs those requests itself — antiforgery is not the right primitive.
/// </remarks>
[AllowAnonymous]
public class SamlController : BasePluginController
{
    #region Fields

    protected readonly ISamlLogoutHandler _logoutHandler;
    protected readonly ISamlMetadataBuilder _metadataBuilder;
    protected readonly ISamlResponseProcessor _responseProcessor;

    #endregion

    #region Ctor

    public SamlController(
        ISamlLogoutHandler logoutHandler,
        ISamlMetadataBuilder metadataBuilder,
        ISamlResponseProcessor responseProcessor)
    {
        _logoutHandler = logoutHandler;
        _metadataBuilder = metadataBuilder;
        _responseProcessor = responseProcessor;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Assertion Consumer Service — receives the IdP's signed <c>SAMLResponse</c> via HTTP-POST.
    /// </summary>
    [HttpPost]
    [Route("saml/acs")]
    [IgnoreAntiforgeryToken]
    [CheckAccessPublicStore(ignore: true)]
    public async Task<IActionResult> Acs()
    {
        return await _responseProcessor.ProcessAcsAsync(Request);
    }

    /// <summary>
    /// SP metadata endpoint. Returns the per-store SAML <c>EntityDescriptor</c> XML for IdP
    /// consumption (anonymous; nothing in the metadata is secret).
    /// </summary>
    /// <remarks>
    /// Catches configuration errors (incomplete settings, undecryptable certificates) and
    /// returns 503 with no body so admins fetching the endpoint pre-config don't see stack
    /// traces.
    /// </remarks>
    [HttpGet]
    [Route("saml/metadata")]
    [CheckAccessPublicStore(ignore: true)]
    public async Task<IActionResult> Metadata()
    {
        try
        {
            return await _metadataBuilder.BuildMetadataAsync();
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>
    /// SAML SLO. Dispatches by request shape:
    /// <list type="bullet">
    ///   <item>contains <c>SAMLRequest</c> → IdP-initiated logout (handle + reply with <c>LogoutResponse</c>);</item>
    ///   <item>contains <c>SAMLResponse</c> → callback after SP-initiated logout (validate, land home);</item>
    ///   <item>neither → nop's logout, which signs out locally and then federates through
    ///         <c>LogoutActionsFilter</c>.</item>
    /// </list>
    /// </summary>
    [HttpGet]
    [HttpPost]
    [Route("saml/logout")]
    [IgnoreAntiforgeryToken]
    [CheckAccessPublicStore(ignore: true)]
    public async Task<IActionResult> Logout()
    {
        if (HasSamlField("SAMLRequest"))
            return await _logoutHandler.HandleLogoutRequestAsync(Request);

        if (HasSamlField("SAMLResponse"))
            return await _logoutHandler.HandleLogoutResponseAsync(Request);

        return RedirectToRoute(NopRouteNames.Standard.LOGOUT);
    }

    /// <summary>
    /// True when the inbound request carries a SAML field (in query, form, or headers).
    /// SAML allows either query parameters (HTTP-Redirect binding) or form fields (HTTP-POST).
    /// </summary>
    private bool HasSamlField(string field)
    {
        if (Request.Query.ContainsKey(field))
            return true;

        if (Request.HasFormContentType && Request.Form.ContainsKey(field))
            return true;

        return false;
    }

    #endregion
}
