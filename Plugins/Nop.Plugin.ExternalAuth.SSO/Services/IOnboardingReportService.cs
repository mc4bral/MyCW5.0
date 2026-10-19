namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Generates a plain-text IdP-onboarding document. Admins paste it into an email
/// to the customer's IT team — it contains the SP-side values to paste into Entra /
/// ADFS / Okta plus labelled blanks for the IdP-side values to send back.
/// </summary>
/// <remarks>
/// Ported from the legacy <c>SSOOnboardingReportService</c> with branching for both OIDC
/// and SAML so the right "paste here" values are surfaced for whichever protocol the
/// store is configured for.
/// </remarks>
public interface IOnboardingReportService
{
    /// <summary>
    /// Build the onboarding report for the given store scope (<c>0</c> = global default).
    /// <paramref name="requestScheme"/> + <paramref name="requestHost"/> are used as the
    /// fallback when <c>SsoConfigurationSettings.BaseUrl</c> is blank.
    /// </summary>
    Task<string> BuildReportAsync(int storeScope, string requestScheme, string requestHost);
}
