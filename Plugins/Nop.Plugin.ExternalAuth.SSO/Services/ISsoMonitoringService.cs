namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// What needs an admin's attention in the stores' SSO set-up: certificates and the OIDC client
/// secret that have expired or expire soon, and IdP metadata that differs from the stored values.
/// </summary>
/// <remarks>
/// Read by System → Warnings (<c>SsoSystemWarningsConsumer</c>, built live on every page load) and by
/// the daily "SSO certificate and metadata check" task, which writes an audit row for each one.
/// Only stores with SSO enabled are looked at.
/// </remarks>
public interface ISsoMonitoringService
{
    /// <summary>
    /// The current findings, most serious first.
    /// </summary>
    Task<IList<SsoMonitoringFinding>> GetFindingsAsync();
}

/// <summary>
/// One thing an admin should act on.
/// </summary>
/// <param name="StoreId">Store whose SSO set-up it concerns</param>
/// <param name="IsFailure">True when sign-in is already affected (something has expired or cannot be read)</param>
/// <param name="Text">Localized text for the admin</param>
public record SsoMonitoringFinding(int StoreId, bool IsFailure, string Text);
