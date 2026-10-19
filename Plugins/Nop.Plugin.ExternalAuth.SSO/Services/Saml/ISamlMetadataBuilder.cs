using Microsoft.AspNetCore.Mvc;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Generates the SP <c>EntityDescriptor</c> XML for the current store.
/// </summary>
/// <remarks>
/// IdPs typically point at <c>/saml/metadata</c> during onboarding to learn the SP's
/// <c>EntityID</c>, ACS URL, supported bindings, and signing/encryption certificates.
/// Endpoint is anonymous; nothing in the metadata is a secret (signing certs are public).
/// </remarks>
public interface ISamlMetadataBuilder
{
    /// <summary>
    /// Build the SP metadata for the current store. Returns the XML body as an
    /// <see cref="IActionResult"/> suitable for <c>GET /saml/metadata</c>.
    /// </summary>
    Task<IActionResult> BuildMetadataAsync();
}
