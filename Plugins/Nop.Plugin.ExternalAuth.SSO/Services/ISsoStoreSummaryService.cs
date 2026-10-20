using Nop.Plugin.ExternalAuth.SSO.Settings;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Aggregates per-store SSO configuration + runtime telemetry for the SSO Accounts admin
/// list page.
/// </summary>
public interface ISsoStoreSummaryService
{
    /// <summary>
    /// Build a summary row for every store whose <see cref="SsoConfigurationSettings"/>
    /// has the plugin enabled (Protocol != Off OR IsEnabled). Rows are unordered;
    /// the caller sorts by DisplayOrder + StoreName.
    /// </summary>
    Task<IList<SsoStoreSummary>> ListAsync();

    /// <summary>
    /// Returns the list of stores that do NOT yet have an enabled SSO configuration —
    /// used to populate the Add New store-picker dropdown.
    /// </summary>
    Task<IList<(int StoreId, string StoreName)>> ListUnconfiguredAsync();
}

/// <summary>
/// Plain DTO; transport between <see cref="ISsoStoreSummaryService"/> and the factory.
/// </summary>
public class SsoStoreSummary
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public SsoProtocol Protocol { get; set; }
    public bool IsEnabled { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime? DateSetupUtc { get; set; }
    public int NumberOfUsers { get; set; }
    public DateTime? LastSuccessfulLoginUtc { get; set; }
    public DateTime? IdpCertificateNotAfter { get; set; }
}
