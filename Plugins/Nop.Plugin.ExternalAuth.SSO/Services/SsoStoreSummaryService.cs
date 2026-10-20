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
/// <remarks>
/// The per-store figures (setup date, users, last successful login) come from the audit log —
/// nop's <c>ExternalAuthenticationRecord</c> has no store column, so it cannot say which store a
/// customer signs in through. They are aggregated in SQL, one query per figure for all stores, so
/// the page does not grow slower as the log grows. Clearing the audit log resets them.
/// </remarks>
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
        SsoAuthEventTypes.Linked,
        SsoAuthEventTypes.SignedIn
    };

    #region Fields

    protected readonly IRepository<SsoAuthEvent> _eventRepository;
    protected readonly ISettingService _settingService;
    protected readonly ISsoSecretsService _secretsService;
    protected readonly IStoreService _storeService;

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

    #region Utilities

    private sealed record StoreTelemetry(DateTime? FirstEventUtc, DateTime? LastSuccessUtc, int Users);

    /// <summary>
    /// Setup date, last successful sign-in and distinct signed-in customers for the given stores,
    /// computed by the database.
    /// </summary>
    private async Task<IDictionary<int, StoreTelemetry>> GetTelemetryAsync(IList<int> storeIds)
    {
        if (storeIds.Count == 0)
            return new Dictionary<int, StoreTelemetry>();

        var events = _eventRepository.Table.Where(e => storeIds.Contains(e.StoreId));
        var successes = events.Where(e => SuccessEventTypes.Contains(e.EventType));

        var firstEvents = await events
            .GroupBy(e => e.StoreId)
            .Select(g => new { StoreId = g.Key, First = g.Min(e => e.OccurredOnUtc) })
            .ToListAsync();

        var lastSuccesses = await successes
            .GroupBy(e => e.StoreId)
            .Select(g => new { StoreId = g.Key, Last = g.Max(e => e.OccurredOnUtc) })
            .ToListAsync();

        var users = await successes
            .Where(e => e.CustomerId != null)
            .Select(e => new { e.StoreId, e.CustomerId })
            .Distinct()
            .GroupBy(e => e.StoreId)
            .Select(g => new { StoreId = g.Key, Count = g.Count() })
            .ToListAsync();

        return storeIds.ToDictionary(
            id => id,
            id => new StoreTelemetry(
                firstEvents.FirstOrDefault(x => x.StoreId == id)?.First,
                lastSuccesses.FirstOrDefault(x => x.StoreId == id)?.Last,
                users.FirstOrDefault(x => x.StoreId == id)?.Count ?? 0));
    }

    private DateTime? TryGetCertNotAfter(SsoConfigurationSettings settings)
    {
        if (settings.Protocol != SsoProtocol.Saml2
            || !SsoCertificates.TryLoadIdpCertificate(_secretsService, settings, out var certificate, out _))
            return null;

        using (certificate)
            return certificate!.NotAfter.ToUniversalTime();
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<IList<SsoStoreSummary>> ListAsync()
    {
        var configured = new List<(int StoreId, string StoreName, SsoConfigurationSettings Settings)>();
        foreach (var store in await _storeService.GetAllStoresAsync())
        {
            var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);
            if (settings.Protocol != SsoProtocol.Off || settings.IsEnabled)
                configured.Add((store.Id, store.Name, settings));
        }

        var telemetry = await GetTelemetryAsync(configured.Select(c => c.StoreId).ToList());

        return configured.Select(c => new SsoStoreSummary
        {
            StoreId = c.StoreId,
            StoreName = c.StoreName,
            Protocol = c.Settings.Protocol,
            IsEnabled = c.Settings.IsEnabled,
            DisplayOrder = c.Settings.DisplayOrder,
            IdpCertificateNotAfter = TryGetCertNotAfter(c.Settings),
            DateSetupUtc = telemetry[c.StoreId].FirstEventUtc,
            LastSuccessfulLoginUtc = telemetry[c.StoreId].LastSuccessUtc,
            NumberOfUsers = telemetry[c.StoreId].Users
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<IList<(int StoreId, string StoreName)>> ListUnconfiguredAsync()
    {
        var unconfigured = new List<(int, string)>();
        foreach (var store in await _storeService.GetAllStoresAsync())
        {
            var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);
            if (settings.Protocol == SsoProtocol.Off && !settings.IsEnabled)
                unconfigured.Add((store.Id, store.Name));
        }

        return unconfigured;
    }

    #endregion
}
