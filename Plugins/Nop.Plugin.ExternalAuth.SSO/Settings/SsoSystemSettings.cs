using Nop.Core.Configuration;
using Nop.Core.Domain.Customers;

namespace Nop.Plugin.ExternalAuth.SSO.Settings;

/// <summary>
/// System-wide settings for the ExternalAuth.SSO plugin.
/// </summary>
/// <remarks>
/// Stored at <c>storeId = 0</c> (never overridden per-store). Covers the local-login bypass
/// mechanism that lets ops recover access when SSO is enabled but the IdP is unavailable.
/// See References/SSO_Plugin_Reference.md §4.2.
/// </remarks>
public class SsoSystemSettings : ISettings
{
    /// <summary>
    /// Token that, when supplied via <c>?ssobypass=&lt;token&gt;</c> on <c>/login</c>, allows access
    /// to the local login form even on stores where SSO is on. Stored as ciphertext.
    /// Empty disables the bypass entirely.
    /// </summary>
    public string SsoBypassToken { get; set; } = string.Empty;

    /// <summary>
    /// CSV of customer-role <c>SystemName</c>s allowed to complete a local login that arrived
    /// through the bypass URL. A customer not in any of these roles is signed out and
    /// <c>BypassDenied</c>-audited after a successful credential submission.
    /// </summary>
    public string LocalLoginAllowedRoles { get; set; } = NopCustomerDefaults.AdministratorsRoleName;

    /// <summary>
    /// Maximum number of SSO login challenges allowed per client IP per sliding hour.
    /// Set to <c>0</c> to disable the cap entirely. Default <c>60</c> — generous for normal
    /// users, tight enough to slow scripted enumeration. Hit attempts produce a
    /// <c>Failed</c> audit row with <c>login_retry_cap_hit</c>.
    /// </summary>
    public int LoginAttemptCapPerHour { get; set; } = 60;

    /// <summary>
    /// Master switch for the SSO audit log. When <c>false</c>, no new events are written
    /// to <c>SSO_AuthEvent</c> for any store — SAML/OIDC login flows and admin-initiated
    /// certificate changes both stop producing rows. Existing rows remain visible on the
    /// audit-log admin page until cleared. Clearing the log always records a single
    /// <c>LogCleared</c> breadcrumb row regardless of this setting.
    /// </summary>
    public bool EnableAuditLogging { get; set; } = true;

    /// <summary>
    /// Audit rows older than this many days are deleted by the daily "SSO audit log retention"
    /// scheduled task. 0 = keep forever.
    /// </summary>
    public int AuditRetentionDays { get; set; } = 365;

    /// <summary>
    /// How many days before an IdP certificate, SP certificate or OIDC client secret expires that
    /// System → Warnings starts warning about it. An expired one is always reported. Default 30.
    /// </summary>
    public int CertificateWarningDays { get; set; } = 30;
}
