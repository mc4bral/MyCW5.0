using Microsoft.AspNetCore.Http;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Cookie options for the SAML correlation cookies (login and logout).
/// </summary>
/// <remarks>
/// The IdP returns to the store with a <b>cross-site</b> request — a POST for the login response,
/// a POST or a redirect for the logout response. Browsers do not send <c>SameSite=Lax</c> cookies
/// on a cross-site POST, so over HTTPS the cookies are <c>SameSite=None; Secure</c>, as ASP.NET
/// Core's own OIDC correlation cookie is. Browsers reject <c>None</c> without <c>Secure</c>, so over
/// plain HTTP (local development only) they fall back to <c>Lax</c> and the correlation check
/// cannot work; SAML needs HTTPS end to end.
/// </remarks>
public static class SamlCookieOptions
{
    /// <summary>
    /// Options for writing or deleting a correlation cookie.
    /// </summary>
    /// <param name="request">The current request; decides Secure / SameSite</param>
    /// <param name="lifetime">Lifetime when writing; null when deleting</param>
    public static CookieOptions For(HttpRequest request, TimeSpan? lifetime = null)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = request.IsHttps,
            SameSite = request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
            IsEssential = true
        };

        if (lifetime.HasValue)
            options.Expires = DateTimeOffset.UtcNow.Add(lifetime.Value);

        return options;
    }
}
