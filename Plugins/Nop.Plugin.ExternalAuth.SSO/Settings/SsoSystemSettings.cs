using Nop.Core.Configuration;

namespace Nop.Plugin.ExternalAuth.SSO.Settings;

/// <summary>
/// System-wide settings for the ExternalAuth.SSO plugin.
/// </summary>
/// <remarks>
/// Stored at <c>storeId = 0</c> (never overridden per-store). Covers the local-login bypass
/// mechanism that lets ops recover access when SSO is enabled but the IdP is unavailable.
/// See SSOPlan.v2.md §3.7.
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
    public string LocalLoginAllowedRoles { get; set; } = "Administrators";

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
}
