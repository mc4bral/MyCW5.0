using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Implements SAML 2.0 Single Log-Out for both SP-initiated and IdP-initiated flows.
/// </summary>
/// <remarks>
/// SP-initiated: customer clicks <em>Logout</em> on the store. The plugin builds a
/// <c>LogoutRequest</c>, signs the customer out locally, and redirects to the IdP's
/// <c>SingleLogoutDestination</c>. The IdP clears its own session and redirects back to
/// the same endpoint with a <c>LogoutResponse</c>.
///
/// IdP-initiated: the IdP POSTs a <c>LogoutRequest</c> directly to the SP logout endpoint
/// (typically when the user logs out at the IdP and the IdP cascades sign-out to relying
/// parties). The plugin validates, signs the local session out, and replies with a
/// <c>LogoutResponse</c>.
///
/// Both flows write a <c>Slo</c> audit row on completion (<c>Failed</c> on errors).
/// </remarks>
public interface ISamlLogoutHandler
{
    /// <summary>SP-initiated SLO. Builds the LogoutRequest, signs out locally, redirects to the IdP.</summary>
    Task<IActionResult> InitiateAsync();

    /// <summary>IdP-initiated SLO. Unbinds the inbound LogoutRequest, signs out locally, replies with a LogoutResponse.</summary>
    Task<IActionResult> HandleLogoutRequestAsync(HttpRequest request);

    /// <summary>Callback after SP-initiated SLO. Validates the inbound LogoutResponse and finalises.</summary>
    Task<IActionResult> HandleLogoutResponseAsync(HttpRequest request);
}
