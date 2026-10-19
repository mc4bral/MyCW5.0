using Nop.Core.Configuration;

namespace Nop.Plugin.ExternalAuth.SSO.Settings;

/// <summary>
/// Per-store configuration for the ExternalAuth.SSO plugin.
/// </summary>
/// <remarks>
/// Persisted via nopCommerce's standard <c>ISettings</c> mechanism with per-store overrides
/// (<c>SaveSettingOverridablePerStoreAsync</c>). Sensitive fields are stored as ciphertext —
/// callers must round-trip through <c>ISsoSecretsService</c> at the controller boundary
/// (write-encrypt on POST, read-decrypt on GET). See SSOPlan.v2.md §3.3.
/// </remarks>
public class SsoConfigurationSettings : ISettings
{
    #region General

    /// <summary>
    /// Active protocol for this store.
    /// </summary>
    public SsoProtocol Protocol { get; set; } = SsoProtocol.Off;

    /// <summary>
    /// Master switch. When <c>false</c> the public login button is hidden and the auth filter does not redirect.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Public base URL of the store, used to construct absolute callback URLs.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Sort order for the SSO Accounts admin list (M12). Lower numbers appear first.
    /// Defaults to 0 — admins can renumber per store to group/highlight specific tenants.
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Display name for the IdP, shown in the public-facing "Sign in with X" button and
    /// the onboarding-report header. Falls back to "Microsoft" when empty.
    /// </summary>
    public string IdpDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Dev-only flag. When <c>true</c> AND the assembly was built with the <c>DEBUG</c>
    /// symbol, <c>ExternalAuthenticationController.Login</c> redirects to the in-plugin
    /// mock IdP page (<c>/mocksso/login</c>) instead of issuing a real OIDC challenge or
    /// SAML AuthnRequest. The mock surface is excluded from Release builds entirely
    /// (<c>#if DEBUG</c> guards on the controller, view import and Login-action branch),
    /// so flipping this on in a Release deployment has no effect.
    /// </summary>
    /// <remarks>
    /// Lets developers exercise the full provisioning + audit + role-mapping pipeline
    /// without a working Azure AD tenant or SP/IdP certificates. Per the mock spec the
    /// mock complete endpoints construct a <c>ClaimsPrincipal</c> from form fields and
    /// pass it directly to <c>ISsoUserProvisioningService.HandleAsync</c> — the real
    /// SAML/OIDC cryptographic paths are not touched.
    /// </remarks>
    public bool EnableMockMode { get; set; }

    #endregion

    #region OpenID Connect

    /// <summary>
    /// OIDC authority — e.g. <c>https://login.microsoftonline.com/&lt;tenant-id&gt;/v2.0</c>.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// OIDC application (client) ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// OIDC client secret — stored as ciphertext.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Callback path the OIDC handler listens on. Defaults to <c>/signin-microsoft</c>.
    /// </summary>
    public string CallbackPath { get; set; } = "/signin-microsoft";

    /// <summary>
    /// Space-delimited OIDC scopes. Defaults to <c>openid profile email</c>.
    /// </summary>
    public string Scopes { get; set; } = "openid profile email";

    #endregion

    #region SAML 2.0

    /// <summary>
    /// IdP <c>EntityID</c>.
    /// </summary>
    public string IdpEntityId { get; set; } = string.Empty;

    /// <summary>
    /// IdP Single Sign-On URL.
    /// </summary>
    public string IdpSsoUrl { get; set; } = string.Empty;

    /// <summary>
    /// IdP Single Log-Out URL. Optional; SLO is disabled when empty.
    /// </summary>
    public string IdpSloUrl { get; set; } = string.Empty;

    /// <summary>
    /// IdP signing certificate (Base64 DER, no PEM headers) — stored as ciphertext.
    /// </summary>
    public string IdpCertificateBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Optional URL to the IdP federation metadata XML; when set, <c>IIdpMetadataImporter</c>
    /// auto-refreshes IdP fields from it.
    /// </summary>
    public string IdpMetadataUrl { get; set; } = string.Empty;

    /// <summary>
    /// SP <c>EntityID</c>.
    /// </summary>
    public string SpEntityId { get; set; } = string.Empty;

    /// <summary>
    /// SP Assertion Consumer Service URL (the SAML response handler endpoint of this store).
    /// </summary>
    public string AcsUrl { get; set; } = string.Empty;

    /// <summary>
    /// SP logout URL (where the IdP posts logout responses back to).
    /// </summary>
    public string SpLogoutUrl { get; set; } = string.Empty;

    /// <summary>
    /// SP signing PFX (Base64) — required when <see cref="SignedRequest"/>
    /// or <see cref="EncryptedAssertion"/> is enabled. Stored as ciphertext.
    /// </summary>
    public string SpPfxBase64 { get; set; } = string.Empty;

    /// <summary>
    /// SP PFX password — stored as ciphertext.
    /// </summary>
    public string SpPfxPassword { get; set; } = string.Empty;

    /// <summary>
    /// Sign outbound <c>AuthnRequest</c> messages.
    /// </summary>
    public bool SignedRequest { get; set; }

    /// <summary>
    /// Sign outbound <c>LogoutRequest</c> messages. Separate switch so a tenant can require
    /// signing on login challenges but not on SLO (or vice versa) per their IdP policy.
    /// </summary>
    public bool SignLogoutRequest { get; set; }

    /// <summary>
    /// Advertise <c>WantAssertionsEncrypted</c> in SP metadata and decrypt encrypted assertions on the ACS endpoint.
    /// </summary>
    public bool EncryptedAssertion { get; set; }

    /// <summary>
    /// SAML binding for login requests (e.g. <c>HTTP-Redirect</c>, <c>HTTP-POST</c>).
    /// </summary>
    public string LoginBinding { get; set; } = "HTTP-Redirect";

    /// <summary>
    /// SAML binding for logout requests.
    /// </summary>
    public string LogoutBinding { get; set; } = "HTTP-Redirect";

    #endregion

    #region Claim mapping

    /// <summary>
    /// Claim type that carries the email address.
    /// </summary>
    public string EmailClaimType { get; set; } = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";

    /// <summary>
    /// Claim type that carries the username.
    /// </summary>
    public string UsernameClaimType { get; set; } = "preferred_username";

    /// <summary>
    /// Claim type that carries the first name.
    /// </summary>
    public string FirstNameClaimType { get; set; } = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname";

    /// <summary>
    /// Claim type that carries the last name.
    /// </summary>
    public string LastNameClaimType { get; set; } = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname";

    /// <summary>
    /// Claim type that carries the group membership list. Entra Object IDs are expected.
    /// </summary>
    public string GroupsClaimType { get; set; } = "groups";

    #endregion

    #region User provisioning

    /// <summary>
    /// When <c>true</c>, create a new nop customer if no existing customer matches the asserted email.
    /// </summary>
    public bool AutoRegister { get; set; } = true;

    /// <summary>
    /// Default role assigned to auto-registered customers (system name).
    /// </summary>
    public string DefaultRoleSystemName { get; set; } = "Registered";

    /// <summary>
    /// Allowed email-domain suffixes (CSV). Empty = all domains accepted. Wildcards supported (e.g. <c>*.example.com</c>).
    /// </summary>
    public string AllowedEmailDomains { get; set; } = string.Empty;

    /// <summary>
    /// When <c>true</c>, re-apply group→role mapping on every successful login (within the plugin's owned set).
    /// </summary>
    public bool RefreshRolesOnLogin { get; set; } = true;

    /// <summary>
    /// When <c>true</c> (default), <c>LoginActionsFilter</c> redirects the storefront
    /// <c>/login</c> GET to the SSO challenge once SSO is enabled for the store. When
    /// <c>false</c>, the standard local login form continues to render and the public
    /// "Sign in with Microsoft" button remains available as an alternative.
    /// </summary>
    public bool RedirectLoginToSso { get; set; } = true;

    #endregion

    #region Custom attributes

    /// <summary>
    /// Free-form JSON for extensibility — passed to plugin extensions that need
    /// per-store configuration we don't model explicitly. Stored as-is; no validation
    /// beyond "is parseable JSON" (validation rule applied at form post).
    /// </summary>
    public string CustomAttributesJson { get; set; } = string.Empty;

    #endregion

    #region Redirects

    /// <summary>
    /// Local URL to redirect to after successful login. Guarded by <c>IUrlHelper.IsLocalUrl</c>.
    /// </summary>
    public string PostLoginRedirectUrl { get; set; } = string.Empty;

    /// <summary>
    /// Local URL to redirect to after logout. Guarded by <c>IUrlHelper.IsLocalUrl</c>.
    /// </summary>
    public string PostLogoutRedirectUrl { get; set; } = string.Empty;

    #endregion
}
