using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Orchestrates the per-store auth-result handoff from an upstream IdP (OIDC or SAML)
/// to nopCommerce's external-authentication pipeline.
/// </summary>
/// <remarks>
/// Called from the public auth controller's callback action with the
/// <see cref="ClaimsPrincipal"/> produced by the protocol handler. Runs the security gates
/// (email-domain allowlist, admin-account link block) and writes audit rows, then delegates
/// to <c>IExternalAuthenticationService.AuthenticateAsync</c> for the actual sign-in.
///
/// M4 scope: default-role-only provisioning — group claims are read into the audit Notes
/// but not translated to nop roles. Group → role mapping lands in M3 by replacing the
/// roleResolution branch inside <see cref="HandleAsync"/>.
/// </remarks>
public interface ISsoUserProvisioningService
{
    /// <summary>
    /// Run security gates against the asserted identity, audit, and hand off to nop.
    /// Returns the <see cref="IActionResult"/> the controller should yield.
    /// </summary>
    Task<IActionResult> HandleAsync(
        ClaimsPrincipal principal,
        string protocolName,
        string? accessToken,
        string? returnUrl);
}
