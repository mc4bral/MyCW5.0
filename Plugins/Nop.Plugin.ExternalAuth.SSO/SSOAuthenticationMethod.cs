using Nop.Core;
using Nop.Plugin.ExternalAuth.SSO.Components;
using Nop.Plugin.ExternalAuth.SSO.Security;
using Nop.Plugin.ExternalAuth.SSO.Settings;
using Nop.Services.Authentication.External;
using Nop.Services.Configuration;
using Nop.Services.Helpers;
using Nop.Services.Localization;
using Nop.Services.Plugins;
using Nop.Services.Security;

namespace Nop.Plugin.ExternalAuth.SSO;

/// <summary>
/// Entry point for the ExternalAuth.SSO plugin. Authenticates nopCommerce customers
/// against Microsoft Entra ID via OpenID Connect (primary) or SAML 2.0 (secondary),
/// selectable per store.
/// </summary>
/// <remarks>
/// Structured after <see cref="Nop.Plugin.Integration.Odoo.OdooIntegrationPlugin"/> for the
/// install / uninstall / update lifecycle. The FluentMigrator schema migration
/// (<see cref="Data.Migrations.SchemaMigration"/>) is invoked by
/// <c>base.InstallAsync</c> / <c>base.UninstallAsync</c> — no explicit table create/drop
/// code lives in this class.
///
/// Implements <see cref="IExternalAuthenticationMethod"/> so nopCommerce recognises it as
/// an external authentication provider and surfaces it on the public login page via
/// <see cref="GetPublicViewComponent"/>.
///
/// Remaining lifecycle responsibility by milestone:
/// <list type="bullet">
///   <item>M3 — register group→role mapping defaults if any.</item>
/// </list>
/// See SSOPlan.v2.md §3.11 and §10.
/// </remarks>
public class SSOAuthenticationMethod : BasePlugin, IExternalAuthenticationMethod
{
    #region Fields

    private readonly ILocalizationService _localizationService;
    private readonly IPermissionService _permissionService;
    private readonly ISettingService _settingService;
    private readonly IWebHelper _webHelper;

    #endregion

    #region Ctor

    public SSOAuthenticationMethod(
        ILocalizationService localizationService,
        IPermissionService permissionService,
        ISettingService settingService,
        IWebHelper webHelper)
    {
        _localizationService = localizationService;
        _permissionService = permissionService;
        _settingService = settingService;
        _webHelper = webHelper;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Returns the URL of the plugin's configuration page in the admin area.
    /// </summary>
    /// <remarks>
    /// The target controller is delivered in M2.b; clicking <em>Configure</em> before then
    /// will return a 404. That is expected during the milestone walkthrough.
    /// </remarks>
    public override string GetConfigurationPageUrl()
        => $"{_webHelper.GetStoreLocation()}Admin/ExternalAuthSSO/Configure";

    /// <summary>
    /// Public view component used by nopCommerce to render the "Sign in with Microsoft"
    /// button on the standard login page. Returns empty content when SSO is disabled or
    /// the protocol is set to <c>Off</c> for the current store (see
    /// <see cref="SSOAuthenticationViewComponent"/>).
    /// </summary>
    public Type GetPublicViewComponent()
        => typeof(SSOAuthenticationViewComponent);

    /// <summary>
    /// Install the plugin. Seeds default settings, registers permissions, adds locale
    /// resources, then defers to <c>base.InstallAsync</c> which runs the FluentMigrator
    /// schema migration.
    /// </summary>
    public override async Task InstallAsync()
    {
        // Default per-store config — disabled until an admin completes the Configure page.
        await _settingService.SaveSettingAsync(new SsoConfigurationSettings());

        // System-wide bypass settings — empty token disables the bypass entirely.
        await _settingService.SaveSettingAsync(new SsoSystemSettings());

        // Discover and insert any IPermissionConfigManager-declared permissions not yet in PermissionRecord.
        // This picks up ExternalAuthSSOPermissionConfigManager automatically.
        await _permissionService.InsertPermissionsAsync();

        await AddOrUpdateLocaleResourcesAsync();

        await base.InstallAsync();
    }

    /// <summary>
    /// Update the plugin. Refreshes locale resources when the target version includes a
    /// <c>DATA</c> marker (same convention as <c>OdooIntegrationPlugin</c>).
    /// </summary>
    /// <param name="currentVersion">Current installed version.</param>
    /// <param name="targetVersion">Target version being upgraded to.</param>
    public override async Task UpdateAsync(string currentVersion, string targetVersion)
    {
        if (targetVersion.Contains("DATA"))
            await AddOrUpdateLocaleResourcesAsync();

        await base.UpdateAsync(currentVersion, targetVersion);
    }

    /// <summary>
    /// Uninstall the plugin. Removes permissions, settings, locale resources, then defers
    /// to <c>base.UninstallAsync</c> which runs the reverse of the schema migration.
    /// </summary>
    public override async Task UninstallAsync()
    {
        await _permissionService.DeletePermissionAsync(ExternalAuthSSOPermissions.ACCESS_SSO_PLUGIN);
        await _permissionService.DeletePermissionAsync(ExternalAuthSSOPermissions.ACCESS_SSO_SETTINGS);

        await _settingService.DeleteSettingAsync<SsoConfigurationSettings>();
        await _settingService.DeleteSettingAsync<SsoSystemSettings>();

        await _localizationService.DeleteLocaleResourcesAsync("Plugins.ExternalAuth.SSO");

        await base.UninstallAsync();
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Install or refresh the plugin's locale resources. Extended each milestone as new
    /// fields, admin pages, and validation messages land.
    /// </summary>
    private async Task AddOrUpdateLocaleResourcesAsync()
    {
        await _localizationService.AddOrUpdateLocaleResourceAsync(
            new Dictionary<string, string>
            {
                // Plugin metadata
                ["Plugins.ExternalAuth.SSO.Description"] =
                    "Single Sign-On against Microsoft Entra ID. Supports OpenID Connect (primary) and SAML 2.0 (secondary), selectable per store.",

                // Permission display names
                ["Plugins.ExternalAuth.SSO.Permissions.AccessSsoPlugin"] = "Access plugin features",
                ["Plugins.ExternalAuth.SSO.Permissions.AccessSsoSettings"] = "Configure",

                // Admin sidebar menu
                ["Plugins.ExternalAuth.SSO.Menu.Root"] = "SSO Authentication",
                ["Plugins.ExternalAuth.SSO.Menu.Configure"] = "Configure",

                // Public login button + flow messages
                ["Plugins.ExternalAuth.SSO.Button.SignInWithMicrosoft"] = "Sign in with Microsoft",
                ["Plugins.ExternalAuth.SSO.Login.NotAvailable"] = "Microsoft sign-in is not available on this store. Please use the standard login form.",
                ["Plugins.ExternalAuth.SSO.Login.SamlNotYetAvailable"] = "SAML 2.0 sign-in is configured but the SAML protocol implementation is not yet enabled in this build.",
                ["Plugins.ExternalAuth.SSO.Login.AdminAccountBlocked"] = "This account is administrative and cannot be linked through SSO. Please sign in with the standard login form.",
                ["Plugins.ExternalAuth.SSO.Login.DomainNotAllowed"] = "Your email domain is not permitted to sign in to this store.",
                ["Plugins.ExternalAuth.SSO.Login.AccessDenied"] = "Access denied — this account is not in a role permitted to bypass SSO. Please use the standard SSO sign-in.",

                // SAML-specific (M6)
                ["Plugins.ExternalAuth.SSO.Saml.ConfigurationIncomplete"] = "SAML configuration is incomplete or invalid: {0}",
                ["Plugins.ExternalAuth.SSO.Saml.SignatureFailed"] = "The SAML response signature could not be verified.",
                ["Plugins.ExternalAuth.SSO.Saml.AudienceMismatch"] = "The SAML response audience does not match the configured Service Provider entity ID.",
                ["Plugins.ExternalAuth.SSO.Saml.InResponseToMismatch"] = "The SAML response correlation ID did not match a pending AuthnRequest. Please try again.",

                // M11 — rate-cap setting + notification
                ["Plugins.ExternalAuth.SSO.Fields.LoginAttemptCapPerHour"] = "Login attempts per hour (per IP)",
                ["Plugins.ExternalAuth.SSO.Fields.LoginAttemptCapPerHour.Hint"] = "Maximum SSO login challenges allowed from a single client IP in a sliding hour window. Set to 0 to disable the cap. Default 60.",
                ["Plugins.ExternalAuth.SSO.Login.RateLimited"] = "Too many SSO login attempts from your address. Please wait a few minutes and try again.",

                // M9 — onboarding report button
                ["Plugins.ExternalAuth.SSO.OnboardingReport.DownloadButton"] = "Download onboarding report",

                // M3 — Group → role mapping admin
                ["Plugins.ExternalAuth.SSO.Menu.GroupRoleMappings"] = "Group → role mappings",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.PageTitle"] = "Group → role mappings",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Intro.Title"] = "About group → role mappings",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Intro.Body"] = "<p class=\"mb-2\">Map identity-provider groups to nopCommerce customer roles so SSO sign-ins grant the right access automatically. On every successful SSO login the plugin reads the asserted <strong>groups</strong> claim, looks each group up here, and updates the customer's nop roles to match.</p><ul class=\"mb-2\"><li>The plugin only manages roles it &quot;owns&quot; — roles that appear in any active mapping below plus the per-store <strong>Default Role</strong>. Roles you've assigned manually outside that set stay untouched.</li><li>Privileged roles (<strong>Administrators</strong> are <strong>never</strong> granted via SSO, regardless of mappings.</li><li>Mappings are <strong>per-store</strong>. The same IdP group can resolve to different nop roles in different stores — use the filter above to switch scopes.</li><li><strong>Group ID</strong> is the IdP's stable identifier — for Microsoft Entra, the group's Object ID (GUID). Display names change; Object IDs don't.</li></ul><p class=\"mb-0\">Set the per-store <em>Default Role</em> and <em>Refresh roles on login</em> options on the Configure page. Use <em>Active</em> below to disable a mapping without deleting it.</p>",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.FiltersTitle"] = "Filters",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.AddTitle"] = "Add group → role mapping",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.EditTitle"] = "Edit group → role mapping",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.AddNewButton"] = "Add new",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.BackButton"] = "Back",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Saved"] = "Mapping saved.",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Deleted"] = "Mapping deleted.",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Search.Store"] = "Store",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Columns.DisplayOrder"] = "Order",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Columns.Store"] = "Store",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Columns.GroupName"] = "Group display name",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Columns.GroupId"] = "Group ID (Entra Object ID)",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Columns.Role"] = "Customer role",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Columns.IsActive"] = "Active",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.Store"] = "Store",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId"] = "Entra group Object ID",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId.Hint"] = "Stable group identifier from the IdP. For Microsoft Entra, the group Object ID (GUID format). Display names change; Object IDs don't.",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId.Required"] = "Group ID is required.",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId.Invalid"] = "Group ID must be a GUID (00000000-0000-0000-0000-000000000000).",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId.Duplicate"] = "A mapping with this group ID already exists for the selected store.",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupDisplayName"] = "Group display name",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupDisplayName.Hint"] = "Human-readable name shown in this grid. Not used for matching — Object ID drives the lookup.",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupDisplayName.Required"] = "Display name is required.",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.CustomerRole"] = "Customer role",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.CustomerRole.Hint"] = "Role granted to customers who arrive with this group in their SSO claims. Administrators are never grantable via SSO and are hidden from the list.",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.CustomerRole.Required"] = "Pick a customer role.",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.CustomerRole.Privileged"] = "Administrators cannot be granted via SSO.",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.DisplayOrder"] = "Display order",
                ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IsActive"] = "Active",

                // M12 — SSO Accounts admin
                ["Plugins.ExternalAuth.SSO.Menu.SsoAccounts"] = "SSO accounts",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.PageTitle"] = "SSO accounts",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.AddNewButton"] = "Add new",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.ConfigureButton"] = "Configure (per-store)",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.TestButton"] = "Test",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.EditButton"] = "Edit",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.DeleteButton"] = "Delete",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.DeleteConfirm"] = "Delete every SSO setting for this store? The store will revert to local-only login. System-wide settings (bypass token, allowed roles, audit logging) are NOT affected. This cannot be undone.",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Deleted"] = "SSO configuration deleted. {0} setting row(s) removed for this store.",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.DeleteError"] = "Could not delete the SSO configuration. Please try again.",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Columns.StoreName"] = "Store name",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Columns.Protocol"] = "Protocol",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Columns.Enabled"] = "On",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Columns.DisplayOrder"] = "Order",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Columns.DateSetup"] = "Date setup",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Columns.NumberOfUsers"] = "Users",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Columns.LastSuccessfulLogin"] = "Last successful login",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Columns.CertificateNotAfter"] = "Cert expires",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Cert.NearExpiryTooltip"] = "Certificate expires within 30 days — plan a rotation with the IdP team.",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Cert.ExpiredTooltip"] = "Certificate has expired — SAML signature validation is currently failing for this store.",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Columns.Actions"] = "Actions",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Add.PageTitle"] = "Add SSO account",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Add.Hint"] = "Pick a store from the list. Stores already configured do not appear. The next page is the per-store Configure form pre-scoped to your choice — set Protocol + the IdP fields and save.",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Add.AllStoresConfigured"] = "Every store already has an SSO configuration. Use the SSO Accounts list to edit them.",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Add.StoreLabel"] = "Store",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Add.ContinueButton"] = "Continue to Configure",
                ["Plugins.ExternalAuth.SSO.SsoAccounts.Add.BackButton"] = "Back to list",
                ["Plugins.ExternalAuth.SSO.Fields.DisplayOrder"] = "Display order",
                ["Plugins.ExternalAuth.SSO.Fields.DisplayOrder.Hint"] = "Sort key for the SSO Accounts list. Lower numbers appear first. Defaults to 0.",

                // M10 — audit log admin
                ["Plugins.ExternalAuth.SSO.Menu.AuditLog"] = "Audit log",
                ["Plugins.ExternalAuth.SSO.AuditLog.PageTitle"] = "SSO audit log",
                ["Plugins.ExternalAuth.SSO.AuditLog.FiltersTitle"] = "Filters",
                ["Plugins.ExternalAuth.SSO.AuditLog.ClearButton"] = "Clear audit log",
                ["Plugins.ExternalAuth.SSO.AuditLog.ClearConfirm"] = "Permanently delete ALL SSO audit log entries? A single LogCleared breadcrumb row will be written to record this action. This cannot be undone.",
                ["Plugins.ExternalAuth.SSO.AuditLog.Cleared"] = "Audit log cleared. {0} row(s) deleted; one LogCleared breadcrumb retained.",
                ["Plugins.ExternalAuth.SSO.AuditLog.BackToConfigure"] = "Back to configuration",
                ["Plugins.ExternalAuth.SSO.AuditLog.Search.Store"] = "Store",
                ["Plugins.ExternalAuth.SSO.AuditLog.Search.EventType"] = "Event type",
                ["Plugins.ExternalAuth.SSO.AuditLog.Search.Protocol"] = "Protocol",
                ["Plugins.ExternalAuth.SSO.AuditLog.Search.Email"] = "Customer email contains",
                ["Plugins.ExternalAuth.SSO.AuditLog.Search.OccurredFrom"] = "Occurred from (UTC)",
                ["Plugins.ExternalAuth.SSO.AuditLog.Search.OccurredTo"] = "Occurred to (UTC)",
                ["Plugins.ExternalAuth.SSO.AuditLog.Columns.OccurredOn"] = "Occurred on",
                ["Plugins.ExternalAuth.SSO.AuditLog.Columns.EventType"] = "Event",
                ["Plugins.ExternalAuth.SSO.AuditLog.Columns.Protocol"] = "Protocol",
                ["Plugins.ExternalAuth.SSO.AuditLog.Columns.Store"] = "Store",
                ["Plugins.ExternalAuth.SSO.AuditLog.Columns.Email"] = "Email",
                ["Plugins.ExternalAuth.SSO.AuditLog.Columns.IdpSubject"] = "IdP subject",
                ["Plugins.ExternalAuth.SSO.AuditLog.Columns.IpAddress"] = "IP",
                ["Plugins.ExternalAuth.SSO.AuditLog.Columns.Notes"] = "Notes",

                // M7 — IdP metadata import (Configure page)
                ["Plugins.ExternalAuth.SSO.IdpMetadata.Title"] = "Import IdP metadata",
                ["Plugins.ExternalAuth.SSO.IdpMetadata.Hint"] = "Paste the IdP's federation-metadata URL OR upload its XML file. The IdP entity ID, SSO URL, SLO URL, and signing certificate fields below will be prefilled. Review and click Save to commit.",
                ["Plugins.ExternalAuth.SSO.IdpMetadata.UrlLabel"] = "Metadata URL",
                ["Plugins.ExternalAuth.SSO.IdpMetadata.FileLabel"] = "Or upload XML",
                ["Plugins.ExternalAuth.SSO.IdpMetadata.ImportButton"] = "Import",
                ["Plugins.ExternalAuth.SSO.IdpMetadata.Importing"] = "Importing...",
                ["Plugins.ExternalAuth.SSO.IdpMetadata.Imported"] = "IdP fields populated from the metadata. Review the values below and click Save.",
                ["Plugins.ExternalAuth.SSO.IdpMetadata.Error.NoInput"] = "Provide either a metadata URL or an XML file to import.",
                ["Plugins.ExternalAuth.SSO.IdpMetadata.Error.Generic"] = "Could not import the IdP metadata.",

                // Common placeholders shown next to encrypted fields
                ["Plugins.ExternalAuth.SSO.Common.SecretIsSet"] = "(a value is stored — leave empty to keep it)",
                ["Plugins.ExternalAuth.SSO.Common.SecretNotSet"] = "(no value stored)",

                // Configure page cards
                ["Plugins.ExternalAuth.SSO.Cards.General"] = "General",
                ["Plugins.ExternalAuth.SSO.Cards.OpenIdConnect"] = "OpenID Connect",
                ["Plugins.ExternalAuth.SSO.Cards.Saml"] = "SAML 2.0",
                ["Plugins.ExternalAuth.SSO.Cards.ClaimMapping"] = "Claim Mapping",
                ["Plugins.ExternalAuth.SSO.Cards.UserProvisioning"] = "User Provisioning",
                ["Plugins.ExternalAuth.SSO.Cards.Redirects"] = "Bindings & Redirects",
                ["Plugins.ExternalAuth.SSO.Cards.System"] = "System-wide (all stores)",
                ["Plugins.ExternalAuth.SSO.Cards.System.Hint"] =
                    "These settings are not per-store. They apply across the whole installation.",

                // M13 — card titles (legacy parity)
                ["Plugins.ExternalAuth.SSO.Cards.IdentityProvider"] = "Identity Provider (Azure AD / Entra)",
                ["Plugins.ExternalAuth.SSO.Cards.IdentityProvider.SloNotConfigured"] =
                    "Single Log-Out (SLO) is not configured. Users will only be signed out of NopCommerce, not from Azure AD.",
                ["Plugins.ExternalAuth.SSO.Cards.SendToIdpTeam"] = "Send these to your IdP team",
                ["Plugins.ExternalAuth.SSO.Cards.SendToIdpTeam.Hint"] =
                    "One-click report for the customer setting up their Identity Provider (Entra / Azure AD, ADFS, Okta, etc.). Section 1 lists the values they need to paste into their IdP. Section 2 lists the values we need them to send back so we can finish the wiring on this side. The report is rebuilt from the currently saved settings; save the form first if you have just made changes.",
                ["Plugins.ExternalAuth.SSO.Cards.ServiceProvider"] = "Service Provider (This Application)",
                ["Plugins.ExternalAuth.SSO.Cards.BindingsRedirects"] = "Bindings & Redirects",
                ["Plugins.ExternalAuth.SSO.Cards.CustomAttributes"] = "Custom Attributes",
                ["Plugins.ExternalAuth.SSO.Cards.LoginBypass"] = "Login Bypass (System-wide)",
                ["Plugins.ExternalAuth.SSO.Cards.LoginBypass.WarningTitle"] = "System-wide settings.",
                ["Plugins.ExternalAuth.SSO.Cards.LoginBypass.WarningBody"] =
                    "These values are shared across every store. They control how an administrator can reach the standard NopCommerce login form when SSO is enabled (for example if Azure AD is unreachable). The bypass token is a free-text value used as a query string at <code>/login?ssobypass=&lt;token&gt;</code>; it is stored encrypted at rest and rotates when an admin changes it. Customers attempting bypass access without a role in <em>Allowed Local Login Roles</em> are denied with a generic message and each attempt is recorded in the SSO Audit Log.",
                ["Plugins.ExternalAuth.SSO.Cards.AuditLogging"] = "Audit Logging (System-wide)",
                ["Plugins.ExternalAuth.SSO.Cards.AuditLogging.InfoBody"] =
                    "Master switch for the SSO Audit Log. When <strong>off</strong>, no new events are written to the audit table for any store — SAML / OIDC login flows and admin-initiated certificate changes both stop producing rows. Existing rows remain visible on the Audit Log page until cleared. Clearing the log always records a single <code>LogCleared</code> breadcrumb regardless of this setting.",
                ["Plugins.ExternalAuth.SSO.Cards.TestConnection"] = "Test SSO Connection",
                ["Plugins.ExternalAuth.SSO.Cards.TestConnection.Hint"] =
                    "Validates the IdP certificate, checks IdP URL reachability, and verifies SP configuration. Save your configuration before testing.",
                ["Plugins.ExternalAuth.SSO.Cards.TestConnection.Button"] = "Test SSO Connection",
                ["Plugins.ExternalAuth.SSO.Cards.TestConnection.Running"] = "Running test...",

                // M14 — IdP metadata handshake (rendered inline under the test-result banner)
                ["Plugins.ExternalAuth.SSO.Cards.TestConnection.HandshakeTitle"] = "Metadata handshake",
                ["Plugins.ExternalAuth.SSO.Cards.TestConnection.HandshakeSkipped"] = "Handshake skipped",
                ["Plugins.ExternalAuth.SSO.Cards.TestConnection.MetadataThumbprints"] = "Metadata signing-cert thumbprints (SHA-256)",
                ["Plugins.ExternalAuth.SSO.Cards.TestConnection.Check.EntityId"] = "IdP EntityID matches metadata",
                ["Plugins.ExternalAuth.SSO.Cards.TestConnection.Check.SsoUrl"] = "IdP SSO URL matches metadata",
                ["Plugins.ExternalAuth.SSO.Cards.TestConnection.Check.SloUrl"] = "IdP SLO URL matches metadata (or not configured)",
                ["Plugins.ExternalAuth.SSO.Cards.TestConnection.Check.Certificate"] = "Stored cert SHA-256 thumbprint advertised by IdP",

                // M13 — new field labels + hints
                ["Plugins.ExternalAuth.SSO.Fields.IdpDisplayName"] = "IdP Display Name",
                ["Plugins.ExternalAuth.SSO.Fields.IdpDisplayName.Hint"] = "Friendly name shown on the public-facing sign-in button (\"Sign in with ...\") and in the onboarding report header. Defaults to \"Microsoft\" when empty.",
                ["Plugins.ExternalAuth.SSO.Fields.SignLogoutRequest"] = "Sign LogoutRequest",
                ["Plugins.ExternalAuth.SSO.Fields.SignLogoutRequest.Hint"] = "Sign outbound LogoutRequest messages with the SP PFX. Independent of Sign AuthnRequest so a tenant can require signing on one but not the other.",
                ["Plugins.ExternalAuth.SSO.Fields.RedirectLoginToSso"] = "Redirect Login to SSO",
                ["Plugins.ExternalAuth.SSO.Fields.RedirectLoginToSso.Hint"] = "When checked (default), the storefront /login page is redirected to the SSO provider once SSO is enabled. Uncheck to keep users on the standard local login form (equivalent to the bypass token path) without disabling SSO entirely.",
                ["Plugins.ExternalAuth.SSO.Fields.CustomAttributesJson"] = "Custom Attributes (JSON)",
                ["Plugins.ExternalAuth.SSO.Fields.CustomAttributesJson.Hint"] = "Free-form JSON for extensibility — consumed by plugin extensions that need per-store configuration we do not model explicitly. Leave empty unless an extension you have installed needs it.",
                ["Plugins.ExternalAuth.SSO.Fields.EnableAuditLogging"] = "Enable Audit Logging",
                ["Plugins.ExternalAuth.SSO.Fields.EnableAuditLogging.Hint"] = "Master switch for the SSO Audit Log. When off, new events are not recorded. LogCleared breadcrumbs are always written regardless.",
                ["Plugins.ExternalAuth.SSO.Fields.SpMetadataUrl"] = "SP Metadata URL",
                ["Plugins.ExternalAuth.SSO.Fields.SpMetadataUrl.Hint"] = "Standard SAML 2.0 SP metadata XML. Most IdPs let you import it instead of typing the values above by hand.",

                // OIDC quick-reference URLs (Send to IdP team card, OIDC protocol).
                // These are the two values the Entra admin must paste into the app
                // registration's Authentication blade before sign-in will work.
                ["Plugins.ExternalAuth.SSO.Fields.OidcRedirectUri"] = "Redirect URI (Reply URL)",
                ["Plugins.ExternalAuth.SSO.Fields.OidcRedirectUri.Hint"] = "Send this to the IdP team. In Entra: App registrations → (your app) → Authentication → Web → Redirect URIs. Computed from Base URL + Callback path. Save the form after changing either to refresh this value.",
                ["Plugins.ExternalAuth.SSO.Fields.OidcPostLogoutRedirectUri"] = "Post-logout Redirect URI",
                ["Plugins.ExternalAuth.SSO.Fields.OidcPostLogoutRedirectUri.Hint"] = "Send this to the IdP team. In Entra: same Authentication page → Front-channel logout URL. Computed from Base URL + /signout-callback-microsoft.",

                // M13 — onboarding-report embedded UI strings
                ["Plugins.ExternalAuth.SSO.OnboardingReport.Label"] = "Onboarding report",
                ["Plugins.ExternalAuth.SSO.OnboardingReport.RefreshButton"] = "Refresh",
                ["Plugins.ExternalAuth.SSO.OnboardingReport.CopyButton"] = "Copy to clipboard",
                ["Plugins.ExternalAuth.SSO.OnboardingReport.Loading"] = "Loading...",
                ["Plugins.ExternalAuth.SSO.OnboardingReport.Error"] = "Could not load the onboarding report.",
                ["Plugins.ExternalAuth.SSO.OnboardingReport.Hint"] = "Paste this text into an email to the customer. The Refresh button rebuilds the report after you save changes elsewhere on this page.",

                // M13 — public button template "Sign in with {0}". {0} = IdP Display Name or "Microsoft"
                ["Plugins.ExternalAuth.SSO.Button.SignInWith"] = "Sign in with {0}",

                // M13 — common micro-UX strings
                ["Plugins.ExternalAuth.SSO.Common.ClearStoredOnSave"] = "Clear stored value on save",
                ["Plugins.ExternalAuth.SSO.Common.ClearStoredIdpCertOnSave"] = "Clear stored IdP certificate on save",

                // M13 — IdP metadata import progress text (referenced by Configure JS)
                ["Plugins.ExternalAuth.SSO.IdpMetadata.Importing"] = "Importing...",

                // General fields
                ["Plugins.ExternalAuth.SSO.Fields.Protocol"] = "Protocol",
                ["Plugins.ExternalAuth.SSO.Fields.Protocol.Hint"] = "Pick which protocol this store uses to authenticate against Microsoft Entra ID. Off disables the SSO button entirely.",
                ["Plugins.ExternalAuth.SSO.Fields.IsEnabled"] = "Enabled",
                ["Plugins.ExternalAuth.SSO.Fields.IsEnabled.Hint"] = "Master switch. When off, the public login button is hidden and the auth filter does not redirect.",
                ["Plugins.ExternalAuth.SSO.Fields.BaseUrl"] = "Base URL",
                ["Plugins.ExternalAuth.SSO.Fields.BaseUrl.Hint"] = "Absolute base URL of the store (e.g. https://shop.example.com). Used to construct OIDC and SAML callback URLs.",
                ["Plugins.ExternalAuth.SSO.Fields.BaseUrl.Required"] = "Store base URL is required when SSO is enabled.",
                ["Plugins.ExternalAuth.SSO.Fields.BaseUrl.Invalid"] = "Store base URL must be an absolute http(s) URL.",

                // OIDC fields
                ["Plugins.ExternalAuth.SSO.Fields.Authority"] = "Authority",
                ["Plugins.ExternalAuth.SSO.Fields.Authority.Hint"] = "OIDC discovery authority — e.g. https://login.microsoftonline.com/<tenant-id>/v2.0.",
                ["Plugins.ExternalAuth.SSO.Fields.Authority.Required"] = "Authority is required when Protocol = OpenID Connect.",
                ["Plugins.ExternalAuth.SSO.Fields.Authority.Invalid"] = "Authority must be an absolute http(s) URL.",
                ["Plugins.ExternalAuth.SSO.Fields.ClientId"] = "Client ID",
                ["Plugins.ExternalAuth.SSO.Fields.ClientId.Hint"] = "Application (client) ID from the Entra app registration.",
                ["Plugins.ExternalAuth.SSO.Fields.ClientId.Required"] = "Client ID is required when Protocol = OpenID Connect.",
                ["Plugins.ExternalAuth.SSO.Fields.ClientSecret"] = "Client secret",
                ["Plugins.ExternalAuth.SSO.Fields.ClientSecret.Hint"] = "Client secret from the Entra app registration. Stored encrypted at rest. Leave blank to keep the existing value.",
                ["Plugins.ExternalAuth.SSO.Fields.ClientSecret.Required"] = "Client secret is required when Protocol = OpenID Connect.",
                ["Plugins.ExternalAuth.SSO.Fields.CallbackPath"] = "Callback path",
                ["Plugins.ExternalAuth.SSO.Fields.CallbackPath.Hint"] = "Path on this store that handles the OIDC redirect. Default: /signin-microsoft.",
                ["Plugins.ExternalAuth.SSO.Fields.Scopes"] = "Scopes",
                ["Plugins.ExternalAuth.SSO.Fields.Scopes.Hint"] = "Space-delimited OIDC scopes. Default: openid profile email.",

                // SAML fields
                ["Plugins.ExternalAuth.SSO.Fields.IdpEntityId"] = "IdP entity ID",
                ["Plugins.ExternalAuth.SSO.Fields.IdpEntityId.Hint"] = "EntityID published by the IdP (e.g. https://sts.windows.net/<tenant>/).",
                ["Plugins.ExternalAuth.SSO.Fields.IdpEntityId.Required"] = "IdP entity ID is required when Protocol = SAML 2.0.",
                ["Plugins.ExternalAuth.SSO.Fields.IdpSsoUrl"] = "IdP SSO URL",
                ["Plugins.ExternalAuth.SSO.Fields.IdpSsoUrl.Hint"] = "The IdP's HTTP-Redirect or HTTP-POST single sign-on endpoint.",
                ["Plugins.ExternalAuth.SSO.Fields.IdpSsoUrl.Required"] = "IdP SSO URL is required when Protocol = SAML 2.0.",
                ["Plugins.ExternalAuth.SSO.Fields.IdpSsoUrl.Invalid"] = "IdP SSO URL must be an absolute http(s) URL.",
                ["Plugins.ExternalAuth.SSO.Fields.IdpSloUrl"] = "IdP SLO URL",
                ["Plugins.ExternalAuth.SSO.Fields.IdpSloUrl.Hint"] = "Optional — IdP single log-out endpoint. SLO is disabled when empty.",
                ["Plugins.ExternalAuth.SSO.Fields.IdpSloUrl.Invalid"] = "IdP SLO URL must be an absolute http(s) URL.",
                ["Plugins.ExternalAuth.SSO.Fields.IdpCertificateBase64"] = "IdP signing certificate (Base64 DER)",
                ["Plugins.ExternalAuth.SSO.Fields.IdpCertificateBase64.Hint"] = "X.509 signing certificate as Base64 DER (no PEM headers). Stored encrypted at rest. Leave blank to keep the existing value.",
                ["Plugins.ExternalAuth.SSO.Fields.IdpCertificateBase64.Required"] = "IdP signing certificate is required when Protocol = SAML 2.0.",
                ["Plugins.ExternalAuth.SSO.Fields.IdpMetadataUrl"] = "IdP metadata URL (optional)",
                ["Plugins.ExternalAuth.SSO.Fields.IdpMetadataUrl.Hint"] = "If supplied, IdP fields above are auto-refreshed from this federation metadata XML.",
                ["Plugins.ExternalAuth.SSO.Fields.IdpMetadataUrl.Invalid"] = "IdP metadata URL must be an absolute http(s) URL.",
                ["Plugins.ExternalAuth.SSO.Fields.SpEntityId"] = "SP entity ID",
                ["Plugins.ExternalAuth.SSO.Fields.SpEntityId.Hint"] = "EntityID this store presents to the IdP.",
                ["Plugins.ExternalAuth.SSO.Fields.AcsUrl"] = "SP ACS URL",
                ["Plugins.ExternalAuth.SSO.Fields.AcsUrl.Hint"] = "Assertion Consumer Service URL on this store (where the IdP POSTs SAMLResponse).",
                ["Plugins.ExternalAuth.SSO.Fields.AcsUrl.Invalid"] = "SP ACS URL must be an absolute http(s) URL.",
                ["Plugins.ExternalAuth.SSO.Fields.SpLogoutUrl"] = "SP logout URL",
                ["Plugins.ExternalAuth.SSO.Fields.SpLogoutUrl.Hint"] = "URL on this store that handles SAML logout responses.",
                ["Plugins.ExternalAuth.SSO.Fields.SpLogoutUrl.Invalid"] = "SP logout URL must be an absolute http(s) URL.",
                ["Plugins.ExternalAuth.SSO.Fields.SpPfxBase64"] = "SP PFX Certificate (Base64)",
                ["Plugins.ExternalAuth.SSO.Fields.SpPfxBase64.Hint"] = "Required when Signed Request or Encrypted Assertion is on. Stored encrypted at rest.",
                ["Plugins.ExternalAuth.SSO.Fields.SpPfxPassword"] = "SP PFX Password",
                ["Plugins.ExternalAuth.SSO.Fields.SpPfxPassword.Hint"] = "Stored encrypted at rest. Leave blank to keep the existing value.",
                ["Plugins.ExternalAuth.SSO.Fields.SignedRequest"] = "Sign AuthnRequest",
                ["Plugins.ExternalAuth.SSO.Fields.SignedRequest.Hint"] = "Sign outbound AuthnRequest messages with the SP PFX.",
                ["Plugins.ExternalAuth.SSO.Fields.EncryptedAssertion"] = "Encrypted assertion",
                ["Plugins.ExternalAuth.SSO.Fields.EncryptedAssertion.Hint"] = "Advertise WantAssertionsEncrypted in SP metadata and decrypt encrypted assertions on the ACS endpoint.",
                ["Plugins.ExternalAuth.SSO.Fields.LoginBinding"] = "Login binding",
                ["Plugins.ExternalAuth.SSO.Fields.LoginBinding.Hint"] = "SAML binding for login requests (HTTP-Redirect or HTTP-POST).",
                ["Plugins.ExternalAuth.SSO.Fields.LogoutBinding"] = "Logout binding",
                ["Plugins.ExternalAuth.SSO.Fields.LogoutBinding.Hint"] = "SAML binding for logout requests.",

                // Claim mapping fields
                ["Plugins.ExternalAuth.SSO.Fields.EmailClaimType"] = "Email claim type",
                ["Plugins.ExternalAuth.SSO.Fields.EmailClaimType.Hint"] = "Claim that carries the email address. Defaults to the standard schema URI.",
                ["Plugins.ExternalAuth.SSO.Fields.UsernameClaimType"] = "Username claim type",
                ["Plugins.ExternalAuth.SSO.Fields.UsernameClaimType.Hint"] = "Claim that carries the username. OIDC default: preferred_username.",
                ["Plugins.ExternalAuth.SSO.Fields.FirstNameClaimType"] = "First name claim type",
                ["Plugins.ExternalAuth.SSO.Fields.LastNameClaimType"] = "Last name claim type",
                ["Plugins.ExternalAuth.SSO.Fields.GroupsClaimType"] = "Group Attribute Name",
                ["Plugins.ExternalAuth.SSO.Fields.GroupsClaimType.Hint"] = "Claim that carries group membership. Entra Object IDs are expected.",

                // User provisioning fields
                ["Plugins.ExternalAuth.SSO.Fields.AutoRegister"] = "Auto-Register New Users",
                ["Plugins.ExternalAuth.SSO.Fields.AutoRegister.Hint"] = "When on, create a new nop customer for an asserted email that doesn't match an existing one.",
                ["Plugins.ExternalAuth.SSO.Fields.DefaultRoleSystemName"] = "Default Role",
                ["Plugins.ExternalAuth.SSO.Fields.DefaultRoleSystemName.Hint"] = "System name of the role assigned to auto-registered customers. Administrators are always blocked.",
                ["Plugins.ExternalAuth.SSO.Fields.AllowedEmailDomains"] = "Allowed Email Domains",
                ["Plugins.ExternalAuth.SSO.Fields.AllowedEmailDomains.Hint"] = "Comma-separated domain suffixes. Empty = all domains accepted. Wildcards supported (e.g. *.example.com).",
                ["Plugins.ExternalAuth.SSO.Fields.RefreshRolesOnLogin"] = "Refresh roles on every login",
                ["Plugins.ExternalAuth.SSO.Fields.RefreshRolesOnLogin.Hint"] = "When on, re-apply group→role mapping on every successful SSO login (within this plugin's owned role set).",

                // Redirect fields
                ["Plugins.ExternalAuth.SSO.Fields.PostLoginRedirectUrl"] = "Custom Post-Login Redirect URL",
                ["Plugins.ExternalAuth.SSO.Fields.PostLoginRedirectUrl.Hint"] = "Where to send the customer after a successful login. Must be a local URL on this store.",
                ["Plugins.ExternalAuth.SSO.Fields.PostLogoutRedirectUrl"] = "Custom Post-Logout Redirect URL",
                ["Plugins.ExternalAuth.SSO.Fields.PostLogoutRedirectUrl.Hint"] = "Where to send the customer after logout completes. Must be a local URL on this store.",

                // System-wide fields
                ["Plugins.ExternalAuth.SSO.Fields.SsoBypassToken"] = "Bypass Token",
                ["Plugins.ExternalAuth.SSO.Fields.SsoBypassToken.Hint"] = "When set, /login?ssobypass=<token> allows ops to reach the local login form even on stores where SSO is on. Stored encrypted. Empty disables the bypass entirely.",
                ["Plugins.ExternalAuth.SSO.Fields.LocalLoginAllowedRoles"] = "Allowed Local Login Roles",
                ["Plugins.ExternalAuth.SSO.Fields.LocalLoginAllowedRoles.Hint"] = "Comma-separated role system names. Only customers in at least one of these roles can complete a local login via the bypass URL.",

                // Mock Mode (DEBUG builds only). The card itself is gated by BuildInfo.IsDebugBuild
                // in the view; the resource keys are seeded unconditionally so DBs round-tripped
                // between Debug and Release dev environments stay consistent.
                ["Plugins.ExternalAuth.SSO.Cards.MockMode"] = "Mock Mode (Dev only)",
                ["Plugins.ExternalAuth.SSO.Cards.MockMode.WarningTitle"] = "Development-only feature.",
                ["Plugins.ExternalAuth.SSO.Cards.MockMode.WarningBody"] =
                    "When on, clicking the public &quot;Sign in with Microsoft&quot; button redirects to an in-plugin mock IdP page instead of Microsoft Entra ID. The mock builds a <code>ClaimsPrincipal</code> directly from form fields and feeds the same provisioning pipeline the real callback uses — so it exercises domain allowlist, group→role mapping, auto-register, and audit logging, but bypasses the real cryptographic checks. The whole mock surface (controller + view + this card) is excluded from <strong>Release</strong> builds at compile time, so flipping this on in a production deployment has no effect.",
                ["Plugins.ExternalAuth.SSO.Fields.EnableMockMode"] = "Enable Mock Mode",
                ["Plugins.ExternalAuth.SSO.Fields.EnableMockMode.Hint"] = "When checked, the Login flow redirects to /mocksso/login instead of the real IdP. Per-store toggle. Has no effect on Release builds (the mock code is #if DEBUG).",

                // Filter-dropdown "any" placeholders. Used as the first row in admin
                // grid search dropdowns (audit log + group→role mapping). Replaces the
                // legacy "*" with a readable label per dropdown.
                ["Plugins.ExternalAuth.SSO.Filters.AllStores"] = "All stores",
                ["Plugins.ExternalAuth.SSO.Filters.AllEventTypes"] = "All event types",
                ["Plugins.ExternalAuth.SSO.Filters.AllProtocols"] = "All protocols"
            });
    }

    #endregion
}

