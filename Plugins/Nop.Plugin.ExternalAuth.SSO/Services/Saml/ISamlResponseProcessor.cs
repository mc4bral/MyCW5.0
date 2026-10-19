using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// Validates an incoming SAML <c>Response</c> on the ACS endpoint and hands the extracted
/// claims to the protocol-agnostic <see cref="ISsoUserProvisioningService"/>.
/// </summary>
public interface ISamlResponseProcessor
{
    /// <summary>
    /// Unbind + validate the SAMLResponse, persist NameID + SessionIndex on the customer
    /// after sign-in, and return the redirect <see cref="IActionResult"/> produced by
    /// <see cref="ISsoUserProvisioningService.HandleAsync"/>.
    /// </summary>
    Task<IActionResult> ProcessAcsAsync(HttpRequest request);
}
