namespace Nop.Plugin.ExternalAuth.SSO.Infrastructure;

/// <summary>
/// The plugin's English locale strings, installed on install and refreshed by every <c>DATA</c> update
/// (<see cref="SSOAuthenticationMethod"/>). A key removed from here must also be deleted in
/// <see cref="SSOAuthenticationMethod.UpdateAsync"/> — nop keeps installed strings until told otherwise.
/// </summary>
public static class SsoLocaleResources
{
    /// <summary>Every resource key the plugin uses, with its English text.</summary>
    public static Dictionary<string, string> All => new()
    {
        // Plugin metadata
        ["Plugins.ExternalAuth.SSO.Description"] =
            "Single Sign-On against Microsoft Entra ID. Supports OpenID Connect (primary) and SAML 2.0 (secondary), selectable per store.",

        // Permission display names
        ["Plugins.ExternalAuth.SSO.Permissions.AccessSsoSettings"] = "Configure",

        // Admin sidebar menu
        ["Plugins.ExternalAuth.SSO.Menu.Root"] = "SSO Authentication",
        ["Plugins.ExternalAuth.SSO.Menu.Configure"] = "Configure",

        // Public login button + flow messages
        ["Plugins.ExternalAuth.SSO.Login.NotAvailable"] = "Microsoft sign-in is not available on this store. Please use the standard login form.",
        ["Plugins.ExternalAuth.SSO.Login.AccessDenied"] = "Access denied — this account is not in a role permitted to bypass SSO. Please use the standard SSO sign-in.",

        // SAML-specific
        ["Plugins.ExternalAuth.SSO.Saml.ConfigurationIncomplete"] = "SAML configuration is incomplete or invalid: {0}",

        // rate-cap setting + notification
        ["Plugins.ExternalAuth.SSO.Fields.LoginAttemptCapPerHour"] = "Login attempts per hour (per IP)",
        ["Plugins.ExternalAuth.SSO.Fields.LoginAttemptCapPerHour.Hint"] = "Maximum SSO login challenges allowed from a single client IP in a sliding hour window. Set to 0 to disable the cap. Default 60.",
        ["Plugins.ExternalAuth.SSO.Login.RateLimited"] = "Too many SSO login attempts from your address. Please wait a few minutes and try again.",

        // onboarding report button
        ["Plugins.ExternalAuth.SSO.OnboardingReport.DownloadButton"] = "Download onboarding report",

        // Group → role mapping admin
        ["Plugins.ExternalAuth.SSO.Menu.GroupRoleMappings"] = "Group → role mappings",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.PageTitle"] = "Group → role mappings",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Intro.Title"] = "About group → role mappings",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Intro.Body"] = "<p class=\"mb-2\">Map identity-provider groups to nopCommerce customer roles so SSO sign-ins grant the right access automatically. On every successful SSO login the plugin reads the asserted <strong>groups</strong> and <strong>app roles</strong> claims, looks each value up here, and updates the customer's nop roles to match.</p><ul class=\"mb-2\"><li>The plugin only manages roles it &quot;owns&quot; — roles that appear in any active mapping below plus the per-store <strong>Default roles</strong>. Roles you've assigned manually outside that set stay untouched.</li><li>Privileged roles (<strong>Administrators</strong>) are <strong>never</strong> granted via SSO, regardless of mappings.</li><li>Mappings are <strong>per-store</strong>. The same IdP group can resolve to different nop roles in different stores — use the filter above to switch scopes.</li><li><strong>Group ID</strong> is the IdP's stable identifier — for Microsoft Entra, the group's Object ID (GUID); display names change, Object IDs don't. It can also be an <strong>app-role</strong> value (e.g. <code>Store.Manager</code>), as sent in the roles claim.</li></ul><p class=\"mb-0\">Set the per-store <em>Default roles</em> and <em>Refresh roles on login</em> options on the Configure page. Use <em>Active</em> below to disable a mapping without deleting it.</p>",
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
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Columns.GroupId"] = "Group ID or app role",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Columns.Role"] = "Customer role",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Columns.IsActive"] = "Active",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.Store"] = "Store",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId"] = "Group Object ID or app role",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId.Hint"] = "Either an Entra group Object ID (GUID — display names change, Object IDs do not) or an app-role value as sent in the roles claim (e.g. Store.Manager).",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId.Required"] = "Group Object ID or app role is required.",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId.Invalid"] = "Use a group Object ID (GUID) or an app-role value: letters, digits and . _ - : /, no spaces, up to 100 characters.",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupId.Duplicate"] = "A mapping for this group or app role already exists for the selected store.",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupDisplayName"] = "Group display name",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupDisplayName.Hint"] = "Human-readable name shown in this grid. Not used for matching — the Group ID or app role drives the lookup.",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IdpGroupDisplayName.Required"] = "Display name is required.",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.CustomerRole"] = "Customer role",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.CustomerRole.Hint"] = "Role granted to customers who arrive with this group in their SSO claims. Administrators are never grantable via SSO and are hidden from the list.",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.CustomerRole.Required"] = "Pick a customer role.",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.CustomerRole.Privileged"] = "Administrators cannot be granted via SSO.",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.DisplayOrder"] = "Display order",
        ["Plugins.ExternalAuth.SSO.GroupRoleMapping.Fields.IsActive"] = "Active",

        // SSO Accounts admin
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
        ["Plugins.ExternalAuth.SSO.SsoAccounts.Add.StoreInvalid"] = "Pick a store from the list. Only stores without an SSO configuration can be added.",
        ["Plugins.ExternalAuth.SSO.SsoAccounts.Add.ContinueButton"] = "Continue to Configure",
        ["Plugins.ExternalAuth.SSO.SsoAccounts.Add.BackButton"] = "Back to list",
        ["Plugins.ExternalAuth.SSO.Fields.DisplayOrder"] = "Display order",
        ["Plugins.ExternalAuth.SSO.Fields.DisplayOrder.Hint"] = "Sort key for the SSO Accounts list. Lower numbers appear first. Defaults to 0.",

        // audit log admin
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

        // IdP metadata import (Configure page)
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
        ["Plugins.ExternalAuth.SSO.Cards.ClaimMapping"] = "Claim Mapping",
        ["Plugins.ExternalAuth.SSO.Cards.UserProvisioning"] = "User Provisioning",

        // card titles (legacy parity)
        ["Plugins.ExternalAuth.SSO.Cards.IdentityProvider"] = "Identity Provider (Azure AD / Entra)",
        ["Plugins.ExternalAuth.SSO.Cards.IdentityProvider.SloNotConfigured"] =
            "Single Log-Out (SLO) is not configured. Users will only be signed out of NopCommerce, not from Azure AD.",
        ["Plugins.ExternalAuth.SSO.Cards.SendToIdpTeam"] = "Send these to your IdP team",
        ["Plugins.ExternalAuth.SSO.Cards.SendToIdpTeam.Hint"] =
            "One-click report for the customer setting up their Identity Provider (Entra / Azure AD, ADFS, Okta, etc.). Section 1 lists the values they need to paste into their IdP. Section 2 lists the values we need them to send back so we can finish the wiring on this side. The report is rebuilt from the currently saved settings; save the form first if you have just made changes.",
        ["Plugins.ExternalAuth.SSO.Cards.ServiceProvider"] = "Service Provider (This Application)",
        ["Plugins.ExternalAuth.SSO.Cards.BindingsRedirects"] = "Bindings & Redirects",
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

        // IdP metadata handshake (rendered inline under the test-result banner)
        ["Plugins.ExternalAuth.SSO.Cards.TestConnection.HandshakeTitle"] = "Metadata handshake",
        ["Plugins.ExternalAuth.SSO.Cards.TestConnection.HandshakeSkipped"] = "Handshake skipped",
        ["Plugins.ExternalAuth.SSO.Cards.TestConnection.MetadataThumbprints"] = "Metadata signing-cert thumbprints (SHA-256)",
        ["Plugins.ExternalAuth.SSO.Cards.TestConnection.Check.EntityId"] = "IdP EntityID matches metadata",
        ["Plugins.ExternalAuth.SSO.Cards.TestConnection.Check.SsoUrl"] = "IdP SSO URL matches metadata",
        ["Plugins.ExternalAuth.SSO.Cards.TestConnection.Check.SloUrl"] = "IdP SLO URL matches metadata (or not configured)",
        ["Plugins.ExternalAuth.SSO.Cards.TestConnection.Check.Certificate"] = "Stored cert SHA-256 thumbprint advertised by IdP",

        // new field labels + hints
        ["Plugins.ExternalAuth.SSO.Fields.IdpDisplayName"] = "IdP Display Name",
        ["Plugins.ExternalAuth.SSO.Fields.IdpDisplayName.Hint"] = "Friendly name shown on the public-facing sign-in button (\"Sign in with ...\") and in the onboarding report header. Defaults to \"Microsoft\" when empty.",
        ["Plugins.ExternalAuth.SSO.Fields.SignLogoutRequest"] = "Sign LogoutRequest",
        ["Plugins.ExternalAuth.SSO.Fields.SignLogoutRequest.Hint"] = "Sign outbound LogoutRequest messages with the SP PFX. Independent of Sign AuthnRequest so a tenant can require signing on one but not the other.",
        ["Plugins.ExternalAuth.SSO.Fields.RedirectLoginToSso"] = "Redirect Login to SSO",
        ["Plugins.ExternalAuth.SSO.Fields.RedirectLoginToSso.Hint"] = "When checked (default), the storefront /login page is redirected to the SSO provider once SSO is enabled. Uncheck to keep users on the standard local login form (equivalent to the bypass token path) without disabling SSO entirely.",
        ["Plugins.ExternalAuth.SSO.Fields.AllowIdpInitiatedLogin"] = "Allow IdP-initiated login",
        ["Plugins.ExternalAuth.SSO.Fields.AllowIdpInitiatedLogin.Hint"] = "Accept a SAML response this store did not request (for example a tile in the IdP portal). Off: only logins started from this store are accepted.",
        ["Plugins.ExternalAuth.SSO.Fields.EnableAuditLogging"] = "Enable Audit Logging",
        ["Plugins.ExternalAuth.SSO.Fields.EnableAuditLogging.Hint"] = "Master switch for the SSO Audit Log. When off, new events are not recorded. LogCleared breadcrumbs are always written regardless.",
        ["Plugins.ExternalAuth.SSO.Fields.AuditRetentionDays"] = "Audit log retention (days)",
        ["Plugins.ExternalAuth.SSO.Fields.AuditRetentionDays.Hint"] = "Rows older than this are deleted by the daily 'SSO audit log retention' scheduled task. 0 keeps them forever.",
        ["Plugins.ExternalAuth.SSO.Fields.AuditRetentionDays.Invalid"] = "Retention must be 0 (keep forever) or a positive number of days.",
        ["Plugins.ExternalAuth.SSO.Fields.CertificateWarningDays"] = "Warn about expiry (days ahead)",
        ["Plugins.ExternalAuth.SSO.Fields.CertificateWarningDays.Hint"] = "System → Warnings starts warning this many days before an IdP certificate, SP certificate or OIDC client secret expires. Expired ones are always reported. Default 30.",
        ["Plugins.ExternalAuth.SSO.Fields.CertificateWarningDays.Invalid"] = "Enter a number of days from 1 to 365.",
        ["Plugins.ExternalAuth.SSO.Fields.ClientSecretExpiresOnUtc"] = "Client secret expires on",
        ["Plugins.ExternalAuth.SSO.Fields.ClientSecretExpiresOnUtc.Hint"] = "Optional. The expiry date shown for this client secret on the IdP's app registration (Entra: Certificates & secrets). System → Warnings warns before it expires. Leave empty to not track it.",
        ["Plugins.ExternalAuth.SSO.Fields.AutoApplyIdpMetadata"] = "Apply metadata changes automatically",
        ["Plugins.ExternalAuth.SSO.Fields.AutoApplyIdpMetadata.Hint"] = "When the daily SSO check finds that the IdP's metadata (entity ID, SSO / SLO URLs, signing certificate) differs from the values above, write the IdP's values here — so a certificate rollover does not break sign-in. Off: the difference is only reported under System → Warnings. The metadata's signature is not verified; turn this on only for an IdP you trust, such as Microsoft Entra.",
        ["Plugins.ExternalAuth.SSO.Fields.IdpMetadataCheck.Last"] = "Last metadata check: {0} —",
        ["Plugins.ExternalAuth.SSO.Fields.IdpMetadataCheck.Matches"] = "the IdP's metadata matches.",
        ["Plugins.ExternalAuth.SSO.Fields.IdpMetadataCheck.Differs"] = "{0}.",
        ["Plugins.ExternalAuth.SSO.Warnings.IdpCertificateExpired"] = "SSO, store {0}: the IdP signing certificate expired on {1}. SAML sign-in fails until a current certificate is saved on the SSO Configure page.",
        ["Plugins.ExternalAuth.SSO.Warnings.IdpCertificateExpiring"] = "SSO, store {0}: the IdP signing certificate expires on {1} (in {2} days). Get the new certificate from the IdP and save it on the SSO Configure page, or import the IdP metadata.",
        ["Plugins.ExternalAuth.SSO.Warnings.IdpCertificateUnreadable"] = "SSO, store {0}: the stored IdP signing certificate cannot be read ({1}). Save it again on the SSO Configure page.",
        ["Plugins.ExternalAuth.SSO.Warnings.SpCertificateExpired"] = "SSO, store {0}: the SP certificate (PFX) expired on {1}. Signed requests and encrypted assertions fail until a new PFX is saved and sent to the IdP.",
        ["Plugins.ExternalAuth.SSO.Warnings.SpCertificateExpiring"] = "SSO, store {0}: the SP certificate (PFX) expires on {1} (in {2} days). Create a new one, save it on the SSO Configure page and send the new SP metadata to the IdP.",
        ["Plugins.ExternalAuth.SSO.Warnings.SpCertificateUnreadable"] = "SSO, store {0}: the stored SP certificate (PFX) cannot be read ({1}). Save it and its password again on the SSO Configure page.",
        ["Plugins.ExternalAuth.SSO.Warnings.ClientSecretExpired"] = "SSO, store {0}: the OIDC client secret expired on {1}. Sign-in fails until a new secret is created at the IdP and saved on the SSO Configure page.",
        ["Plugins.ExternalAuth.SSO.Warnings.ClientSecretExpiring"] = "SSO, store {0}: the OIDC client secret expires on {1} (in {2} days). Create a new secret at the IdP, save it on the SSO Configure page and update its expiry date.",
        ["Plugins.ExternalAuth.SSO.Warnings.MetadataDiffers"] = "SSO, store {0}: the IdP's published metadata differs from the stored settings — {1} (checked {2}). Apply it with Import IdP metadata on the SSO Configure page, or turn on automatic apply.",
        ["Plugins.ExternalAuth.SSO.Fields.SpMetadataUrl"] = "SP Metadata URL",
        ["Plugins.ExternalAuth.SSO.Fields.SpMetadataUrl.Hint"] = "Standard SAML 2.0 SP metadata XML. Most IdPs let you import it instead of typing the values above by hand.",

        // OIDC quick-reference URLs (Send to IdP team card, OIDC protocol).
        // These are the two values the Entra admin must paste into the app
        // registration's Authentication blade before sign-in will work.
        ["Plugins.ExternalAuth.SSO.Fields.OidcRedirectUri"] = "Redirect URI (Reply URL)",
        ["Plugins.ExternalAuth.SSO.Fields.OidcRedirectUri.Hint"] = "Send this to the IdP team. In Entra: App registrations → (your app) → Authentication → Web → Redirect URIs. Computed from Base URL + Callback path. Save the form after changing either to refresh this value.",
        ["Plugins.ExternalAuth.SSO.Fields.OidcPostLogoutRedirectUri"] = "Post-logout Redirect URI",
        ["Plugins.ExternalAuth.SSO.Fields.OidcPostLogoutRedirectUri.Hint"] = "Send this to the IdP team. In Entra: same Authentication page → Front-channel logout URL. Computed from Base URL + /signout-callback-microsoft.",

        // onboarding-report embedded UI strings
        ["Plugins.ExternalAuth.SSO.OnboardingReport.Label"] = "Onboarding report",
        ["Plugins.ExternalAuth.SSO.OnboardingReport.RefreshButton"] = "Refresh",
        ["Plugins.ExternalAuth.SSO.OnboardingReport.CopyButton"] = "Copy to clipboard",
        ["Plugins.ExternalAuth.SSO.OnboardingReport.Loading"] = "Loading...",
        ["Plugins.ExternalAuth.SSO.OnboardingReport.Error"] = "Could not load the onboarding report.",
        ["Plugins.ExternalAuth.SSO.OnboardingReport.Hint"] = "Paste this text into an email to the customer. The Refresh button rebuilds the report after you save changes elsewhere on this page.",

        // public button template "Sign in with {0}". {0} = IdP Display Name or "Microsoft"
        ["Plugins.ExternalAuth.SSO.Button.SignInWith"] = "Sign in with {0}",

        // common micro-UX strings
        ["Plugins.ExternalAuth.SSO.Common.ClearStoredOnSave"] = "Clear stored value on save",
        ["Plugins.ExternalAuth.SSO.Common.ClearStoredIdpCertOnSave"] = "Clear stored IdP certificate on save",

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
        ["Plugins.ExternalAuth.SSO.Fields.IdpMetadataUrl.Hint"] = "Optional. The IdP's federation metadata URL. Test SSO Connection reads it to check that the IdP entity ID, URLs and signing certificate above still match what the IdP publishes. Nothing is refreshed automatically: to update the fields from metadata, use Import IdP metadata.",
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
        ["Plugins.ExternalAuth.SSO.Fields.RolesClaimType"] = "App Roles Attribute Name",
        ["Plugins.ExternalAuth.SSO.Fields.RolesClaimType.Hint"] = "Claim that carries app roles (Entra: roles). Its values are matched by the group → role mappings like group IDs. Leave empty to ignore app roles.",

        // User provisioning fields
        ["Plugins.ExternalAuth.SSO.Fields.AutoRegister"] = "Auto-Register New Users",
        ["Plugins.ExternalAuth.SSO.Fields.AutoRegister.Hint"] = "When on, create a new nop customer for an asserted email that doesn't match an existing one.",
        ["Plugins.ExternalAuth.SSO.Fields.DefaultRoles"] = "Default roles",
        ["Plugins.ExternalAuth.SSO.Fields.DefaultRoles.Hint"] = "Roles every SSO customer of this store gets, kept in step on each login together with the group mappings. Administrators are never offered or applied.",
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
    };
}
