using System.Security.Cryptography.X509Certificates;
using Nop.Data;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Domain;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Configuration;
using Nop.Services.Stores;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Default implementation of <see cref="ISsoStoreSummaryService"/>.
/// </summary>
public class SsoStoreSummaryService : ISsoStoreSummaryService
{
    /// <summary>
    /// Audit event types that count as "successful SSO sign-in" for the user-count and
    /// last-login columns. Login is excluded — it's written pre-redirect so it doesn't
    /// guarantee the customer actually completed the round trip.
    /// </summary>
    private static readonly string[] SuccessEventTypes =
    {
        SsoAuthEventTypes.AutoRegistered,
        SsoAuthEventTypes.Linked
    };

    #region Fields

    private readonly IRepository<SsoAuthEvent> _eventRepository;
    private readonly ISettingService _settingService;
    private readonly ISsoSecretsService _secretsService;
    private readonly IStoreService _storeService;

    #endregion

    #region Ctor

    public SsoStoreSummaryService(
        IRepository<SsoAuthEvent> eventRepository,
        ISettingService settingService,
        ISsoSecretsService secretsService,
        IStoreService storeService)
    {
        _eventRepository = eventRepository;
        _settingService = settingService;
        _secretsService = secretsService;
        _storeService = storeService;
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<IList<SsoStoreSummary>> ListAsync()
    {
        var allStores = await _storeService.GetAllStoresAsync();
        var result = new List<SsoStoreSummary>();

        foreach (var store in allStores)
        {
            var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);
            if (settings.Protocol == SsoProtocol.Off && !settings.IsEnabled)
                continue;

            var summary = new SsoStoreSummary
            {
                StoreId = store.Id,
                StoreName = store.Name,
                Protocol = settings.Protocol,
                IsEnabled = settings.IsEnabled,
                DisplayOrder = settings.DisplayOrder,
                IdpCertificateNotAfter = TryGetCertNotAfter(settings)
            };

            // Single query per store to fold the user-count + last-login + setup date.
            await PopulateTelemetryAsync(summary, store.Id);
            result.Add(summary);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IList<(int StoreId, string StoreName)>> ListUnconfiguredAsync()
    {
        var allStores = await _storeService.GetAllStoresAsync();
        var unconfigured = new List<(int, string)>();

        foreach (var store in allStores)
        {
            var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);
            if (settings.Protocol == SsoProtocol.Off && !settings.IsEnabled)
                unconfigured.Add((store.Id, store.Name));
        }

        return unconfigured;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Populates the three audit-driven columns. Falls back to nulls when the store has
    /// no audit history (newly configured tenant before its first login).
    /// </summary>
    private async Task PopulateTelemetryAsync(SsoStoreSummary summary, int storeId)
    {
        var events = await _eventRepository.GetAllAsync(q => q.Where(e => e.StoreId == storeId));

        if (events.Count == 0)
            return;

        summary.DateSetupUtc = events.Min(e => e.OccurredOnUtc);

        var successes = events
            .Where(e => SuccessEventTypes.Contains(e.EventType))
            .ToList();

        if (successes.Count > 0)
        {
            summary.LastSuccessfulLoginUtc = successes.Max(e => e.OccurredOnUtc);
            summary.NumberOfUsers = successes
                .Where(e => e.CustomerId.HasValue)
                .Select(e => e.CustomerId!.Value)
                .Distinct()
                .Count();
        }
    }

    /// <summary>
    /// Parses the SAML IdP certificate and returns its NotAfter. Null when not SAML, when
    /// no cert is configured, or when the stored ciphertext fails to decrypt — admins will
    /// see "(unknown)" in the column rather than a thrown exception breaking the whole grid.
    /// </summary>
    private DateTime? TryGetCertNotAfter(SsoConfigurationSettings settings)
    {
        if (settings.Protocol != SsoProtocol.Saml2)
            return null;

        if (string.IsNullOrWhiteSpace(settings.IdpCertificateBase64))
            return null;

        if (!_secretsService.TryUnprotect(settings.IdpCertificateBase64, out var plaintext)
            || string.IsNullOrWhiteSpace(plaintext))
            return null;

        try
        {
            var stripped = new string(plaintext
                .Replace("-----BEGIN CERTIFICATE-----", string.Empty)
                .Replace("-----END CERTIFICATE-----", string.Empty)
                .Where(c => !char.IsWhiteSpace(c))
                .ToArray());

            var bytes = Convert.FromBase64String(stripped);
            var cert = X509CertificateLoader.LoadCertificate(bytes);
            return cert.NotAfter.ToUniversalTime();
        }
        catch
        {
            return null;
        }
    }

    #endregion
}
