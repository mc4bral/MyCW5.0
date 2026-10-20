using System.Text;
using System.Xml;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Services.Http;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Default implementation of <see cref="IIdpMetadataImporter"/>.
/// </summary>
/// <remarks>
/// <para>
/// Parsing is ITfoxtec's <see cref="EntityDescriptor.ReadIdPSsoDescriptor"/>: it refuses DTDs (no
/// XXE), accepts a single <c>EntityDescriptor</c> or a federation <c>EntitiesDescriptor</c>, and
/// returns the IdP's endpoints and signing certificates. Both the Configure-page import and the
/// Test-connection handshake go through <see cref="ParseSnapshot"/>, so they read metadata the
/// same way.
/// </para>
/// <para>
/// ITfoxtec does not verify a metadata signature; neither does this class — the handshake reports
/// only that one is present, and trust comes from comparing the IdP certificate thumbprint.
/// </para>
/// <para>
/// Fetching stays here: HTTPS only and no private addresses (<see cref="SafeUrl"/>), a 1 MB cap,
/// and the named client <see cref="HttpClientName"/> (10-second timeout, set in NopStartup).
/// </para>
/// </remarks>
public class IdpMetadataImporter : IIdpMetadataImporter
{
    /// <summary>Named HttpClient configured in NopStartup with a short timeout.</summary>
    public const string HttpClientName = "ExternalAuthSSO.MetadataFetcher";

    private const string XmlDsigNamespace = "http://www.w3.org/2000/09/xmldsig#";

    #region Fields

    protected readonly IHttpClientFactory _httpClientFactory;

    #endregion

    #region Ctor

    public IdpMetadataImporter(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Text of a metadata document, with its encoding detected from the BOM / XML declaration and
    /// the BOM removed (ITfoxtec's reader rejects a leading BOM; ADFS metadata usually has one).
    /// </summary>
    private static string DecodeXml(byte[] xmlBytes)
    {
        using var reader = new StreamReader(new MemoryStream(xmlBytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd().TrimStart('﻿');
    }

    /// <summary>
    /// Whether the root element carries an XML signature. Presence only; nothing is verified.
    /// </summary>
    private static bool HasRootSignature(string xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };

        using var reader = XmlReader.Create(new StringReader(xml), settings);
        if (reader.MoveToContent() != XmlNodeType.Element || reader.IsEmptyElement)
            return false;

        var rootDepth = reader.Depth;
        while (reader.Read() && reader.Depth > rootDepth)
        {
            if (reader.NodeType == XmlNodeType.Element
                && reader.Depth == rootDepth + 1
                && reader.LocalName == "Signature"
                && reader.NamespaceURI == XmlDsigNamespace)
                return true;
        }

        return false;
    }

    private static SamlIdpMetadataSnapshot ReadSnapshot(string xml)
    {
        EntityDescriptor entityDescriptor;
        try
        {
            entityDescriptor = new EntityDescriptor().ReadIdPSsoDescriptor(xml);
        }
        catch (XmlException ex)
        {
            throw new InvalidOperationException($"Metadata is not valid XML: {ex.Message}", ex);
        }

        var idp = entityDescriptor.IdPSsoDescriptor
            ?? throw new InvalidOperationException(
                "The metadata has no IDPSSODescriptor — it does not describe an Identity Provider.");

        static SamlMetadataEndpoint ToEndpoint(Uri? binding, Uri? location) => new()
        {
            Binding = binding?.OriginalString ?? string.Empty,
            Location = location?.OriginalString ?? string.Empty
        };

        var snapshot = new SamlIdpMetadataSnapshot
        {
            EntityId = entityDescriptor.EntityId ?? string.Empty,
            HasSignature = HasRootSignature(xml)
        };

        foreach (var sso in idp.SingleSignOnServices ?? [])
            snapshot.SsoEndpoints.Add(ToEndpoint(sso.Binding, sso.Location));

        foreach (var slo in idp.SingleLogoutServices ?? [])
            snapshot.SloEndpoints.Add(ToEndpoint(slo.Binding, slo.Location));

        foreach (var certificate in idp.SigningCertificates ?? [])
        {
            snapshot.SigningCerts.Add(new SamlMetadataSigningCert
            {
                Base64 = Convert.ToBase64String(certificate.RawData),
                Sha256Thumbprint = SsoCertificates.Sha256Thumbprint(certificate),
                Certificate = certificate
            });
        }

        return snapshot;
    }

    /// <summary>
    /// The endpoint the Configure page should use: HTTP-Redirect, then HTTP-POST, then any.
    /// </summary>
    private static string PreferredLocation(IList<SamlMetadataEndpoint> endpoints)
        => (endpoints.FirstOrDefault(e => e.Binding == SamlMetadataBindings.HttpRedirect)
            ?? endpoints.FirstOrDefault(e => e.Binding == SamlMetadataBindings.HttpPost)
            ?? endpoints.FirstOrDefault())?.Location ?? string.Empty;

    /// <summary>
    /// The four Configure-page fields, with warnings for what the admin should check.
    /// </summary>
    private static IdpMetadataImportResult ToImportResult(SamlIdpMetadataSnapshot snapshot)
    {
        try
        {
            if (snapshot.SsoEndpoints.Count == 0)
                throw new InvalidOperationException("Metadata contains no SingleSignOnService endpoint.");

            var warnings = new List<string>();
            if (snapshot.SloEndpoints.Count == 0)
                warnings.Add("Metadata declares no SingleLogoutService endpoint.");
            if (snapshot.SigningCerts.Count == 0)
                warnings.Add("Metadata contains no signing certificate.");
            if (snapshot.SigningCerts.Count > 1)
                warnings.Add("Metadata contains several signing certificates — the first was selected.");

            return new IdpMetadataImportResult
            {
                IdpEntityId = snapshot.EntityId,
                IdpSsoUrl = PreferredLocation(snapshot.SsoEndpoints),
                IdpSloUrl = PreferredLocation(snapshot.SloEndpoints),
                IdpCertificateBase64 = snapshot.SigningCerts.FirstOrDefault()?.Base64 ?? string.Empty,
                Warnings = warnings
            };
        }
        finally
        {
            foreach (var cert in snapshot.SigningCerts)
                cert.Certificate?.Dispose();
        }
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public Task<IdpMetadataImportResult> ImportFromXmlAsync(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new InvalidOperationException("Metadata XML is empty.");

        return Task.FromResult(ToImportResult(ReadSnapshot(xml.TrimStart('﻿'))));
    }

    /// <inheritdoc />
    public async Task<IdpMetadataImportResult> ImportFromUrlAsync(string url)
        => ToImportResult(ParseSnapshot(await FetchBytesAsync(url)));

    /// <inheritdoc />
    public async Task<byte[]> FetchBytesAsync(string url, int maxBytes = 1024 * 1024)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("URL is not a valid absolute URI.");

        SafeUrl.EnsureSafe(uri, "Metadata URL");

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
                throw new InvalidOperationException($"Metadata document exceeds {maxBytes / 1024} KB limit.");
            ms.Write(buffer, 0, read);
        }

        return ms.ToArray();
    }

    /// <inheritdoc />
    public SamlIdpMetadataSnapshot ParseSnapshot(byte[] xmlBytes)
    {
        if (xmlBytes is null || xmlBytes.Length == 0)
            throw new InvalidOperationException("Metadata document is empty.");

        return ReadSnapshot(DecodeXml(xmlBytes));
    }

    #endregion
}
