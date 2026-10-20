namespace Nop.Plugin.ExternalAuth.SSO.Services.Saml;

/// <summary>
/// What a <see cref="ITfoxtec.Identity.Saml2.Saml2Configuration"/> is built for. Decides whether
/// it carries the SP signing certificate, because ITfoxtec signs whatever it binds when one is
/// present.
/// </summary>
public enum SamlConfigPurpose
{
    /// <summary>AuthnRequest and ACS: signs when <c>SignedRequest</c> is on.</summary>
    Login = 0,

    /// <summary>LogoutRequest / LogoutResponse: signs when <c>SignLogoutRequest</c> is on.</summary>
    Logout = 1,

    /// <summary>SP metadata: carries the certificate when either kind of signing is on, so the IdP can verify both.</summary>
    Metadata = 2
}
