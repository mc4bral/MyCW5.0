using Microsoft.AspNetCore.Mvc;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Builds the redirect <see cref="IActionResult"/> that kicks off an SP-initiated SAML 2.0
/// login flow.
/// </summary>
public interface ISamlChallengeBuilder
{
    /// <summary>
    /// Build the AuthnRequest, persist the request ID to a short-lived correlation cookie
    /// (along with <paramref name="returnUrl"/>), bind it to the per-store login binding
    /// (HTTP-Redirect or HTTP-POST), and return the corresponding redirect/form result.
    /// </summary>
    Task<IActionResult> BuildChallengeAsync(string? returnUrl);
}
