using Nop.Plugin.ExternalAuth.SSO.Models;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Lightweight reachability + certificate check for a per-store SSO configuration.
/// </summary>
/// <remarks>
/// OIDC: GET <c>{Authority}/.well-known/openid-configuration</c>, verify the response is
/// parseable JSON.
///
/// SAML: parse the IdP signing certificate (verify it's a valid X.509 with future
/// <c>NotAfter</c>), then HEAD the IdP SSO URL via the SSRF-guarded client (`SafeUrl`).
///
/// Returns a structured result rather than throwing; callers surface the message + details
/// inline next to the Test button on the SSO Accounts grid.
/// </remarks>
public interface ISsoTestConnectionService
{
    /// <summary>Run the test for <paramref name="storeId"/>.</summary>
    Task<TestConnectionResult> TestAsync(int storeId);
}
