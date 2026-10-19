namespace Nop.Plugin.ExternalAuth.SSO.Constants;

/// <summary>
/// <see cref="Nop.Services.Common.IGenericAttributeService"/> keys that the plugin sets on
/// the <c>Customer</c> after a successful SSO sign-in. These are read by the SAML SLO flow
/// (M6.c) and by the logout-actions filter to know which protocol to terminate.
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
}
