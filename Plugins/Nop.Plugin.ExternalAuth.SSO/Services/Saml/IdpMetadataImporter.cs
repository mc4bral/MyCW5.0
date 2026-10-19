using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using System.Xml.Linq;
using Nop.Plugin.ExternalAuth.SSO.Models;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Default implementation of <see cref="IIdpMetadataImporter"/>.
/// </summary>
/// <remarks>
/// Uses <see cref="XDocument"/> with secure reader settings (DTD prohibited, XmlResolver
/// disabled) to neutralise XXE before any data is read. Named HTTP client
/// <c>ExternalAuthSSO.MetadataFetcher</c> has a 10-second timeout configured in
/// NopStartup. SSRF guard: HTTPS only, no localhost / loopback / private IPv4 / IPv6
/// link-local — same shape as legacy H5.
/// </remarks>
public class IdpMetadataImporter : IIdpMetadataImporter
{
    /// <summary>Named HttpClient configured in NopStartup with a short timeout.</summary>
    public const string HttpClientName = "ExternalAuthSSO.MetadataFetcher";

    /// <summary>SAML 2.0 metadata namespace.</summary>
    private static readonly XNamespace Md = "urn:oasis:names:tc:SAML:2.0:metadata";

    /// <summary>XML Digital Signature namespace — where <c>KeyInfo</c> / <c>X509Data</c> live.</summary>
    private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";

    /// <summary>HTTP-Redirect binding URI.</summary>
    private const string HttpRedirectBinding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect";

    /// <summary>HTTP-POST binding URI.</summary>
    private const string HttpPostBinding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST";

    #region Fields

    private readonly IHttpClientFactory _httpClientFactory;

    #endregion

    #region Ctor

    public IdpMetadataImporter(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public Task<IdpMetadataImportResult> ImportFromXmlAsync(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new InvalidOperationException("Metadata XML is empty.");

        return Task.FromResult(Parse(xml));
    }

    /// <inheritdoc />
    public async Task<IdpMetadataImportResult> ImportFromUrlAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("URL is not a valid absolute URI.");

        EnsureUrlIsSafe(uri);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(uri);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync();
        return Parse(xml);
    }

    #endregion

    #region Utilities — parsing

    /// <summary>
    /// Parse the metadata XML using XXE-safe reader settings.
    /// </summary>
    private static IdpMetadataImportResult Parse(string xml)
    {
        var readerSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        XDocument doc;
        using (var stringReader = new StringReader(xml))
        using (var xmlReader = XmlReader.Create(stringReader, readerSettings))
        {
            doc = XDocument.Load(xmlReader);
        }

        var entityDescriptor = doc.Root;
        if (entityDescriptor is null || entityDescriptor.Name != Md + "EntityDescriptor")
            throw new InvalidOperationException("Root element is not <EntityDescriptor>.");

        var entityId = entityDescriptor.Attribute("entityID")?.Value ?? string.Empty;

        var idpSsoDescriptor = entityDescriptor.Element(Md + "IDPSSODescriptor")
            ?? throw new InvalidOperationException("Metadata contains no <IDPSSODescriptor>.");

        var (ssoUrl, ssoWarning) = ResolveBindingLocation(idpSsoDescriptor, "SingleSignOnService", required: true);
        var (sloUrl, sloWarning) = ResolveBindingLocation(idpSsoDescriptor, "SingleLogoutService", required: false);
        var (cert, certWarning) = ExtractSigningCertificate(idpSsoDescriptor);

        var warnings = new List<string>();
        if (!string.IsNullOrEmpty(ssoWarning)) warnings.Add(ssoWarning);
        if (!string.IsNullOrEmpty(sloWarning)) warnings.Add(sloWarning);
        if (!string.IsNullOrEmpty(certWarning)) warnings.Add(certWarning);

        return new IdpMetadataImportResult
        {
            IdpEntityId = entityId,
            IdpSsoUrl = ssoUrl,
            IdpSloUrl = sloUrl,
            IdpCertificateBase64 = cert,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Pick the best Location for an SSO/SLO service element. Prefers HTTP-Redirect, then
    /// HTTP-POST, then any binding. Returns empty + a warning when nothing is found.
    /// </summary>
    private static (string location, string warning) ResolveBindingLocation(
        XElement idpDescriptor,
        string elementName,
        bool required)
    {
        var endpoints = idpDescriptor.Elements(Md + elementName).ToList();

        if (endpoints.Count == 0)
        {
            return required
                ? throw new InvalidOperationException($"Metadata contains no <{elementName}> elements.")
                : (string.Empty, $"Metadata declares no {elementName} endpoint.");
        }

        var preferred = endpoints.FirstOrDefault(e => string.Equals(
                e.Attribute("Binding")?.Value, HttpRedirectBinding, StringComparison.Ordinal))
            ?? endpoints.FirstOrDefault(e => string.Equals(
                e.Attribute("Binding")?.Value, HttpPostBinding, StringComparison.Ordinal))
            ?? endpoints[0];

        return (preferred.Attribute("Location")?.Value ?? string.Empty, string.Empty);
    }

    /// <summary>
    /// Extract the first signing certificate as Base64 DER. Picks <c>use="signing"</c>
    /// first, then any KeyDescriptor without a <c>use</c> attribute (default = both).
    /// </summary>
    private static (string base64, string warning) ExtractSigningCertificate(XElement idpDescriptor)
    {
        var keyDescriptors = idpDescriptor.Elements(Md + "KeyDescriptor").ToList();
        if (keyDescriptors.Count == 0)
            return (string.Empty, "Metadata contains no <KeyDescriptor> — no IdP signing certificate to extract.");

        var signingKey = keyDescriptors.FirstOrDefault(k => string.Equals(
                k.Attribute("use")?.Value, "signing", StringComparison.OrdinalIgnoreCase))
            ?? keyDescriptors.FirstOrDefault(k => k.Attribute("use") is null)
            ?? keyDescriptors[0];

        var certElement = signingKey
            .Element(Ds + "KeyInfo")?
            .Element(Ds + "X509Data")?
            .Element(Ds + "X509Certificate");

        if (certElement is null || string.IsNullOrWhiteSpace(certElement.Value))
            return (string.Empty, "Found <KeyDescriptor> but no <X509Certificate> child.");

        // Strip whitespace and any PEM headers that crept in.
        var stripped = new string(certElement.Value
            .Where(c => !char.IsWhiteSpace(c))
            .ToArray());

        var warning = keyDescriptors.Count > 1
            ? "Metadata contains multiple <KeyDescriptor> elements — the first signing certificate was selected."
            : string.Empty;

        return (stripped, warning);
    }

    #endregion

    #region Utilities — SSRF guard

    /// <summary>
    /// Reject URLs that could be used to scan the local network or attack other internal
    /// services. HTTPS-only, hostname must resolve to a public address.
    /// </summary>
    private static void EnsureUrlIsSafe(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Metadata URL must use HTTPS.");

        if (uri.IsLoopback)
            throw new InvalidOperationException("Metadata URL must not point at the local host.");

        // Resolve the hostname and reject any address in a private / link-local / multicast
        // / loopback range. This is a best-effort defence — DNS-rebinding can defeat it, so
        // production deployments should also restrict outbound traffic at the network edge.
        IPAddress[] addresses;
        try
        {
            addresses = Dns.GetHostAddresses(uri.Host);
        }
        catch
        {
            throw new InvalidOperationException("Metadata URL host could not be resolved.");
        }

        foreach (var addr in addresses)
        {
            if (IsPrivateOrReservedAddress(addr))
                throw new InvalidOperationException(
                    $"Metadata URL host resolves to a private or reserved address ({addr}).");
        }
    }

    private static bool IsPrivateOrReservedAddress(IPAddress addr)
    {
        if (IPAddress.IsLoopback(addr)) return true;

        var bytes = addr.GetAddressBytes();

        // IPv4 private / link-local / TEST-NET / etc.
        if (bytes.Length == 4)
        {
            // 10.0.0.0/8
            if (bytes[0] == 10) return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            // 169.254.0.0/16 (link-local)
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            // 127.0.0.0/8 (loopback)
            if (bytes[0] == 127) return true;
            // 0.0.0.0/8
            if (bytes[0] == 0) return true;
        }

        // IPv6 link-local / site-local / ULA / loopback
        if (addr.IsIPv6LinkLocal || addr.IsIPv6SiteLocal) return true;
        if (bytes.Length == 16)
        {
            // fc00::/7 — Unique Local Addresses
            if ((bytes[0] & 0xFE) == 0xFC) return true;
        }

        return false;
    }

    #endregion

    #region M14 — Handshake fetch + parse

    /// <inheritdoc />
    public async Task<byte[]> FetchBytesAsync(string url, int maxBytes = 1024 * 1024)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("URL is not a valid absolute URI.");

        EnsureUrlIsSafe(uri);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var ms = new MemoryStream();
        var buffer = new byte[8192];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            total += read;
            if (total > maxBytes)
                throw new InvalidOperationException(
                    $"Metadata document exceeds {maxBytes / 1024} KB limit.");
            ms.Write(buffer, 0, read);
        }

        return ms.ToArray();
    }

    /// <inheritdoc />
    public SamlIdpMetadataSnapshot ParseSnapshot(byte[] xmlBytes)
    {
        if (xmlBytes is null || xmlBytes.Length == 0)
            throw new InvalidOperationException("Metadata document is empty.");

        // XmlReader on a MemoryStream auto-detects BOM + XML declaration encoding;
        // string-path parsing leaves a leading BOM that crashes "Data at the root level is
        // invalid" for ADFS metadata.
        var readerSettings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        XDocument doc;
        using (var ms = new MemoryStream(xmlBytes))
        using (var xmlReader = XmlReader.Create(ms, readerSettings))
        {
            doc = XDocument.Load(xmlReader);
        }

        return ParseSnapshotFromDocument(doc);
    }

    /// <summary>
    /// Walks the <c>EntityDescriptor</c> tree extracting the bits the handshake needs.
    /// Accepts either a single <c>EntityDescriptor</c> document or an
    /// <c>EntitiesDescriptor</c> federation document (first IdP child wins).
    /// </summary>
    private static SamlIdpMetadataSnapshot ParseSnapshotFromDocument(XDocument doc)
    {
        var root = doc.Root ?? throw new InvalidOperationException("Metadata XML has no root element.");

        XElement? entityDescriptor;
        if (root.Name == Md + "EntityDescriptor")
        {
            entityDescriptor = root;
        }
        else if (root.Name == Md + "EntitiesDescriptor")
        {
            entityDescriptor = root.Elements(Md + "EntityDescriptor")
                                   .FirstOrDefault(e => e.Element(Md + "IDPSSODescriptor") is not null)
                               ?? root.Elements(Md + "EntityDescriptor").FirstOrDefault();
        }
        else
        {
            entityDescriptor = null;
        }

        if (entityDescriptor is null)
            throw new InvalidOperationException("Metadata document has no EntityDescriptor element.");

        var snapshot = new SamlIdpMetadataSnapshot
        {
            EntityId = entityDescriptor.Attribute("entityID")?.Value ?? string.Empty,
            HasSignature = entityDescriptor.Element(Ds + "Signature") is not null
        };

        var idpDescriptor = entityDescriptor.Element(Md + "IDPSSODescriptor")
            ?? throw new InvalidOperationException(
                "EntityDescriptor does not contain an IDPSSODescriptor — this metadata document does not describe an Identity Provider.");

        foreach (var sso in idpDescriptor.Elements(Md + "SingleSignOnService"))
        {
            snapshot.SsoEndpoints.Add(new SamlMetadataEndpoint
            {
                Binding = sso.Attribute("Binding")?.Value ?? string.Empty,
                Location = sso.Attribute("Location")?.Value ?? string.Empty
            });
        }

        foreach (var slo in idpDescriptor.Elements(Md + "SingleLogoutService"))
        {
            snapshot.SloEndpoints.Add(new SamlMetadataEndpoint
            {
                Binding = slo.Attribute("Binding")?.Value ?? string.Empty,
                Location = slo.Attribute("Location")?.Value ?? string.Empty
            });
        }

        // KeyDescriptor with use="signing" OR no use attribute (per SAML metadata spec
        // = valid for both). Skip use="encryption" — those can't validate assertions.
        foreach (var key in idpDescriptor.Elements(Md + "KeyDescriptor"))
        {
            var use = key.Attribute("use")?.Value;
            if (!string.IsNullOrEmpty(use)
                && !string.Equals(use, "signing", StringComparison.OrdinalIgnoreCase))
                continue;

            var certElement = key.Element(Ds + "KeyInfo")
                ?.Element(Ds + "X509Data")
                ?.Element(Ds + "X509Certificate");
            if (certElement is null || string.IsNullOrWhiteSpace(certElement.Value))
                continue;

            var b64 = new string(certElement.Value.Where(c => !char.IsWhiteSpace(c)).ToArray());
            X509Certificate2 parsed;
            try
            {
                parsed = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(b64));
            }
            catch
            {
                // Malformed cert in metadata — skip silently. Handshake surfaces the count of
                // valid certs; one bad entry should not abort the parse.
                continue;
            }

            snapshot.SigningCerts.Add(new SamlMetadataSigningCert
            {
                Base64 = b64,
                Sha256Thumbprint = ComputeSha256Thumbprint(parsed),
                Certificate = parsed
            });
        }

        return snapshot;
    }

    /// <summary>
    /// SHA-256 thumbprint of the certificate's raw DER, uppercase hex with no separators.
    /// Chosen over <see cref="X509Certificate2.Thumbprint"/> (SHA-1, deprecated) so the
    /// handshake never accepts a SHA-1 collision.
    /// </summary>
    public static string ComputeSha256Thumbprint(X509Certificate2 cert)
    {
        if (cert is null) throw new ArgumentNullException(nameof(cert));
        var hash = SHA256.HashData(cert.RawData);
        return Convert.ToHexString(hash); // uppercase, no separators by default
    }

    #endregion
}
