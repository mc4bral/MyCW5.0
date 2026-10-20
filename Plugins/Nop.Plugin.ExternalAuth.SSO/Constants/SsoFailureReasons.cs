namespace Nop.Plugin.ExternalAuth.SSO.Constants;

/// <summary>
/// Stable string codes for failure causes on <c>SSO_AuthEvent</c> rows where
/// <c>EventType = Failed</c>.
/// </summary>
/// <remarks>
/// Persisted as a <c>"[reason=&lt;code&gt;] "</c> prefix on the <c>Notes</c> column so
/// administrators can grep / filter the audit log by cause without re-parsing free-text
/// English. A future change may promote this to a dedicated column; the codes are
/// intended to remain stable across that change so existing rows stay searchable by the
/// same identifiers.
///
/// Ported from the legacy <c>Nop.Plugin.Auth.SSO</c> plugin per the May 2026 comparison
/// pass. Add a new code here whenever a new failure branch is introduced. Do not rename
/// existing codes without a migration plan for old rows.
///
/// Use via <see cref="Format"/> so the prefix shape stays consistent.
/// </remarks>
public static class SsoFailureReasons
{
    /// <summary>Plugin enabled flag is off for the target store.</summary>
    public const string SsoDisabled = "sso_disabled";

    /// <summary>IdP Entity ID / SSO URL / OIDC Authority was empty after resolving defaults.</summary>
    public const string IdpConfigIncomplete = "idp_config_incomplete";

    /// <summary>Hard-cap on login attempts in a short window was hit (rate limiter).</summary>
    public const string LoginRetryCapHit = "login_retry_cap_hit";

    /// <summary>Unhandled exception while building the challenge.</summary>
    public const string LoginInternalError = "login_internal_error";

    /// <summary>ACS POST arrived without a SAMLResponse field.</summary>
    public const string AcsEmptyResponse = "acs_empty_response";

    /// <summary>
    /// The plugin-owned correlation cookie was missing, expired, malformed, or its
    /// stored request id did not match the inbound <c>InResponseTo</c>.
    /// </summary>
    public const string AcsPendingCookieInvalid = "acs_pending_cookie_invalid";

    /// <summary>The IdP response had no email claim under any configured name.</summary>
    public const string AcsNoEmailClaim = "acs_no_email_claim";

    /// <summary>Email's domain is not in <c>AllowedEmailDomains</c>.</summary>
    public const string AcsEmailDomainBlocked = "acs_email_domain_blocked";

    /// <summary><c>IExternalAuthenticationService.AuthenticateAsync</c> rejected the request.</summary>
    public const string AcsCustomerLoginFailed = "acs_customer_login_failed";

    /// <summary>
    /// IdP-asserted email matched an existing privileged (admin) customer account;
    /// the link was refused as defence-in-depth.
    /// </summary>
    public const string AcsAdminAccountBlocked = "acs_admin_account_blocked";

    /// <summary>
    /// The sign-in succeeded but the IdP sent a group-overage marker instead of the group list,
    /// so group → role sync was skipped and the customer's roles were left unchanged. Recorded in
    /// the success row's notes, not as a failure.
    /// </summary>
    public const string GroupsOverage = "groups_overage";

    /// <summary>The OIDC callback could not authenticate against the handler (no principal or a handler failure).</summary>
    public const string OidcCallbackFailed = "oidc_callback_failed";

    /// <summary>The DEBUG mock IdP's "failure" scenario.</summary>
    public const string MockFailure = "mock_failure";

    /// <summary>No existing customer matched the email and <c>AutoRegister</c> is off.</summary>
    public const string AcsAutoRegisterDisabled = "acs_auto_register_disabled";

    /// <summary>Auto-registration returned errors.</summary>
    public const string AcsAutoRegisterFailed = "acs_auto_register_failed";

    /// <summary>
    /// Cryptographic / temporal validation failed — signature, digest method, NotBefore /
    /// NotOnOrAfter, audience, issuer, or replay. ITfoxtec raised the underlying exception.
    /// </summary>
    public const string AcsSecurityValidationFailed = "acs_security_validation_failed";

    /// <summary>Unhandled exception while processing the ACS POST.</summary>
    public const string AcsUnexpectedError = "acs_unexpected_error";

    /// <summary>
    /// Bypass cookie was present but the resulting customer was not in any of the
    /// roles listed in <c>LocalLoginAllowedRoles</c> (LocalLoginEnforcementFilter).
    /// </summary>
    public const string BypassNotInRole = "bypass_not_in_role";

    /// <summary>SAML SP-initiated LogoutRequest construction/binding threw.</summary>
    public const string SloRequestFailed = "slo_request_failed";

    /// <summary>SAML LogoutResponse validation failed (signature, InResponseTo, etc.).</summary>
    public const string SloResponseFailed = "slo_response_failed";

    /// <summary>
    /// Returns the canonical Notes prefix for <paramref name="code"/> plus
    /// <paramref name="detail"/>. Example: <c>"[reason=acs_email_domain_blocked] domain
    /// not in allowlist 'example.com'"</c>.
    /// </summary>
    public static string Format(string code, string? detail = null)
        => string.IsNullOrEmpty(detail) ? $"[reason={code}]" : $"[reason={code}] {detail}";
}
