namespace Nop.Plugin.ExternalAuth.SSO.Constants;

/// <summary>
/// Values written to <see cref="Domain.SsoAuthEvent.EventType"/>.
/// </summary>
public static class SsoAuthEventTypes
{
    /// <summary>SSO challenge initiated — written before redirecting to the IdP.</summary>
    public const string Login = "Login";

    /// <summary>A new nopCommerce customer was created from the IdP assertion.</summary>
    public const string AutoRegistered = "AutoRegistered";

    /// <summary>An existing nopCommerce customer was linked to the IdP subject.</summary>
    public const string Linked = "Linked";

    /// <summary>A customer already linked to the IdP subject signed in.</summary>
    public const string SignedIn = "SignedIn";

    /// <summary>Single Log-Out completed.</summary>
    public const string Slo = "Slo";

    /// <summary>An error occurred during the flow (signature, allowlist, replay, etc.).</summary>
    public const string Failed = "Failed";

    /// <summary>
    /// Local-login bypass cookie was set but the resulting customer was not in
    /// <c>SsoSystemSettings.LocalLoginAllowedRoles</c>.
    /// </summary>
    public const string BypassDenied = "BypassDenied";

    /// <summary>
    /// Admin saved a Configure-page change. <c>Notes</c> records the admin's email and
    /// the store scope. Useful as a tamper-evidence trail for configuration drift.
    /// </summary>
    public const string ConfigChanged = "ConfigChanged";

    /// <summary>
    /// Single breadcrumb row written by <see cref="Services.ISsoEventService.ClearAllAsync"/>
    /// before deleting the rest of the audit log. Survives the clear so there's always a
    /// "what happened to the history" record.
    /// </summary>
    public const string LogCleared = "LogCleared";

    /// <summary>
    /// An admin clicked "Test SSO Connection" on the Configure or SSO Accounts page and a
    /// real IdP-metadata handshake was attempted. Written through <c>LogAsync(forced: true)</c>
    /// so the breadcrumb is retained even when audit logging is disabled. Notes carries the
    /// per-check outcome summary (EntityID / SSO URL / SLO URL / Certificate match flags).
    /// </summary>
    public const string HandshakeRun = "HandshakeRun";

    /// <summary>
    /// The daily "SSO certificate and metadata check" found something to act on (a certificate or
    /// client secret that expires soon or has expired, IdP metadata that differs). One row per finding
    /// per run; Notes carries the same text as System → Warnings.
    /// </summary>
    public const string ScheduledCheck = "ScheduledCheck";
}

/// <summary>
/// Values written to <see cref="Domain.SsoAuthEvent.Protocol"/>.
/// </summary>
public static class SsoProtocolNames
{
    /// <summary>Authentication via OpenID Connect.</summary>
    public const string Oidc = "Oidc";

    /// <summary>Authentication via SAML 2.0.</summary>
    public const string Saml2 = "Saml2";
}
