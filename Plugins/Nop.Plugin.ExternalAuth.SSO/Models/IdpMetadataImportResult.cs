namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>
/// Output of <see cref="Services.Saml.IIdpMetadataImporter"/>. Serialised to JSON by the
/// admin import action; the Configure page reads it client-side to prefill IdP fields.
/// </summary>
public record IdpMetadataImportResult
{
    /// <summary><c>entityID</c> attribute on the root <c>EntityDescriptor</c>.</summary>
    public string IdpEntityId { get; init; } = string.Empty;

    /// <summary>Selected <c>SingleSignOnService</c> location. Prefers HTTP-Redirect binding, falls back to HTTP-POST.</summary>
    public string IdpSsoUrl { get; init; } = string.Empty;

    /// <summary>Selected <c>SingleLogoutService</c> location (HTTP-Redirect preferred). Empty when the IdP doesn't publish SLO.</summary>
    public string IdpSloUrl { get; init; } = string.Empty;

    /// <summary>First <c>X509Certificate</c> from a <c>KeyDescriptor</c> marked <c>use="signing"</c> (or unmarked).</summary>
    public string IdpCertificateBase64 { get; init; } = string.Empty;

    /// <summary>
    /// Non-fatal observations (e.g. "metadata declares no SLO endpoint", "multiple signing
    /// certificates present — first was selected"). Surfaced to the admin UI so they can
    /// review before saving.
    /// </summary>
    public IList<string> Warnings { get; init; } = new List<string>();
}
