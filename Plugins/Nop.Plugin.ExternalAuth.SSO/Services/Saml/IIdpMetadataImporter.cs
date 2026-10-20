using Nop.Plugin.ExternalAuth.SSO.Models;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Reads SAML 2.0 IdP metadata (through ITfoxtec) into the four IdP fields the plugin needs on its
/// Configure page and into the richer snapshot used by the Test-connection handshake.
/// </summary>
public interface IIdpMetadataImporter
{
    /// <summary>
    /// Parse an <c>EntityDescriptor</c> XML document into the flat result used by the
    /// Configure-page prefill UI. Throws on unparseable input.
    /// </summary>
    Task<IdpMetadataImportResult> ImportFromXmlAsync(string xml);

    /// <summary>
    /// Fetch metadata from <paramref name="url"/> (HTTPS-only, public-IP only — SSRF
    /// guarded) and parse the response. Throws on transport errors, SSRF rejection, or
    /// unparseable response.
    /// </summary>
    Task<IdpMetadataImportResult> ImportFromUrlAsync(string url);

    /// <summary>
    /// Fetch metadata as a byte stream. Used by the Test Connection handshake so
    /// BOM-prefixed XML (typical of ADFS) parses cleanly via XmlReader's encoding
    /// auto-detection — going through a UTF-8 string would leave a leading BOM that
    /// throws "Data at the root level is invalid". The size cap (1 MB) defuses zip-bomb
    /// / OOM tricks if an attacker can influence the URL.
    /// </summary>
    Task<byte[]> FetchBytesAsync(string url, int maxBytes = 1024 * 1024);

    /// <summary>
    /// Parse a metadata document into the rich snapshot used by the handshake — keeps
    /// per-binding endpoint info and SHA-256 cert thumbprints. Throws on unparseable input.
    /// </summary>
    SamlIdpMetadataSnapshot ParseSnapshot(byte[] xmlBytes);
}
