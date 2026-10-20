#if DEBUG
namespace Nop.Plugin.ExternalAuth.SSO.Constants;

/// <summary>
/// Routing + form constants for the dev-only Mock IdP surface.
/// Wrapped in <c>#if DEBUG</c> so the constants do not exist in Release builds.
/// </summary>
/// <remarks>
/// Paired with <c>MockSsoController</c> (also DEBUG-only). The mock flow exists purely
/// to exercise the provisioning pipeline (<c>ISsoUserProvisioningService.HandleAsync</c>)
/// without a working Azure AD tenant or SP/IdP certificates. See References/SSO_Plugin_Reference.md §9.
/// </remarks>
public static class MockSsoDefaults
{
    /// <summary>Mock IdP login URL — the page the storefront login is redirected to when EnableMockMode is on.</summary>
    public const string LoginPath = "/mocksso/login";

    /// <summary>Mock SAML completion URL — receives the form post and dispatches to the provisioning service as protocol = "Saml2".</summary>
    public const string SamlCompletePath = "/mocksso/saml/complete";

    /// <summary>Mock OIDC completion URL — receives the form post and dispatches to the provisioning service as protocol = "Oidc".</summary>
    public const string OidcCompletePath = "/mocksso/oidc/complete";

    /// <summary>Query-string protocol marker for the mock login page. Values: "saml" or "oidc".</summary>
    public const string ProtocolQueryKey = "protocol";

    /// <summary>Mock protocol value naming SAML 2.0.</summary>
    public const string ProtocolSaml = "saml";

    /// <summary>Mock protocol value naming OpenID Connect.</summary>
    public const string ProtocolOidc = "oidc";
}
#endif
