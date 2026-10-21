using Microsoft.AspNetCore.Mvc;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Redirects the browser to a SAML HTTP-Redirect binding URL exactly as ITfoxtec built it.
/// </summary>
/// <remarks>
/// A plain <see cref="RedirectResult"/> goes through nop's <c>NopRedirectResultExecutor</c>, which —
/// with <c>SecuritySettings.AllowNonAsciiCharactersInHeaders</c> on — URL-decodes the whole address
/// before sending it. That turns the escaped Base64 of <c>SAMLRequest</c> back into raw <c>+</c>,
/// <c>/</c> and <c>=</c>; the IdP then reads every <c>+</c> as a space and rejects the request
/// (Keycloak: <c>invalid_token</c>). Found against Keycloak on 2026-10-21. This result sets the
/// <c>Location</c> header itself, so nop's executor never sees it. The URL is always an absolute
/// IdP address from the stored configuration, never user input.
/// </remarks>
public class SamlRedirectResult : IActionResult
{
    /// <summary>
    /// The binding's redirect address, already escaped.
    /// </summary>
    public string Url { get; }

    /// <param name="url">The binding's redirect address, already escaped (<c>Saml2RedirectBinding.RedirectLocation</c>)</param>
    public SamlRedirectResult(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        Url = url.OriginalString;
    }

    /// <inheritdoc />
    public Task ExecuteResultAsync(ActionContext context)
    {
        context.HttpContext.Response.Redirect(Url);
        return Task.CompletedTask;
    }
}
