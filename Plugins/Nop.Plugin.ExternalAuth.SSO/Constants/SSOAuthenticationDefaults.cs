namespace Nop.Plugin.ExternalAuth.SSO.Constants;

/// <summary>
/// Plugin-wide string constants.
/// </summary>
public static class SSOAuthenticationDefaults
{
    /// <summary>
    /// Plugin <c>SystemName</c> — must match <c>plugin.json</c>.
    /// </summary>
    public const string SystemName = "ExternalAuth.SSO";

    /// <summary>
    /// ASP.NET Core authentication scheme name for the OIDC handler registered by the plugin.
    /// </summary>
    public const string OidcAuthenticationScheme = "ExternalAuthSSO-Oidc";

    /// <summary>
    /// <see cref="AuthenticationProperties"/> item key used to pass an error-callback URL
    /// through the OIDC challenge so <c>OnRemoteFailure</c> can redirect there.
    /// </summary>
    public const string ErrorCallback = "ExternalAuthSSO.ErrorCallback";

    /// <summary>
    /// Name of the short-lived cookie set by <c>LoginActionsFilter</c> after a valid
    /// <c>?ssobypass=&lt;token&gt;</c> arrives. Its presence tells the front-door filter to
    /// let subsequent <c>/Customer/Login</c> GETs through to the standard local form, and
    /// tells <c>LocalLoginEnforcementFilter</c> to enforce the role allowlist on the POST.
    /// </summary>
    public const string BypassCookieName = ".AspNetCore.ExternalAuthSSO.BypassUsed";

    /// <summary>
    /// Query-string key the bypass token arrives under: <c>/login?ssobypass=&lt;token&gt;</c>.
    /// </summary>
    public const string BypassQueryStringKey = "ssobypass";

    /// <summary>
    /// How long the bypass cookie lives after being set. Short enough that a stolen cookie
    /// has minimal blast radius, long enough that the admin has time to type credentials
    /// and complete the login.
    /// </summary>
    public static readonly TimeSpan BypassCookieLifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Name of the short-lived cookie that correlates a SAML <c>AuthnRequest</c> ID across
    /// the browser round-trip. Used in place of <c>HttpContext.Session</c> so multi-tab
    /// logins don't collide (fixes legacy P2-7).
    /// </summary>
    public const string SamlCorrelationCookieName = ".AspNetCore.ExternalAuthSSO.SamlCorrelation";

    /// <summary>
    /// How long the SAML correlation cookie lives. Same rationale as the bypass cookie —
    /// long enough for the IdP round-trip, short enough to limit replay surface.
    /// </summary>
    public static readonly TimeSpan SamlCorrelationCookieLifetime = TimeSpan.FromMinutes(10);

    /// <summary>SAML Assertion Consumer Service path on this store.</summary>
    public const string SamlAcsPath = "/saml/acs";

    /// <summary>SAML SP metadata path on this store.</summary>
    public const string SamlMetadataPath = "/saml/metadata";

    /// <summary>SAML logout path on this store (handles both SP-initiated and IdP-initiated SLO).</summary>
    public const string SamlLogoutPath = "/saml/logout";

    /// <summary>OIDC handler post-sign-out callback path. Configured on <c>OpenIdConnectOptions.SignedOutCallbackPath</c>.</summary>
    public const string OidcSignedOutCallbackPath = "/signout-callback-microsoft";

    /// <summary>Name of the SAML SLO correlation cookie for SP-initiated InResponseTo validation.</summary>
    public const string SamlLogoutCorrelationCookieName = ".AspNetCore.ExternalAuthSSO.SamlLogoutCorrelation";
}
