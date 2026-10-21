using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Stores;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Default implementation of <see cref="ISsoMonitoringService"/>.
/// </summary>
/// <remarks>
/// Reads only stored values — certificates, the client-secret expiry date the admin typed, and the
/// result of the last metadata comparison. It never calls the IdP, so System → Warnings stays fast;
/// the daily task and Test connection do the network work.
/// </remarks>
public class SsoMonitoringService : ISsoMonitoringService
{
    #region Fields

    protected readonly ILocalizationService _localizationService;
    protected readonly ISettingService _settingService;
    protected readonly ISsoSecretsService _secretsService;
    protected readonly IStoreService _storeService;

    #endregion

    #region Ctor

    public SsoMonitoringService(
        ILocalizationService localizationService,
        ISettingService settingService,
        ISsoSecretsService secretsService,
        IStoreService storeService)
    {
        _localizationService = localizationService;
        _settingService = settingService;
        _secretsService = secretsService;
        _storeService = storeService;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// How an expiry date stands against the warning window.
    /// </summary>
    internal enum ExpiryState
    {
        Ok,
        Expiring,
        Expired
    }

    /// <summary>
    /// Whether <paramref name="expiresUtc"/> has passed, falls within <paramref name="warningDays"/>
    /// of <paramref name="nowUtc"/>, or is further away; and the whole days left (0 once expired).
    /// </summary>
    internal static (ExpiryState state, int daysLeft) GetExpiryState(DateTime expiresUtc, DateTime nowUtc, int warningDays)
    {
        if (expiresUtc <= nowUtc)
            return (ExpiryState.Expired, 0);

        var daysLeft = (int)Math.Floor((expiresUtc - nowUtc).TotalDays);
        return daysLeft < warningDays ? (ExpiryState.Expiring, daysLeft) : (ExpiryState.Ok, daysLeft);
    }

    /// <param name="shownDate">Date shown to the admin; defaults to <paramref name="expiresUtc"/>'s date</param>
    /// <summary>
    /// Whether these store settings lock every admin out of the store's own login (D10): <c>/login</c>
    /// always goes to the IdP (SSO on, a protocol, "Redirect login to SSO"), the bypass link is off
    /// (no bypass token) and SSO never signs in an admin account. Used by the Configure validator to
    /// refuse such a save and here to report a store already in that state.
    /// </summary>
    internal static bool LocksAdminsOut(bool isEnabled, SsoProtocol protocol, bool redirectLoginToSso, bool hasBypassToken)
        => isEnabled && protocol != SsoProtocol.Off && redirectLoginToSso && !hasBypassToken;

    private async Task AddExpiryFindingAsync(IList<SsoMonitoringFinding> findings, int storeId, string storeName,
        DateTime expiresUtc, int warningDays, string expiredKey, string expiringKey, DateTime? shownDate = null)
    {
        var (state, daysLeft) = GetExpiryState(expiresUtc, DateTime.UtcNow, warningDays);
        var date = (shownDate ?? expiresUtc).ToString("yyyy-MM-dd");

        if (state == ExpiryState.Expired)
            findings.Add(new(storeId, true, string.Format(await _localizationService.GetResourceAsync(expiredKey), storeName, date)));
        else if (state == ExpiryState.Expiring)
            findings.Add(new(storeId, false, string.Format(await _localizationService.GetResourceAsync(expiringKey), storeName, date, daysLeft)));
    }

    private async Task AddSamlFindingsAsync(IList<SsoMonitoringFinding> findings, int storeId, string storeName,
        SsoConfigurationSettings settings, int warningDays)
    {
        // IdP signing certificate — always needed for SAML.
        if (SsoCertificates.TryLoadIdpCertificate(_secretsService, settings, out var idpCert, out var idpError))
        {
            using (idpCert)
                await AddExpiryFindingAsync(findings, storeId, storeName, idpCert!.NotAfter.ToUniversalTime(), warningDays,
                    "Plugins.ExternalAuth.SSO.Warnings.IdpCertificateExpired", "Plugins.ExternalAuth.SSO.Warnings.IdpCertificateExpiring");
        }
        else if (!string.IsNullOrWhiteSpace(settings.IdpCertificateBase64))
        {
            findings.Add(new(storeId, true, string.Format(
                await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Warnings.IdpCertificateUnreadable"), storeName, idpError)));
        }

        // SP certificate — only when something uses it.
        var spPfxUsed = settings.SignedRequest || settings.SignLogoutRequest || settings.EncryptedAssertion;
        if (spPfxUsed && !string.IsNullOrWhiteSpace(settings.SpPfxBase64))
        {
            try
            {
                using var spCert = SsoCertificates.LoadSpPfx(_secretsService, settings);
                await AddExpiryFindingAsync(findings, storeId, storeName, spCert.NotAfter.ToUniversalTime(), warningDays,
                    "Plugins.ExternalAuth.SSO.Warnings.SpCertificateExpired", "Plugins.ExternalAuth.SSO.Warnings.SpCertificateExpiring");
            }
            catch (Exception ex)
            {
                findings.Add(new(storeId, true, string.Format(
                    await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Warnings.SpCertificateUnreadable"), storeName, ex.Message)));
            }
        }

        // Last metadata comparison (daily task or Test connection).
        if (!string.IsNullOrEmpty(settings.IdpMetadataDifferences))
        {
            var checkedOn = settings.IdpMetadataCheckedOnUtc?.ToString("yyyy-MM-dd HH:mm") + " UTC";
            findings.Add(new(storeId, false, string.Format(
                await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Warnings.MetadataDiffers"), storeName, settings.IdpMetadataDifferences, checkedOn)));
        }
    }

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<IList<SsoMonitoringFinding>> GetFindingsAsync()
    {
        var findings = new List<SsoMonitoringFinding>();
        var systemWide = await _settingService.LoadSettingAsync<SsoSystemSettings>(storeId: 0);
        var warningDays = systemWide.CertificateWarningDays;
        var hasBypassToken = !string.IsNullOrEmpty(systemWide.SsoBypassToken);

        foreach (var store in await _storeService.GetAllStoresAsync())
        {
            var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(store.Id);
            if (!settings.IsEnabled || settings.Protocol == SsoProtocol.Off)
                continue;

            if (LocksAdminsOut(settings.IsEnabled, settings.Protocol, settings.RedirectLoginToSso, hasBypassToken))
                findings.Add(new(store.Id, true, string.Format(
                    await _localizationService.GetResourceAsync("Plugins.ExternalAuth.SSO.Warnings.AdminLockout"), store.Name)));

            if (settings.Protocol == SsoProtocol.Saml2)
                await AddSamlFindingsAsync(findings, store.Id, store.Name, settings, warningDays);
            else if (settings.Protocol == SsoProtocol.OpenIdConnect && settings.ClientSecretExpiresOnUtc.HasValue)
            {
                // A typed date: the secret is good through the end of that day.
                var expiresOn = settings.ClientSecretExpiresOnUtc.Value.Date;
                await AddExpiryFindingAsync(findings, store.Id, store.Name,
                    DateTime.SpecifyKind(expiresOn.AddDays(1), DateTimeKind.Utc), warningDays,
                    "Plugins.ExternalAuth.SSO.Warnings.ClientSecretExpired", "Plugins.ExternalAuth.SSO.Warnings.ClientSecretExpiring",
                    shownDate: expiresOn);
            }
        }

        return findings.OrderByDescending(f => f.IsFailure).ThenBy(f => f.StoreId).ToList();
    }

    #endregion
}
