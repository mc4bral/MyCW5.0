using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core.Domain.Customers;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Implements SAML 2.0 Single Log-Out for both SP-initiated and IdP-initiated flows.
/// </summary>
/// <remarks>
/// SP-initiated: the customer clicks <em>Logout</em> on the store. nop's own logout runs first
/// (local sign-out, activity log, events); <c>LogoutActionsFilter</c> then calls
/// <see cref="InitiateAsync"/> with the NameID and SessionIndex it captured beforehand, and the
/// browser is sent to the IdP's <c>SingleLogoutDestination</c>. The IdP clears its session and
/// returns to <c>/saml/logout</c> with a <c>LogoutResponse</c>.
///
/// IdP-initiated: the IdP sends a <c>LogoutRequest</c> to <c>/saml/logout</c> (typically when the
/// user logs out at the IdP). The plugin validates it, signs the local session out and replies
/// with a <c>LogoutResponse</c>.
///
/// Both flows write a <c>Slo</c> audit row (<c>Failed</c> on errors).
/// </remarks>
public interface ISamlLogoutHandler
{
    /// <summary>
    /// SP-initiated SLO, after nop has signed the customer out locally. Builds the
    /// <c>LogoutRequest</c> and redirects to the IdP; lands on the post-logout page when the IdP
    /// has no SLO endpoint.
    /// </summary>
    /// <param name="customer">The customer who just logged out</param>
    /// <param name="nameId">The NameID the IdP asserted at login</param>
    /// <param name="sessionIndex">The SessionIndex the IdP asserted at login</param>
    Task<IActionResult> InitiateAsync(Customer customer, string nameId, string? sessionIndex);

    /// <summary>IdP-initiated SLO. Unbinds the inbound LogoutRequest, signs out locally, replies with a LogoutResponse.</summary>
    Task<IActionResult> HandleLogoutRequestAsync(HttpRequest request);

    /// <summary>Callback after SP-initiated SLO. Validates the inbound LogoutResponse and lands on the post-logout page.</summary>
    Task<IActionResult> HandleLogoutResponseAsync(HttpRequest request);

    /// <summary>
    /// The store's post-logout landing page: <c>PostLogoutRedirectUrl</c> when it is a local URL,
    /// otherwise <c>/</c>.
    /// </summary>
    Task<string> GetPostLogoutUrlAsync();
}
