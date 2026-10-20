namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>
/// Outcome of comparing the stored IdP configuration against the SAML 2.0 metadata
/// published by the IdP. The handshake proves that the stored signing certificate is
/// the one the IdP currently uses, so a later real SAML response will validate.
/// Ported from the 4.9 plugin's <c>SSOHandshakeResult</c>.
/// </summary>
public class HandshakeResult
{
    /// <summary>Metadata document was fetched and parsed without error.</summary>
    public bool MetadataFetched { get; set; }

    /// <summary>
    /// EntityID advertised by the metadata equals stored <c>IdpEntityId</c>
    /// (Ordinal, case-sensitive — per the SAML 2.0 specification).
    /// </summary>
    public bool EntityIdMatches { get; set; }

    /// <summary>
    /// One of the SingleSignOnService Locations advertised in metadata (preferring the
    /// configured <c>LoginBinding</c>) equals stored <c>IdpSsoUrl</c>.
    /// </summary>
    public bool SsoUrlMatches { get; set; }

    /// <summary>
    /// SingleLogoutService check — <c>true</c> when either (a) no SLO URL is stored
    /// OR (b) one advertised SLO Location matches stored <c>IdpSloUrl</c>. SLO is
    /// optional in SAML 2.0 so absence is not a failure.
    /// </summary>
    public bool SloUrlMatches { get; set; }

    /// <summary>
    /// SHA-256 thumbprint of the stored IdP certificate matches at least one signing
    /// cert advertised in metadata. PASS is the load-bearing signal that "the stored
    /// cert will actually verify a real SAML response".
    /// </summary>
    public bool CertificateMatches { get; set; }

    /// <summary>
    /// <c>true</c>/<c>false</c> when the metadata document itself carries an XML
    /// signature that verifies against one of the certs in the metadata; <c>null</c>
    /// when the metadata is unsigned. Informational only — never gates the overall
    /// test (verifying with a cert embedded in the same document is circular).
    /// </summary>
    public bool? MetadataSigned { get; set; }

    /// <summary>
    /// SHA-256 thumbprints (uppercase hex, no separators) of every signing certificate
    /// advertised in the metadata. Surfaced so the admin can see what's coming during
    /// an IdP cert rotation.
    /// </summary>
    public IList<string> SigningKeyThumbprints { get; set; } = new List<string>();

    /// <summary>Human-readable summary of what happened.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>
    /// Populated when the handshake was deliberately skipped (for example, because
    /// <c>IdpMetadataUrl</c> is empty). When set, the overall test result ignores the
    /// handshake fields entirely.
    /// </summary>
    public string SkippedReason { get; set; } = string.Empty;
}
