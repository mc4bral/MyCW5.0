namespace Nop.Plugin.ExternalAuth.SSO.Settings;

/// <summary>
/// The Single Sign-On protocol active for a store.
/// </summary>
/// <remarks>
/// Exactly one protocol is active per store, selected by
/// the admin via a dropdown on the Configure page. When <see cref="Off"/>, the public
/// "Sign in with Microsoft" button does not render and <c>LoginActionsFilter</c> does not
/// redirect to the external auth challenge.
/// </remarks>
public enum SsoProtocol
{
    /// <summary>
    /// SSO is disabled for the store. The local login form is the only authentication path.
    /// </summary>
    Off = 0,

    /// <summary>
    /// OpenID Connect (primary protocol — preferred for Microsoft Entra ID).
    /// </summary>
    OpenIdConnect = 1,

    /// <summary>
    /// SAML 2.0 (secondary protocol — for legacy IdP compatibility, implemented via ITfoxtec.Identity.Saml2).
    /// </summary>
    Saml2 = 2
}
