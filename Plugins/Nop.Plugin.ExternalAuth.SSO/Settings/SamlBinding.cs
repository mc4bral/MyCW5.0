using Nop.Plugin.ExternalAuth.SSO.Models;

namespace Nop.Plugin.ExternalAuth.SSO.Settings;

/// <summary>
/// SAML 2.0 binding the store uses to send a message to the IdP.
/// </summary>
/// <remarks>
/// Stored by name (<c>HttpRedirect</c> / <c>HttpPost</c>). Installs from before 2026-10-19 stored the
/// display form (<c>HTTP-Redirect</c>); the plugin update converts those rows.
/// </remarks>
public enum SamlBinding
{
    /// <summary>HTTP-Redirect: the message travels in the query string (the usual choice for Entra).</summary>
    HttpRedirect = 0,

    /// <summary>HTTP-POST: the message travels in an auto-submitted form.</summary>
    HttpPost = 1
}

/// <summary>
/// Conversions for <see cref="SamlBinding"/>.
/// </summary>
public static class SamlBindingExtensions
{
    /// <summary>
    /// The binding's URN, as it appears in SAML metadata.
    /// </summary>
    /// <param name="binding">Binding</param>
    public static string ToUrn(this SamlBinding binding)
        => binding == SamlBinding.HttpPost ? SamlMetadataBindings.HttpPost : SamlMetadataBindings.HttpRedirect;

    /// <summary>
    /// The name admins and IdP teams know the binding by (<c>HTTP-Redirect</c>, <c>HTTP-POST</c>).
    /// </summary>
    /// <param name="binding">Binding</param>
    public static string ToDisplayName(this SamlBinding binding)
        => binding == SamlBinding.HttpPost ? "HTTP-POST" : "HTTP-Redirect";

    /// <summary>
    /// Read a binding from any stored or typed form: the enum name, the display name or the URN.
    /// Anything that does not mention POST is HTTP-Redirect, the default.
    /// </summary>
    /// <param name="value">Stored or typed value</param>
    public static SamlBinding Parse(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Contains("POST", StringComparison.OrdinalIgnoreCase)
            ? SamlBinding.HttpPost
            : SamlBinding.HttpRedirect;
}
