namespace Nop.Plugin.ExternalAuth.SSO.Constants;

/// <summary>
/// <see cref="Nop.Services.Common.IGenericAttributeService"/> keys that the plugin sets on
/// the <c>Customer</c> after a successful SSO sign-in. These are read by the SAML SLO flow
/// and by the logout-actions filter to know which protocol to terminate.
/// </summary>
public static class SsoCustomerAttributes
{
    /// <summary>SAML <c>NameID</c> persisted at login for SP-initiated <c>LogoutRequest</c>.</summary>
    public const string SamlNameId = "ExternalAuth.SSO.SamlNameId";

    /// <summary>SAML <c>SessionIndex</c> persisted at login for SP-initiated <c>LogoutRequest</c>.</summary>
    public const string SamlSessionIndex = "ExternalAuth.SSO.SamlSessionIndex";

    /// <summary>
    /// Protocol the customer signed in with (one of <see cref="SsoProtocolNames"/> values).
    /// Read by <c>LogoutActionsFilter</c> to choose the right SLO path on customer logout.
    /// </summary>
    public const string SignInProtocol = "ExternalAuth.SSO.SignInProtocol";

    /// <summary>
    /// The OIDC <c>id_token</c> from the customer's last SSO sign-in, sent back to the IdP as
    /// <c>id_token_hint</c> at logout so it ends the right session without an account picker.
    /// Cleared at logout.
    /// </summary>
    public const string OidcIdToken = "ExternalAuth.SSO.OidcIdToken";
}
