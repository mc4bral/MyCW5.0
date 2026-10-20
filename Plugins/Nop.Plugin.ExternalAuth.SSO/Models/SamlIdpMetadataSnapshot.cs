using System.Security.Cryptography.X509Certificates;

namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>
/// Standard SAML 2.0 binding URNs as they appear on a metadata
/// <c>SingleSignOnService</c> or <c>SingleLogoutService</c> element.
/// </summary>
public static class SamlMetadataBindings
{
    public const string HttpRedirect = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect";
    public const string HttpPost = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST";
}

/// <summary>
/// One endpoint advertised by the IdP metadata, scoped to a specific binding.
/// </summary>
public class SamlMetadataEndpoint
{
    public string Binding { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

/// <summary>
/// A signing certificate advertised in an IdP metadata <c>KeyDescriptor use="signing"</c>
/// block, plus the SHA-256 thumbprint of the parsed X.509.
/// </summary>
public class SamlMetadataSigningCert
{
    public string Base64 { get; set; } = string.Empty;

    /// <summary>SHA-256 thumbprint, uppercase hex, no separators.</summary>
    public string Sha256Thumbprint { get; set; } = string.Empty;

    public X509Certificate2? Certificate { get; set; }
}

/// <summary>
/// In-memory snapshot of the bits of an IdP <c>EntityDescriptor</c> the
/// Test Connection handshake compares against the stored per-store configuration.
/// </summary>
public class SamlIdpMetadataSnapshot
{
    public string EntityId { get; set; } = string.Empty;
    public IList<SamlMetadataEndpoint> SsoEndpoints { get; set; } = new List<SamlMetadataEndpoint>();
    public IList<SamlMetadataEndpoint> SloEndpoints { get; set; } = new List<SamlMetadataEndpoint>();
    public IList<SamlMetadataSigningCert> SigningCerts { get; set; } = new List<SamlMetadataSigningCert>();

    /// <summary>
    /// <c>true</c> when the <c>EntityDescriptor</c> element carries a
    /// <c>&lt;ds:Signature&gt;</c> child. Verification is performed by the caller —
    /// the parser only flags presence.
    /// </summary>
    public bool HasSignature { get; set; }
}
