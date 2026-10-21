using System.Text;
using Nop.Plugin.ExternalAuth.SSO.Constants;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Configuration;
using Nop.Services.Stores;

namespace Nop.Plugin.ExternalAuth.SSO.Services;

/// <summary>
/// Default implementation of <see cref="IOnboardingReportService"/>.
/// </summary>
public class OnboardingReportService : IOnboardingReportService
{
    #region Fields

    protected readonly ISettingService _settingService;
    protected readonly IStoreService _storeService;

    #endregion

    #region Ctor

    public OnboardingReportService(
        ISettingService settingService,
        IStoreService storeService)
    {
        _settingService = settingService;
        _storeService = storeService;
    }

    #endregion

    #region Utilities

    #region OIDC

    private static void BuildOidcSection1(StringBuilder sb, SsoConfigurationSettings settings, string baseUrl)
    {
        var redirectUri = !string.IsNullOrWhiteSpace(baseUrl) && !string.IsNullOrWhiteSpace(settings.CallbackPath)
            ? baseUrl + settings.CallbackPath
            : "(set Base URL and Callback path on the Configure page first)";

        var postLogoutUri = !string.IsNullOrWhiteSpace(baseUrl)
            ? baseUrl + SSOAuthenticationDefaults.OidcSignedOutCallbackPath
            : "(set Base URL on the Configure page first)";

        AppendValue(sb, "Redirect URI (Reply URL)",
            "Entra: App registrations -> (your app) -> Authentication -> Add a platform -> Web",
            "Type the redirect URI in the 'Redirect URIs' box.",
            redirectUri);

        AppendValue(sb, "Post-logout redirect URI",
            "Entra: same Authentication page -> 'Front-channel logout URL'",
            null,
            postLogoutUri);

        AppendValue(sb, "Allowed token audience (Application ID)",
            "Entra: Overview -> 'Application (client) ID'",
            "This goes into our Client ID field. Send the value back in Section 2.",
            "(populated from the value you send back)");

        sb.AppendLine("Token settings:");
        sb.AppendLine("  - Issue ID tokens with the user's email and group membership.");
        sb.AppendLine("  - Optional: configure 'groups' claim under Token configuration if you want");
        sb.AppendLine("    Entra groups mapped to nopCommerce roles (advanced — coordinate with Company).");
        sb.AppendLine();
    }

    private static void BuildOidcSection2(StringBuilder sb)
    {
        AppendBlank(sb, "Application (client) ID", "Entra: App registrations -> (your app) -> Overview");
        AppendBlank(sb, "Tenant ID (Directory ID)", "Entra: App registrations -> (your app) -> Overview");
        AppendBlank(sb, "Client secret", "Entra: Certificates & secrets -> New client secret");
        sb.AppendLine("Optional — non-default OIDC authority (e.g. govcloud / sovereign tenants)");
        sb.AppendLine("  Default authority: https://login.microsoftonline.com/<tenant>/v2.0");
        sb.AppendLine("  Value: ____________________________________________________");
        sb.AppendLine();
    }

    #endregion

    #region SAML

    private static void BuildSamlSection1(StringBuilder sb, SsoConfigurationSettings settings, string baseUrl)
    {
        var acsUrl = !string.IsNullOrWhiteSpace(settings.AcsUrl)
            ? settings.AcsUrl
            : (!string.IsNullOrWhiteSpace(baseUrl)
                ? baseUrl + SSOAuthenticationDefaults.SamlAcsPath
                : "(set Base URL on the Configure page first)");

        var metadataUrl = !string.IsNullOrWhiteSpace(baseUrl)
            ? baseUrl + SSOAuthenticationDefaults.SamlMetadataPath
            : "(set Base URL on the Configure page first)";

        var signOnUrl = !string.IsNullOrWhiteSpace(baseUrl)
            ? baseUrl + "/external-authentication/login"
            : "(set Base URL on the Configure page first)";

        AppendValue(sb, "Identifier (SP Entity ID)",
            "Entra: Enterprise applications -> (your app) -> Single sign-on",
            "       -> Basic SAML Configuration -> Identifier (Entity ID)",
            string.IsNullOrWhiteSpace(settings.SpEntityId) ? "(not configured)" : settings.SpEntityId);

        AppendValue(sb, "Reply URL (Assertion Consumer Service URL)",
            "Entra: Basic SAML Configuration -> Reply URL (ACS URL)",
            null,
            acsUrl);

        AppendValue(sb, "Sign-on URL (optional but recommended)",
            "Entra: Basic SAML Configuration -> Sign on URL",
            null,
            signOnUrl);

        AppendValue(sb, "Logout URL (optional — enables Single Log-Out)",
            "Entra: Basic SAML Configuration -> Logout URL",
            null,
            string.IsNullOrWhiteSpace(settings.SpLogoutUrl) ? baseUrl + SSOAuthenticationDefaults.SamlLogoutPath : settings.SpLogoutUrl);

        AppendValue(sb, "SP Metadata URL (recommended import shortcut)",
            "Entra exposes an 'Upload metadata file' button — paste this URL or its XML body.",
            "Most other SAML IdPs offer the same import option.",
            metadataUrl);

        sb.AppendLine("Additional facts the IdP team may need:");
        sb.AppendLine("  - NameID format expected: persistent (urn:oasis:names:tc:SAML:1.1:nameid-format:persistent)");
        sb.AppendLine("  - Sign AuthnRequest:    " + (settings.SignedRequest ? "Yes" : "No"));
        sb.AppendLine("  - Encrypted Assertion:  " + (settings.EncryptedAssertion ? "Yes" : "No"));
        sb.AppendLine();
        sb.AppendLine("Required claims (these attribute names must arrive in the SAML assertion):");
        sb.AppendLine("  - Email:      " + WithDefault(settings.EmailClaimType, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress"));
        sb.AppendLine("  - First name: " + WithDefault(settings.FirstNameClaimType, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname"));
        sb.AppendLine("  - Last name:  " + WithDefault(settings.LastNameClaimType, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/surname"));
        sb.AppendLine("  - Groups:     " + WithDefault(settings.GroupsClaimType, "(none)"));
        sb.AppendLine("  - App roles:  " + WithDefault(settings.RolesClaimType, "(none)"));
        sb.AppendLine();
    }

    private static void BuildSamlSection2(StringBuilder sb)
    {
        AppendBlank(sb, "IdP display name (e.g. 'Contoso Entra')", null);
        AppendBlank(sb, "IdP SSO (login) URL", "Entra: Set up SSO with SAML -> Login URL");
        AppendBlank(sb, "IdP Logout URL (optional)", "Entra: Set up SSO with SAML -> Logout URL");
        AppendBlank(sb, "IdP Entity ID / Issuer", "Entra: 'Microsoft Entra Identifier' (also called Azure AD Identifier)");

        sb.AppendLine("IdP Signing Certificate (Base64 X.509)");
        sb.AppendLine("  Entra: SAML Signing Certificate -> 'Certificate (Base64)' download.");
        sb.AppendLine("  Open the .cer file in a text editor and paste its contents here:");
        sb.AppendLine("    -----BEGIN CERTIFICATE-----");
        sb.AppendLine("    ____________________________________________________________");
        sb.AppendLine("    ____________________________________________________________");
        sb.AppendLine("    -----END CERTIFICATE-----");
        sb.AppendLine();

        sb.AppendLine("Optional — claim attribute names if you customised them in the IdP:");
        sb.AppendLine("  Email claim:      ____________________________");
        sb.AppendLine("  First name claim: ____________________________");
        sb.AppendLine("  Last name claim:  ____________________________");
        sb.AppendLine("  Group attribute:  ____________________________");
        sb.AppendLine();
    }

    private static void BuildGenericSection2(StringBuilder sb)
    {
        sb.AppendLine("(No protocol selected — pick OIDC or SAML on the Configure page, save, then");
        sb.AppendLine(" regenerate this report. The relevant Section 2 will then be filled in.)");
        sb.AppendLine();
    }

    #endregion

    #region Formatting

    private static void AppendValue(StringBuilder sb, string title, string path1, string? path2, string value)
    {
        sb.AppendLine(title);
        if (!string.IsNullOrEmpty(path1)) sb.Append("  ").AppendLine(path1);
        if (!string.IsNullOrEmpty(path2)) sb.Append("  ").AppendLine(path2);
        sb.Append("  Value: ").AppendLine(string.IsNullOrWhiteSpace(value) ? "(not configured)" : value);
        sb.AppendLine();
    }

    private static void AppendBlank(StringBuilder sb, string title, string? path)
    {
        sb.AppendLine(title);
        if (!string.IsNullOrEmpty(path)) sb.Append("  ").AppendLine(path);
        sb.AppendLine("  Value: ____________________________________________________");
        sb.AppendLine();
    }

    private static string WithDefault(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    #endregion

    #endregion

    #region Methods

    /// <inheritdoc />
    public async Task<string> BuildReportAsync(int storeScope, string requestScheme, string requestHost)
    {
        var settings = await _settingService.LoadSettingAsync<SsoConfigurationSettings>(storeScope);

        var baseUrl = !string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? settings.BaseUrl.TrimEnd('/')
            : (!string.IsNullOrWhiteSpace(requestScheme) && !string.IsNullOrWhiteSpace(requestHost)
                ? $"{requestScheme}://{requestHost}"
                : string.Empty);

        var storeName = "(All stores)";
        if (storeScope > 0)
        {
            var store = await _storeService.GetStoreByIdAsync(storeScope);
            if (store is not null)
                storeName = store.Name;
        }

        var sb = new StringBuilder();
        const string sep = "------------------------------------------------------------";

        sb.AppendLine("SSO — Identity Provider onboarding information");
        sb.Append("Store: ").Append(storeName)
            .Append("    Generated: ").Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"))
            .AppendLine();
        if (!string.IsNullOrWhiteSpace(settings.IdpDisplayName))
            sb.Append("IdP display name: ").AppendLine(settings.IdpDisplayName);
        sb.Append("Protocol selected: ").AppendLine(settings.Protocol.ToString());
        sb.AppendLine();
        sb.AppendLine("This document has two sections:");
        sb.AppendLine("  1. Values to paste into your Identity Provider (Entra / ADFS / Okta).");
        sb.AppendLine("  2. Values we need you to send back so we can finish the setup.");
        sb.AppendLine();

        // ----- Section 1 --------------------------------------------------------
        sb.AppendLine(sep);
        sb.AppendLine("SECTION 1 — Paste these values into your IdP");
        sb.AppendLine(sep);
        sb.AppendLine();

        if (settings.Protocol == SsoProtocol.OpenIdConnect)
            BuildOidcSection1(sb, settings, baseUrl);
        else if (settings.Protocol == SsoProtocol.Saml2)
            BuildSamlSection1(sb, settings, baseUrl);
        else
            sb.AppendLine("(Protocol is currently set to Off — pick OIDC or SAML on the Configure page first.)");

        sb.AppendLine();

        // ----- Section 2 --------------------------------------------------------
        sb.AppendLine(sep);
        sb.AppendLine("SECTION 2 — Please send the following back");
        sb.AppendLine(sep);
        sb.AppendLine();

        if (settings.Protocol == SsoProtocol.OpenIdConnect)
            BuildOidcSection2(sb);
        else if (settings.Protocol == SsoProtocol.Saml2)
            BuildSamlSection2(sb);
        else
            BuildGenericSection2(sb);

        sb.AppendLine();
        sb.AppendLine("Once we receive the items above we will load them into the SSO configuration");
        sb.AppendLine("on your store and run an end-to-end test login with you. If anything in this");
        sb.AppendLine("document is unclear, reply to this email and we will help.");
        sb.AppendLine();

        return sb.ToString();
    }

    #endregion
}
