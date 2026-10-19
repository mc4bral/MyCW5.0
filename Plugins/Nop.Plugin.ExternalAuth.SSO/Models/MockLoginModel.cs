#if DEBUG
namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>
/// Form-post model for the dev-only Mock IdP login page.
/// Wrapped in <c>#if DEBUG</c> — does not exist in Release builds.
/// </summary>
/// <remarks>
/// Posted to <c>MockSsoController.SamlComplete</c> or <c>MockSsoController.OidcComplete</c>.
/// The controller builds a <see cref="System.Security.Claims.ClaimsPrincipal"/> from these
/// fields and delegates to <c>ISsoUserProvisioningService.HandleAsync</c> — the same
/// chokepoint the real SAML/OIDC paths use.
/// </remarks>
public class MockLoginModel
{
    /// <summary>Scenario radio value — see <c>SsoMockScenarios</c>.</summary>
    public string Scenario { get; set; } = string.Empty;

    /// <summary>Email asserted by the mock IdP. Drives the email-domain allowlist + customer matching.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>First-name claim payload. Used only for new-user auto-register.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Last-name claim payload. Used only for new-user auto-register.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// IdP subject identifier (<c>oid</c> claim for OIDC, <c>NameID</c> for SAML).
    /// Defaults to the email when blank — many test scenarios don't care about a separate stable id.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated list of group GUIDs to assert in the configured groups claim.
    /// Used by the <c>success-with-groups</c> scenario to exercise group→role mapping.
    /// </summary>
    public string Groups { get; set; } = string.Empty;

    /// <summary>Return URL preserved across the mock redirect, so the user lands where they started.</summary>
    public string ReturnUrl { get; set; } = string.Empty;
}
#endif
