using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>
/// Admin Configure-page view model for the ExternalAuth.SSO plugin.
/// </summary>
/// <remarks>
/// <para>
/// The plugin uses nopCommerce's standard store-scope picker
/// (<c>StoreScopeConfigurationViewComponent</c>) for selecting which store to edit:
/// <see cref="ActiveStoreScopeConfiguration"/> = 0 means "All stores" (system-wide
/// settings editor only); &gt; 0 means a specific store.
/// </para>
/// <para>
/// There are no <c>XXX_OverrideForStore</c> flags on this model — the override is implicit.
/// When a specific store is selected the factory writes every per-store field for that
/// store with <c>overrideForStore = true</c>. When "All stores" is selected the per-store
/// cards are not rendered and the factory does not touch the per-store fields at all.
/// System-wide settings (<see cref="SsoSystemSettings"/>) save at <c>storeId = 0</c>
/// regardless of the active scope.
/// </para>
/// <para>
/// Sensitive fields (ClientSecret, IdpCertificateBase64, SpPfxBase64, SpPfxPassword,
/// SsoBypassToken) are never round-tripped as ciphertext: the GET zeroes the value and
/// sets a paired <c>Has{Field}</c> flag indicating whether one is currently stored.
/// On POST an empty value means "leave existing untouched"; a non-empty value replaces.
/// </para>
/// </remarks>
public record ConfigurationModel : BaseNopModel
{
    public ConfigurationModel()
    {
        AvailableCustomerRoles = new List<SelectListItem>();
    }

    /// <summary>
    /// Store scope being edited (0 = "all stores"). Read on POST from the standard
    /// nop store-scope picker's hidden state.
    /// </summary>
    public int ActiveStoreScopeConfiguration { get; set; }

    #region General

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.Protocol")]
    public SsoProtocol Protocol { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.IsEnabled")]
    public bool IsEnabled { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.BaseUrl")]
    public string BaseUrl { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.DisplayOrder")]
    public int DisplayOrder { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.IdpDisplayName")]
    public string IdpDisplayName { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.EnableMockMode")]
    public bool EnableMockMode { get; set; }

    #endregion

    #region OpenID Connect

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.Authority")]
    public string Authority { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.ClientId")]
    public string ClientId { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.ClientSecret")]
    [DataType(DataType.Password)]
    public string ClientSecret { get; set; } = string.Empty;
    public bool HasClientSecret { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.CallbackPath")]
    public string CallbackPath { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.Scopes")]
    public string Scopes { get; set; } = string.Empty;

    #endregion

    #region SAML 2.0

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.IdpEntityId")]
    public string IdpEntityId { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.IdpSsoUrl")]
    public string IdpSsoUrl { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.IdpSloUrl")]
    public string IdpSloUrl { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.IdpCertificateBase64")]
    public string IdpCertificateBase64 { get; set; } = string.Empty;
    public bool HasIdpCertificate { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.IdpMetadataUrl")]
    public string IdpMetadataUrl { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.SpEntityId")]
    public string SpEntityId { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.AcsUrl")]
    public string AcsUrl { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.SpLogoutUrl")]
    public string SpLogoutUrl { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.SpPfxBase64")]
    public string SpPfxBase64 { get; set; } = string.Empty;
    public bool HasSpPfx { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.SpPfxPassword")]
    [DataType(DataType.Password)]
    public string SpPfxPassword { get; set; } = string.Empty;
    public bool HasSpPfxPassword { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.SignedRequest")]
    public bool SignedRequest { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.SignLogoutRequest")]
    public bool SignLogoutRequest { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.EncryptedAssertion")]
    public bool EncryptedAssertion { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.AllowIdpInitiatedLogin")]
    public bool AllowIdpInitiatedLogin { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.LoginBinding")]
    public SamlBinding LoginBinding { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.LogoutBinding")]
    public SamlBinding LogoutBinding { get; set; }

    /// <summary>Binding choices for the two dropdowns (value = enum name, text = HTTP-Redirect / HTTP-POST).</summary>
    public IList<SelectListItem> AvailableBindings { get; set; } = new List<SelectListItem>();

    #endregion

    #region Claim mapping

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.EmailClaimType")]
    public string EmailClaimType { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.UsernameClaimType")]
    public string UsernameClaimType { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.FirstNameClaimType")]
    public string FirstNameClaimType { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.LastNameClaimType")]
    public string LastNameClaimType { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.GroupsClaimType")]
    public string GroupsClaimType { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.RolesClaimType")]
    public string RolesClaimType { get; set; } = string.Empty;

    #endregion

    #region User provisioning

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.AutoRegister")]
    public bool AutoRegister { get; set; }

    /// <summary>
    /// Roles offered as default roles (privileged roles left out).
    /// </summary>
    public IList<SelectListItem> AvailableCustomerRoles { get; set; }

    /// <summary>
    /// The store's default role ids. Optional: no selection means no default role. Initialised so
    /// MVC's implicit <c>[Required]</c> for a non-nullable reference (nullable context) does not
    /// reject a post with nothing selected.
    /// </summary>
    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.DefaultRoles")]
    public IList<int> DefaultRoleIds { get; set; } = new List<int>();

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.AllowedEmailDomains")]
    public string AllowedEmailDomains { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.RefreshRolesOnLogin")]
    public bool RefreshRolesOnLogin { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.RedirectLoginToSso")]
    public bool RedirectLoginToSso { get; set; }

    #endregion

    #region Redirects

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.PostLoginRedirectUrl")]
    public string PostLoginRedirectUrl { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.PostLogoutRedirectUrl")]
    public string PostLogoutRedirectUrl { get; set; } = string.Empty;

    #endregion

    #region Cert / PFX previews + "Clear stored on save" toggles

    /// <summary>
    /// Short preview of the currently stored IdP certificate (first 10 + last 10 Base64
    /// chars). Lets admins confirm which cert is loaded without exposing the full value.
    /// </summary>
    public string IdpCertificatePreview { get; set; } = string.Empty;

    /// <summary>Tick + Save to wipe the stored IdP cert.</summary>
    public bool ClearIdpCertificate { get; set; }

    /// <summary>Preview for the SP PFX.</summary>
    public string SpPfxPreview { get; set; } = string.Empty;

    /// <summary>Tick + Save to wipe the stored SP PFX.</summary>
    public bool ClearSpPfx { get; set; }

    /// <summary>Tick + Save to wipe the stored SP PFX password.</summary>
    public bool ClearSpPfxPassword { get; set; }

    /// <summary>Tick + Save to wipe the stored OIDC client secret.</summary>
    public bool ClearClientSecret { get; set; }

    /// <summary>Tick + Save to wipe the stored bypass token (disables the bypass entirely).</summary>
    public bool ClearSsoBypassToken { get; set; }

    #endregion

    #region System (always storeId=0)

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.SsoBypassToken")]
    [DataType(DataType.Password)]
    public string SsoBypassToken { get; set; } = string.Empty;
    public bool HasSsoBypassToken { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.LocalLoginAllowedRoles")]
    public string LocalLoginAllowedRoles { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.LoginAttemptCapPerHour")]
    public int LoginAttemptCapPerHour { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.EnableAuditLogging")]
    public bool EnableAuditLogging { get; set; } = true;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.Fields.AuditRetentionDays")]
    public int AuditRetentionDays { get; set; }

    #endregion
}
