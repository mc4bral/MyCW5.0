using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Models;
using Nop.Plugin.ExternalAuth.SSO.Services.Http;
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

    protected readonly IHttpClientFactory _httpClientFactory;
    protected readonly IIdpMetadataImporter _metadataImporter;
    protected readonly ISettingService _settingService;
    protected readonly ISsoEventService _eventService;
    protected readonly ISsoSecretsService _secretsService;

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

    #region Utilities

    #region OIDC

    private async Task<TestConnectionResult> TestOidcAsync(SsoConfigurationSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Authority))
            return Fail("OIDC Authority is not configured.");

        var discoveryUrl = settings.Authority.TrimEnd('/') + "/.well-known/openid-configuration";

        if (!SafeUrl.TryValidate(discoveryUrl, "OIDC discovery URL", out var unsafeReason))
            return Fail(unsafeReason);

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

    #region SAML

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
        if (!SafeUrl.TryValidate(settings.IdpSsoUrl, "IdP SSO URL", out var unsafeReason))
            return Fail(unsafeReason);

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

        // 3. real metadata handshake. Optional: skipped cleanly when
        //    IdpMetadataUrl is empty so admins without published metadata still get the
        //    legacy reachability + cert checks above.
        var handshake = await RunHandshakeAsync(settings);
        if (string.IsNullOrEmpty(handshake.SkippedReason))
            await SaveMetadataStatusAsync(settings, storeId, DescribeDifferences(handshake));

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
        var loginBindingUrn = settings.LoginBinding.ToUrn();
        handshake.SsoUrlMatches = AnyEndpointMatches(md.SsoEndpoints, loginBindingUrn, settings.IdpSsoUrl);

        // 3. SLO URL — optional. Absent stored SLO URL = pass (SAML 2.0 allows IdPs to
        //    not advertise SLO at all).
        if (string.IsNullOrWhiteSpace(settings.IdpSloUrl))
        {
            handshake.SloUrlMatches = true;
        }
        else
        {
            var logoutBindingUrn = settings.LogoutBinding.ToUrn();
            handshake.SloUrlMatches = AnyEndpointMatches(md.SloEndpoints, logoutBindingUrn, settings.IdpSloUrl);
        }

        // 4. Cert thumbprint — the load-bearing handshake signal.
        if (SsoCertificates.TryLoadIdpCertificate(_secretsService, settings, out var storedCert, out var storedCertError))
        {
            using (storedCert)
            {
                var storedThumbprint = SsoCertificates.Sha256Thumbprint(storedCert!);
                handshake.CertificateMatches = md.SigningCerts.Any(c =>
                    string.Equals(c.Sha256Thumbprint, storedThumbprint, StringComparison.OrdinalIgnoreCase));
            }
        }
        else
        {
            handshake.Detail = $"Stored IdP certificate could not be loaded for comparison: {storedCertError}";
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

    /// <summary>
    /// What a metadata comparison found, in words for the admin: the fields that differ
    /// ("signing certificate, SSO URL"), or why the metadata could not be compared. Empty when
    /// everything matched or no comparison was attempted (no metadata URL).
    /// </summary>
    internal static string DescribeDifferences(HandshakeResult handshake)
    {
        if (!string.IsNullOrEmpty(handshake.SkippedReason))
            return string.Empty;

        if (!handshake.MetadataFetched)
            return $"the metadata could not be read ({handshake.Detail})";

        var fields = new List<string>();
        if (!handshake.EntityIdMatches) fields.Add("entity ID");
        if (!handshake.SsoUrlMatches) fields.Add("SSO URL");
        if (!handshake.SloUrlMatches) fields.Add("SLO URL");
        if (!handshake.CertificateMatches) fields.Add("signing certificate");

        return string.Join(", ", fields);
    }

    /// <summary>
    /// Record the result of a metadata comparison on the store's settings: when, and what differed.
    /// Read by the Configure page and System → Warnings.
    /// </summary>
    private async Task SaveMetadataStatusAsync(SsoConfigurationSettings settings, int storeId, string differences)
    {
        settings.IdpMetadataCheckedOnUtc = DateTime.UtcNow;
        settings.IdpMetadataDifferences = differences;
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.IdpMetadataCheckedOnUtc, true, storeId, false);
        await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.IdpMetadataDifferences, true, storeId, false);
        await _settingService.ClearCacheAsync();
    }

    /// <summary>
    /// Write the IdP's published values over the ones that differ (the store opted in with
    /// <see cref="SsoConfigurationSettings.AutoApplyIdpMetadata"/>). The certificate is replaced only
    /// when the stored one is no longer published, so an IdP that lists an old and a new certificate
    /// during a rollover keeps working with the stored one.
    /// </summary>
    private async Task ApplyMetadataAsync(SsoConfigurationSettings settings, int storeId, HandshakeResult handshake)
    {
        var imported = await _metadataImporter.ImportFromUrlAsync(settings.IdpMetadataUrl);

        if (!handshake.EntityIdMatches && !string.IsNullOrEmpty(imported.IdpEntityId))
        {
            settings.IdpEntityId = imported.IdpEntityId;
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.IdpEntityId, true, storeId, false);
        }

        if (!handshake.SsoUrlMatches && !string.IsNullOrEmpty(imported.IdpSsoUrl))
        {
            settings.IdpSsoUrl = imported.IdpSsoUrl;
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.IdpSsoUrl, true, storeId, false);
        }

        if (!handshake.SloUrlMatches && !string.IsNullOrEmpty(imported.IdpSloUrl))
        {
            settings.IdpSloUrl = imported.IdpSloUrl;
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.IdpSloUrl, true, storeId, false);
        }

        if (!handshake.CertificateMatches && !string.IsNullOrEmpty(imported.IdpCertificateBase64))
        {
            settings.IdpCertificateBase64 = _secretsService.Protect(imported.IdpCertificateBase64);
            await _settingService.SaveSettingOverridablePerStoreAsync(settings, x => x.IdpCertificateBase64, true, storeId, false);
        }

        await _settingService.ClearCacheAsync();
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
        if (!SsoCertificates.TryLoadIdpCertificate(_secretsService, settings, out var cert, out var error))
            return Fail(error);

        using (cert)
        {
            var notAfter = cert!.NotAfter.ToUniversalTime();
            if (notAfter < DateTime.UtcNow)
                return Fail($"IdP signing certificate expired on {notAfter:yyyy-MM-dd}.");

            return Ok(
                "Certificate parsed.",
                $"Subject: {cert.Subject}\nIssuer: {cert.Issuer}\nNotAfter: {notAfter:yyyy-MM-dd} UTC");
        }
    }

    #endregion

    #region Result helpers

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

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<string?> CheckMetadataAsync(int storeId, bool allowAutoApply)
    {
        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(storeId);
        if (settings.Protocol != SsoProtocol.Saml2 || string.IsNullOrWhiteSpace(settings.IdpMetadataUrl))
            return null;

        var handshake = await RunHandshakeAsync(settings);
        var differences = DescribeDifferences(handshake);

        if (differences.Length > 0 && handshake.MetadataFetched && allowAutoApply && settings.AutoApplyIdpMetadata)
        {
            try
            {
                await ApplyMetadataAsync(settings, storeId, handshake);

                await _eventService.LogAsync(
                    eventType: SsoAuthEventTypes.ConfigChanged,
                    protocol: SsoProtocolNames.Saml2,
                    storeId: storeId,
                    notes: $"IdP metadata applied automatically by the daily SSO check: {differences}.");

                // Compare again, so the stored status says what is true now.
                settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(storeId);
                differences = DescribeDifferences(await RunHandshakeAsync(settings));
            }
            catch (Exception ex)
            {
                differences = $"{differences}; automatic apply failed ({ex.Message})";
            }
        }

        await SaveMetadataStatusAsync(settings, storeId, differences);
        return differences;
    }

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

        // write a forced HandshakeRun audit row on every click. Forced=true so the
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
}
