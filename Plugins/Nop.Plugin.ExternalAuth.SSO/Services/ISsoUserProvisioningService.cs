using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Orchestrates the per-store auth-result handoff from an upstream IdP (OIDC or SAML)
/// to nopCommerce's external-authentication pipeline.
/// </summary>
/// <remarks>
/// Called by the OIDC callback, the SAML ACS and the DEBUG mock IdP with the
/// <see cref="ClaimsPrincipal"/> the protocol produced. Runs the security gates (email-domain
/// allowlist, admin-account block, auto-registration switch), links an existing customer by
/// email, and delegates the sign-in to <c>IExternalAuthenticationService.AuthenticateAsync</c>.
/// Only when nop actually signed the asserted customer in does it record the sign-in protocol,
/// apply group → role mapping and write the success audit row.
/// </remarks>
public interface ISsoUserProvisioningService
{
    /// <summary>
    /// Run security gates against the asserted identity, audit, and hand off to nop.
    /// Returns the <see cref="IActionResult"/> the controller should yield.
    /// </summary>
    /// <param name="principal">Identity the protocol produced</param>
    /// <param name="protocolName">One of <c>SsoProtocolNames</c></param>
    /// <param name="accessToken">OIDC access token, when there is one</param>
    /// <param name="returnUrl">Page to return to</param>
    /// <param name="idToken">OIDC id_token, kept on the customer for <c>id_token_hint</c> at logout</param>
    Task<IActionResult> HandleAsync(
        ClaimsPrincipal principal,
        string protocolName,
        string? accessToken,
        string? returnUrl,
        string? idToken = null);
}
