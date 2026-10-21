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

    /// <summary>
    /// Compare a SAML store's IdP fields with its metadata URL and record the result on the store's
    /// settings (<c>IdpMetadataCheckedOnUtc</c>, <c>IdpMetadataDifferences</c>). With
    /// <paramref name="allowAutoApply"/> and the store's <c>AutoApplyIdpMetadata</c> on, the IdP's
    /// values are written over the ones that differ (audit row <c>ConfigChanged</c>).
    /// </summary>
    /// <param name="storeId">Store</param>
    /// <param name="allowAutoApply">Whether the caller may change settings (the daily check: yes)</param>
    /// <returns>What still differs (empty = matches), or null when the store has no SAML metadata URL</returns>
    Task<string?> CheckMetadataAsync(int storeId, bool allowAutoApply);
}
