namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Per-IP rate limiter for SSO login challenges (OIDC + SAML entry points).
/// </summary>
/// <remarks>
/// Counter is stored in <c>IStaticCacheManager</c> keyed by client IP. TTL is a sliding
/// 1-hour window. <see cref="SsoSystemSettings.LoginAttemptCapPerHour"/> drives the cap;
/// <c>0</c> disables the check entirely.
///
/// This is a defence against accidental login-redirect loops and casual brute-force
/// enumeration of valid email accounts via SSO. It is not a substitute for IdP-side rate
/// limiting (Entra has its own controls) or network-edge WAF rules.
/// </remarks>
public interface ISsoRateLimiter
{
    /// <summary>Counter for the login entry point (<c>ExternalAuthentication/Login</c>).</summary>
    public const string LoginBucket = "login";

    /// <summary>Counter for the SAML assertion consumer (<c>POST /saml/acs</c>).</summary>
    public const string AcsBucket = "acs";

    /// <summary>
    /// Increment the current client IP's counter for <paramref name="bucket"/> and return
    /// <c>true</c> if the new value is still within the cap (or the cap is disabled). Returns
    /// <c>false</c> when the IP is over the cap. Each bucket has its own counter under the same cap.
    /// </summary>
    /// <param name="bucket">Which endpoint is counted (<see cref="LoginBucket"/>, <see cref="AcsBucket"/>)</param>
    Task<bool> CheckAndIncrementAsync(string bucket = LoginBucket);
}
