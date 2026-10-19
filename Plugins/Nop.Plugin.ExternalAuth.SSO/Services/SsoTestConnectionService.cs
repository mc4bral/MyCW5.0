using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Services.Saml;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Configuration;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Default implementation of <see cref="ISsoTestConnectionService"/>.
/// </summary>
public class SsoTestConnectionService : ISsoTestConnectionService
{
    /// <summary>
    /// Reuses the metadata-fetcher HttpClient: HTTPS-aware, 10-second timeout, accepts
    /// JSON / XML media types — fine for both the OIDC discovery doc and the SAML HEAD probe.
    /// </summary>
    private const string HttpClientName = IdpMetadataImporter.HttpClientName;

    #region Fields

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IIdpMetadataImporter _metadataImporter;
    private readonly ISettingService _settingService;
    private readonly ISsoEventService _eventService;
    private readonly ISsoSecretsService _secretsService;

    #endregion

    #region Ctor

    public SsoTestConnectionService(
        IHttpClientFactory httpClientFactory,
        IIdpMetadataImporter metadataImporter,
        ISettingService settingService,
        ISsoEventService eventService,
        ISsoSecretsService secretsService)
    {
        _httpClientFactory = httpClientFactory;
        _metadataImporter = metadataImporter;
        _settingService = settingService;
        _eventService = eventService;
        _secretsService = secretsService;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<TestConnectionResult> TestAsync(int storeId)
    {
        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(storeId);

        var result = settings.Protocol switch
        {
            SsoProtocol.OpenIdConnect => await TestOidcAsync(settings),
            SsoProtocol.Saml2 => await TestSamlAsync(settings, storeId),
            _ => Fail("SSO protocol is set to Off — nothing to test.")
        };

        // M14 — write a forced HandshakeRun audit row on every click. Forced=true so the
        // breadcrumb survives even when EnableAuditLogging is off; the row is the
        // tamper-evidence trail of who tested SSO and when.
        await _eventService.LogAsync(
            eventType: SsoAuthEventTypes.HandshakeRun,
            protocol: settings.Protocol.ToString(),
            storeId: storeId,
            customerId: null,
            customerEmail: string.Empty,
            idpSubject: string.Empty,
            notes: BuildHandshakeAuditNotes(result),
            forced: true);

        return result;
    }

    /// <summary>
    /// Compact one-line summary of the test outcome for the audit Notes column —
    /// readable in the admin grid without scrolling. Mirrors legacy
    /// <c>BuildHandshakeDetail</c> shape.
    /// </summary>
    private static string BuildHandshakeAuditNotes(TestConnectionResult result)
    {
        var h = result.Handshake;
        if (!string.IsNullOrEmpty(h.SkippedReason))
            return $"Overall={(result.Success ? "PASS" : "FAIL")}; handshake skipped: {h.SkippedReason}";

        return $"Overall={(result.Success ? "PASS" : "FAIL")}; "
            + $"EntityID={(h.EntityIdMatches ? "match" : "mismatch")}; "
            + $"SSO URL={(h.SsoUrlMatches ? "match" : "mismatch")}; "
            + $"SLO URL={(h.SloUrlMatches ? "match" : "mismatch")}; "
            + $"Certificate={(h.CertificateMatches ? "match" : "mismatch")} "
            + $"({h.SigningKeyThumbprints.Count} cert(s) in metadata)";
    }

    #endregion

    #region Utilities — OIDC

    private async Task<TestConnectionResult> TestOidcAsync(SsoConfigurationSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Authority))
            return Fail("OIDC Authority is not configured.");

        var discoveryUrl = settings.Authority.TrimEnd('/') + "/.well-known/openid-configuration";

        try
        {
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await client.GetAsync(discoveryUrl);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json); // throws on invalid JSON

            var issuer = doc.RootElement.TryGetProperty("issuer", out var issuerEl)
                ? issuerEl.GetString() ?? "(no issuer)"
                : "(no issuer)";

            return Ok(
                $"OIDC discovery document fetched successfully.",
                $"Discovery URL: {discoveryUrl}\nIssuer reported by IdP: {issuer}");
        }
        catch (Exception ex)
        {
            return Fail($"OIDC discovery fetch failed: {ex.Message}");
        }
    }

    #endregion

    #region Utilities — SAML

    private async Task<TestConnectionResult> TestSamlAsync(SsoConfigurationSettings settings, int storeId)
    {
        if (string.IsNullOrWhiteSpace(settings.IdpSsoUrl))
            return Fail("IdP SSO URL is not configured.");

        // 1. Legacy cert health check (parse + NotAfter > now).
        var certResult = ValidateIdpCertificate(settings);
        if (!certResult.Success)
            return certResult;

        // 2. Legacy URL reachability probe — kept so admins still get HEAD-level signal
        //    even when IdpMetadataUrl is empty.
        try
        {
            using var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Head, settings.IdpSsoUrl);
            using var response = await client.SendAsync(request);

            // Entra rejects HEAD on SSO endpoints with 405 — that still proves the host
            // is reachable, which is the actual signal we want.
            if (!response.IsSuccessStatusCode
                && response.StatusCode != System.Net.HttpStatusCode.MethodNotAllowed)
            {
                return Fail($"IdP SSO URL returned HTTP {(int)response.StatusCode}.");
            }
        }
        catch (Exception ex)
        {
            return Fail($"IdP SSO URL probe failed: {ex.Message}");
        }

        // 3. M14 — real metadata handshake. Optional: skipped cleanly when
        //    IdpMetadataUrl is empty so admins without published metadata still get the
        //    legacy reachability + cert checks above.
        var handshake = await RunHandshakeAsync(settings);

        var ok = Ok(
            BuildSuccessMessage(handshake),
            BuildSuccessDetails(handshake, certResult.Details));
        ok.Handshake = handshake;

        // Overall failure when the handshake was attempted but any sub-check failed.
        if (string.IsNullOrEmpty(handshake.SkippedReason)
            && (!handshake.MetadataFetched
                || !handshake.EntityIdMatches
                || !handshake.SsoUrlMatches
                || !handshake.SloUrlMatches
                || !handshake.CertificateMatches))
        {
            ok.Success = false;
            ok.Message = "SAML handshake failed — stored IdP configuration does not match the IdP's published metadata.";
        }

        return ok;
    }

    /// <summary>
    /// Fetches IdP metadata, parses it, and compares against the stored configuration.
    /// Returns a populated <see cref="HandshakeResult"/>; never throws — all error
    /// paths surface via <c>SkippedReason</c> or by leaving the per-check booleans
    /// at <c>false</c> with a diagnostic in <c>Detail</c>.
    /// </summary>
    private async Task<HandshakeResult> RunHandshakeAsync(SsoConfigurationSettings settings)
    {
        var handshake = new HandshakeResult();

        if (string.IsNullOrWhiteSpace(settings.IdpMetadataUrl))
        {
            handshake.SkippedReason = "IdP Metadata URL is not configured.";
            handshake.Detail = handshake.SkippedReason;
            return handshake;
        }

        byte[] bytes;
        try
        {
            bytes = await _metadataImporter.FetchBytesAsync(settings.IdpMetadataUrl);
        }
        catch (Exception ex)
        {
            handshake.Detail = $"Metadata fetch failed: {ex.Message}";
            return handshake;
        }

        SamlIdpMetadataSnapshot md;
        try
        {
            md = _metadataImporter.ParseSnapshot(bytes);
        }
        catch (Exception ex)
        {
            handshake.Detail = $"Metadata parse failed: {ex.Message}";
            return handshake;
        }

        handshake.MetadataFetched = true;
        handshake.SigningKeyThumbprints = md.SigningCerts.Select(c => c.Sha256Thumbprint).ToList();

        // 1. EntityID — ordinal, case-sensitive per SAML 2.0 spec.
        handshake.EntityIdMatches = !string.IsNullOrEmpty(md.EntityId)
            && string.Equals(md.EntityId, settings.IdpEntityId, StringComparison.Ordinal);

        // 2. SSO URL — scoped to the configured login binding, fall back to any binding.
        var loginBindingUrn = MapBindingToUrn(settings.LoginBinding);
        handshake.SsoUrlMatches = AnyEndpointMatches(md.SsoEndpoints, loginBindingUrn, settings.IdpSsoUrl);

        // 3. SLO URL — optional. Absent stored SLO URL = pass (SAML 2.0 allows IdPs to
        //    not advertise SLO at all).
        if (string.IsNullOrWhiteSpace(settings.IdpSloUrl))
        {
            handshake.SloUrlMatches = true;
        }
        else
        {
            var logoutBindingUrn = MapBindingToUrn(settings.LogoutBinding);
            handshake.SloUrlMatches = AnyEndpointMatches(md.SloEndpoints, logoutBindingUrn, settings.IdpSloUrl);
        }

        // 4. Cert thumbprint — the load-bearing handshake signal.
        try
        {
            var storedCert = ParseStoredIdpCertificate(settings);
            if (storedCert is null)
            {
                handshake.Detail = "Stored IdP certificate could not be parsed for comparison.";
            }
            else
            {
                var storedThumbprint = IdpMetadataImporter.ComputeSha256Thumbprint(storedCert);
                handshake.CertificateMatches = md.SigningCerts.Any(c =>
                    string.Equals(c.Sha256Thumbprint, storedThumbprint, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch (Exception ex)
        {
            handshake.Detail = $"Stored IdP certificate could not be loaded for comparison: {ex.Message}";
        }

        // 5. Metadata signature — informational; never gates the overall result. A
        //    metadata document signed by a cert it advertises is circular proof.
        if (md.HasSignature)
        {
            // ITfoxtec / SignedXml verification could go here in a future tightening; for
            // now we surface presence only.
            handshake.MetadataSigned = null;
        }

        // Dispose parsed metadata certs (they hold native crypto handles).
        foreach (var c in md.SigningCerts)
            c.Certificate?.Dispose();

        if (string.IsNullOrEmpty(handshake.Detail))
            handshake.Detail = BuildHandshakeDetail(handshake, md.SigningCerts.Count);

        return handshake;
    }

    private static string MapBindingToUrn(string? shortBinding)
    {
        // Settings store "HTTP-Redirect" / "HTTP-POST" as the short form; legacy
        // BindingsRedirects card uses these strings. Default is HTTP-Redirect.
        if (!string.IsNullOrWhiteSpace(shortBinding)
            && shortBinding.Contains("POST", StringComparison.OrdinalIgnoreCase))
            return SamlMetadataBindings.HttpPost;
        return SamlMetadataBindings.HttpRedirect;
    }

    /// <summary>
    /// Prefer endpoints with the configured binding, fall back to any endpoint with a
    /// matching <c>Location</c>. SAML 2.0 lets an IdP advertise the same URL under
    /// multiple bindings — ADFS in particular ships one Location across both.
    /// </summary>
    private static bool AnyEndpointMatches(
        IEnumerable<SamlMetadataEndpoint> endpoints,
        string preferredBindingUrn,
        string storedLocation)
    {
        if (endpoints is null || string.IsNullOrWhiteSpace(storedLocation))
            return false;

        var list = endpoints.ToList();
        var preferred = list.Where(e => string.Equals(e.Binding, preferredBindingUrn, StringComparison.Ordinal)).ToList();
        var candidates = preferred.Count > 0 ? preferred : list;

        return candidates.Any(e => string.Equals(
            e.Location?.Trim(), storedLocation.Trim(), StringComparison.Ordinal));
    }

    /// <summary>
    /// Load the stored encrypted IdP cert and return it ready for SHA-256 thumbprint
    /// comparison. Returns <c>null</c> when nothing is stored or decryption fails.
    /// </summary>
    private X509Certificate2? ParseStoredIdpCertificate(SsoConfigurationSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.IdpCertificateBase64))
            return null;
        if (!_secretsService.TryUnprotect(settings.IdpCertificateBase64, out var plaintext)
            || string.IsNullOrWhiteSpace(plaintext))
            return null;

        var stripped = new string(plaintext
            .Replace("-----BEGIN CERTIFICATE-----", string.Empty)
            .Replace("-----END CERTIFICATE-----", string.Empty)
            .Where(c => !char.IsWhiteSpace(c))
            .ToArray());

        try
        {
            return X509CertificateLoader.LoadCertificate(Convert.FromBase64String(stripped));
        }
        catch
        {
            return null;
        }
    }

    private static string BuildHandshakeDetail(HandshakeResult h, int metadataCertCount)
    {
        var parts = new List<string>
        {
            $"EntityID={(h.EntityIdMatches ? "match" : "mismatch")}",
            $"SSO URL={(h.SsoUrlMatches ? "match" : "mismatch")}",
            $"SLO URL={(h.SloUrlMatches ? "match" : "mismatch")}",
            $"Certificate={(h.CertificateMatches ? "match" : "mismatch")} ({metadataCertCount} signing cert(s) in metadata)"
        };
        if (h.MetadataSigned.HasValue)
            parts.Add($"Metadata signature={(h.MetadataSigned.Value ? "valid" : "invalid")}");
        return string.Join("; ", parts);
    }

    private static string BuildSuccessMessage(HandshakeResult h)
    {
        if (!string.IsNullOrEmpty(h.SkippedReason))
            return "SAML cert + URL checks passed. Configure an IdP Metadata URL to enable the cryptographic handshake.";
        return "SAML handshake succeeded — stored configuration matches the IdP's published metadata.";
    }

    private static string BuildSuccessDetails(HandshakeResult h, string certDetails)
    {
        var sections = new List<string> { certDetails };
        if (!string.IsNullOrEmpty(h.Detail))
            sections.Add($"Handshake: {h.Detail}");
        if (h.SigningKeyThumbprints.Count > 0)
            sections.Add($"Metadata signing thumbprints (SHA-256):\n  - {string.Join("\n  - ", h.SigningKeyThumbprints)}");
        return string.Join("\n", sections.Where(s => !string.IsNullOrEmpty(s)));
    }

    private TestConnectionResult ValidateIdpCertificate(SsoConfigurationSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.IdpCertificateBase64))
            return Fail("IdP signing certificate is not configured.");

        if (!_secretsService.TryUnprotect(settings.IdpCertificateBase64, out var plaintext)
            || string.IsNullOrWhiteSpace(plaintext))
            return Fail("IdP signing certificate could not be decrypted.");

        try
        {
            var stripped = new string(plaintext
                .Replace("-----BEGIN CERTIFICATE-----", string.Empty)
                .Replace("-----END CERTIFICATE-----", string.Empty)
                .Where(c => !char.IsWhiteSpace(c))
                .ToArray());

            var bytes = Convert.FromBase64String(stripped);
            var cert = X509CertificateLoader.LoadCertificate(bytes);

            var now = DateTime.UtcNow;
            var notAfter = cert.NotAfter.ToUniversalTime();
            if (notAfter < now)
                return Fail($"IdP signing certificate expired on {notAfter:yyyy-MM-dd}.");

            return Ok(
                "Certificate parsed.",
                $"Subject: {cert.Subject}\nIssuer: {cert.Issuer}\nNotAfter: {notAfter:yyyy-MM-dd} UTC");
        }
        catch (Exception ex)
        {
            return Fail($"IdP signing certificate is not a valid X.509 (Base64 DER): {ex.Message}");
        }
    }

    #endregion

    #region Utilities — result helpers

    private static TestConnectionResult Ok(string message, string? details = null) =>
        new()
        {
            Success = true,
            Message = message,
            Details = details ?? string.Empty,
            CheckedAtUtc = DateTime.UtcNow
        };

    private static TestConnectionResult Fail(string message, string? details = null) =>
        new()
        {
            Success = false,
            Message = message,
            Details = details ?? string.Empty,
            CheckedAtUtc = DateTime.UtcNow
        };

    #endregion
}
