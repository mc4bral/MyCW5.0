using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.ExternalAuth.SSO.Models;

/// <summary>
/// One row in the SSO Accounts admin grid (M12).
/// </summary>
public record SsoStoreSummaryModel : BaseNopEntityModel
{
    /// <summary>
    /// Identifier of the underlying nopCommerce Store. Used by the Edit / Test buttons.
    /// </summary>
    public int StoreId { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.SsoAccounts.Columns.StoreName")]
    public string StoreName { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.SsoAccounts.Columns.Protocol")]
    public string Protocol { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.SsoAccounts.Columns.Enabled")]
    public bool IsEnabled { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.SsoAccounts.Columns.DisplayOrder")]
    public int DisplayOrder { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.SsoAccounts.Columns.DateSetup")]
    public string DateSetup { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.SsoAccounts.Columns.NumberOfUsers")]
    public int NumberOfUsers { get; set; }

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.SsoAccounts.Columns.LastSuccessfulLogin")]
    public string LastSuccessfulLogin { get; set; } = string.Empty;

    [NopResourceDisplayName("Plugins.ExternalAuth.SSO.SsoAccounts.Columns.CertificateNotAfter")]
    public string CertificateNotAfter { get; set; } = string.Empty;

    /// <summary>
    /// <c>true</c> when the IdP cert's <c>NotAfter</c> is within 30 days but still in the future —
    /// the grid highlights the cell amber so admins can plan a rotation.
    /// </summary>
    public bool CertificateNearExpiry { get; set; }

    /// <summary>
    /// <c>true</c> when the IdP cert's <c>NotAfter</c> is already in the past — SAML signature
    /// validation will currently be failing. Grid highlights the cell red.
    /// </summary>
    public bool CertificateExpired { get; set; }
}
