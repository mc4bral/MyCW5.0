# SSO Authentication plugin — Reference

| | |
|---|---|
| **Project** | `Nop.Plugin.ExternalAuth.SSO` |
| **SystemName** | `ExternalAuth.SSO` · Group `ExternalAuth methods` · FriendlyName *SSO Authentication* |
| **Host** | nopCommerce **5.00**, .NET **10** (`C:\Work\CW_NOP_5`). Ported from the 4.90 / .NET 9 build on 2026-10-19 |
| **Protocols** | OpenID Connect (primary) and SAML 2.0 (secondary), one per store |
| **Status** | Builds and installs on 5.00; admin pages work. Gaps G1–G14 fixed 2026-10-19 (§11). **OIDC and SAML sign-in have not been tested against a real IdP on 5.00** — see §12 |
| **Coupling** | None. Generic plugin, no reference to CW.Core |

This document describes the plugin **as built**. It replaces `SSOPlan.v2.md` (the design spec, written before
implementation) and `SSO_SESSION_HANDOFF.md` (the May 2026 session notes). Where the code differs from those two
documents, this one follows the code, and §11 lists the differences.

---

## 1. What it does

- Lets a store's customers sign in with a corporate identity provider (designed around Microsoft Entra ID, but
  any standards-compliant OIDC or SAML 2.0 IdP works).
- Each store picks its own protocol and IdP on the Configure page; stores without SSO keep nop's normal login.
- Signs the customer in through nop's own external-auth pipeline (`IExternalAuthenticationService`), so account
  linking, registration, cart migration, `CustomerLoggedinEvent` and the activity log are nop's behaviour.
- Applies IdP group → nop customer role mappings on every login, inside a plugin-owned role set.
- Optionally sends `/login` straight to the IdP, with a token-protected bypass so administrators can still use the
  local form when the IdP is down.
- Federated sign-out (SAML SLO in both directions; OIDC end-session — see §11, gap G1).
- Writes an audit log of every sign-in, failure, bypass attempt, sign-out and configuration change.

---

## 2. Solution layout

```
Nop.Plugin.ExternalAuth.SSO/
├── SSOAuthenticationMethod.cs        BasePlugin + IExternalAuthenticationMethod; install/uninstall; ~260 locale strings
├── plugin.json
├── Components/                       SSOAuthenticationViewComponent (public login button),
│                                     StoreScopeSSOViewComponent + StoreScopeConfigurationModel (admin store picker)
├── Constants/                        SSOAuthenticationDefaults, SsoAuthEventTypes / SsoProtocolNames,
│                                     SsoFailureReasons, SsoCustomerAttributes, BuildInfo, Mock* (DEBUG)
├── Controllers/
│   ├── ExternalAuthenticationController.cs   public: Login (challenge) and LoginCallback (OIDC)
│   ├── SamlController.cs                     public: /saml/acs, /saml/metadata, /saml/logout
│   ├── ExternalAuthSSOController.cs          admin: every admin page and AJAX endpoint
│   └── MockSsoController.cs                  DEBUG only: /mocksso/*
├── Data/                             SSO_AuthEvent + SSO_GroupRoleMapping builders, SchemaMigration,
│                                     MigrationVersions, ExternalAuthSSONameCompatibility
├── Domain/                           SsoAuthEvent, SsoGroupRoleMapping
├── Factories/                        Configuration, AuditEvent, GroupRoleMapping, StoreSummary model factories
├── Filters/                          LoginActionsFilter, LocalLoginEnforcementFilter, LogoutActionsFilter
├── Infrastructure/                   NopStartup, OidcAuthenticationRegistrar, EventConsumer (admin menu), SsoCustomerRegisteredEventConsumer (names),
│   └── Oidc/                        ViewLocationExpander; Oidc/: per-store OIDC options (SsoOidcOptionsConfigurer,
│                                     StoreAwareOidcOptionsCache)
├── Models/                           admin view models + result records (TestConnectionResult, HandshakeResult, ...)
├── Security/                         ExternalAuthSSOPermissions, ExternalAuthSSOPermissionConfigManager
├── Services/                         provisioning, events, group→role, secrets, rate limiter, store summary,
│   └── Saml/                         test connection, onboarding report; SAML config, challenge, ACS,
│                                     metadata, logout, IdP-metadata import; Http/SafeUrl (outbound URL guard); SsoCertificates
├── Settings/                         SsoConfigurationSettings (per store), SsoSystemSettings (store 0), SsoProtocol
├── Validators/                       ConfigurationModelValidator, SsoGroupRoleMappingModelValidator
├── Views/                            Configure, SsoAccounts, AddSsoAccount, GroupRoleMappings,
│                                     CreateOrUpdateGroupRoleMapping, AuditLog, PublicInfo, MockSso/Login,
│                                     Components/StoreScopeSSO/Default
├── SAMLSSOConfig_StoreID_110.json    test-tenant sample (see §10.2)
└── miniorange.saml/                  binaries/assets of the old MiniOrange plugin — not built, not referenced
```

There is **no** `RouteProvider`: public endpoints use attribute routes (`SamlController`, `MockSsoController`) or
nop's default `{controller}/{action}` route (`ExternalAuthentication/Login`, `ExternalAuthentication/LoginCallback`).

---

## 3. Data

Both tables are created by `SchemaMigration` (`[NopMigration("2026/06/01 00:00:00", ..., Installation)]`,
`AutoReversingMigration`, so they are dropped on uninstall).

**`SSO_AuthEvent`** (`SsoAuthEvent`) — append-only audit row.

| Column | Notes |
|---|---|
| `StoreId` | store that handled the event |
| `CustomerId` | null when no customer was resolved |
| `CustomerEmail`, `IdpSubject` | from the token / assertion (`sub` or NameID) |
| `EventType` | `Login` (SAML challenge started), `AutoRegistered`, `Linked`, `SignedIn` (returning customer), `Slo`, `Failed`, `BypassDenied`, `ConfigChanged`, `LogCleared`, `HandshakeRun`. The SSO Accounts "Users" and "Last successful login" columns count `AutoRegistered`, `Linked` and `SignedIn` |
| `Protocol` | `Oidc`, `Saml2`, or empty |
| `OccurredOnUtc`, `IpAddress`, `Notes` | `Notes` carries a machine-readable reason (`SsoFailureReasons`, e.g. `acs_email_domain_blocked`) plus detail |

Indexes: `IX_SSO_AuthEvent_OccurredOnUtc`, `IX_SSO_AuthEvent_StoreId_OccurredOnUtc`. `StoreId` has **no foreign key**:
system-wide rows (an "All stores" save, `LogCleared`) use store 0, and the log must outlive a deleted store
(`DropAuditStoreForeignKeyMigration` removed the key from existing installs).

**`SSO_GroupRoleMapping`** (`SsoGroupRoleMapping`) — `StoreId`, `IdpGroupId` (an Entra group object ID **or** an
app-role value; letters, digits and `. _ : / -`, up to 100 characters), `IdpGroupDisplayName` (display only), `CustomerRoleId`, `DisplayOrder`, `IsActive`.
Index: `IX_SSO_GroupRoleMapping_StoreId_IdpGroupId`.

**Customer generic attributes** (`SsoCustomerAttributes`): `ExternalAuth.SSO.SamlNameId`,
`ExternalAuth.SSO.SamlSessionIndex` (written on a SAML sign-in) and `ExternalAuth.SSO.SignInProtocol` (`Oidc` /
`Saml2`, written by provisioning on every successful SSO sign-in) and `ExternalAuth.SSO.OidcIdToken` (the OIDC
`id_token`, written on an OIDC sign-in and sent as `id_token_hint` at logout). Read and cleared by the logout filter.

---

## 4. Settings

### 4.1 `SsoConfigurationSettings` — per store

Saved with `overrideForStore: true` for the store selected on the Configure page. There are no per-field
"override" checkboxes: picking a store *is* the override.

| Group | Properties |
|---|---|
| General | `Protocol` (`Off` / `OpenIdConnect` / `Saml2`), `IsEnabled`, `BaseUrl`, `IdpDisplayName` (login-button label, default "Microsoft"), `DisplayOrder`, `RedirectLoginToSso` (default true), `EnableMockMode` (DEBUG only) |
| OIDC | `Authority`, `ClientId`, `ClientSecret`*, `CallbackPath` (`/signin-microsoft`), `Scopes` (`openid profile email`) |
| SAML IdP | `IdpEntityId`, `IdpSsoUrl`, `IdpSloUrl`, `IdpCertificateBase64`*, `IdpMetadataUrl` |
| SAML SP | `SpEntityId`, `AcsUrl`, `SpLogoutUrl`, `SpPfxBase64`*, `SpPfxPassword`*, `SignedRequest` (sign AuthnRequest), `SignLogoutRequest` (sign logout messages, independently), `EncryptedAssertion`, `AllowIdpInitiatedLogin` (default off), `LoginBinding`, `LogoutBinding` (`SamlBinding` enum, dropdowns: HTTP-Redirect default, HTTP-POST) |
| Claims | `EmailClaimType`, `UsernameClaimType`, `FirstNameClaimType`, `LastNameClaimType`, `GroupsClaimType`, `RolesClaimType` (app roles, default `roles`; both feed the mapping) |
| Provisioning | `AutoRegister` (new customers allowed, default true), `DefaultRoleIds` (`List<int>`, stored as a comma list; the Configure picker leaves out privileged roles and the save drops any that are posted anyway), `AllowedEmailDomains` (CSV, `*.example.com` wildcards), `RefreshRolesOnLogin` (true) |
| Redirects | `PostLoginRedirectUrl` (used when the login has no `returnUrl`), `PostLogoutRedirectUrl` (landing page after OIDC and SAML sign-out). Local URLs only; anything else is ignored |

\* encrypted at rest (§7.1).

**Deleting a store's SSO account** (SSO Accounts grid → Delete) removes the store's rows **and writes the
defaults back** for that store. Without the write-back, nop's `loadSharedValueIfNotFound` would fall back to the
store-0 values and the "deleted" store would silently inherit them.

### 4.2 `SsoSystemSettings` — store 0, system-wide

| Property | Default | Purpose |
|---|---|---|
| `SsoBypassToken`* | empty (bypass off) | value for `?ssobypass=` (§6.1) |
| `LocalLoginAllowedRoles` | `Administrators` | role system names allowed to finish a bypass login |
| `LoginAttemptCapPerHour` | 60 | per-IP cap per hour, counted separately for the login entry point and `/saml/acs`; 0 = off |
| `EnableAuditLogging` | true | off = no audit rows, except `HandshakeRun` (forced) and `LogCleared` (written directly) |
| `AuditRetentionDays` | 365 | rows older than this are deleted by the daily scheduled task **SSO audit log retention** (`SsoAuditRetentionTask`, registered on install and update); 0 = keep forever |

These cards render on the Configure page only at scope "All stores"; the per-store cards render only when a
store is selected. A save at a store scope writes only the per-store settings — the system-wide ones are saved only
at "All stores" (before 2026-10-19 a store-scope save blanked them).

---

## 5. Sign-in flows

### 5.1 Entry point

The public button (`SSOAuthenticationViewComponent` → `PublicInfo.cshtml`) renders only when the store has
`IsEnabled` and `Protocol != Off`. It links to `ExternalAuthentication/Login`, which:

1. Applies the per-IP rate limit (`SsoRateLimiter`, `IStaticCacheManager`, 60-minute window). Over the cap →
   `Failed` / `login_retry_cap_hit`, warning, back to `/login`.
2. Refuses if SSO is off for the store, or the plugin is not active for the store in nop's external-auth list.
3. DEBUG + `EnableMockMode` → redirects to the mock IdP (§9).
4. **SAML** → `SamlChallengeBuilder` (§5.3). **OIDC** → a `Login` / `Oidc` audit row ("challenge sent"), then
   `Challenge(...)` on scheme `ExternalAuthSSO-Oidc`, with `LoginCallback` as the redirect and `/login` as the error
   callback. `LoginCallback` passes the `id_token` to provisioning.

### 5.2 OpenID Connect

- `OidcAuthenticationRegistrar` (created by nop with `Activator.CreateInstance`, not DI) registers the handler with
  the parts that never vary: authorization code flow, `SaveTokens`, userinfo claims, `OnRemoteFailure` → redirect
  to `/login` instead of a 500.
- **Options are per store.** `Infrastructure/Oidc/SsoOidcOptionsConfigurer` fills authority, client ID and secret,
  callback path, `SignedOutCallbackPath = /signout-callback-microsoft`, the post-logout page and scopes from the
  **current store's** settings. `Infrastructure/Oidc/StoreAwareOidcOptionsCache` (the
  `IOptionsMonitorCache<OpenIdConnectOptions>`) keeps one options object per store, each with its own
  discovery-document manager. So each store can use its own IdP app registration.
- **Changes apply without a restart.** Saving, deleting or seeding a store's settings calls
  `ISsoOidcOptionsCache.InvalidateStore`. Entries also expire after 15 minutes, which covers web farms, where a
  save on one server does not reach the others.
- When `Authority` / `ClientId` are empty the configurer uses placeholders so `OpenIdConnectOptions.Validate()`
  does not throw — the OIDC handler is initialised on every request, and an unconfigured store would otherwise
  fail on every page.
- `LoginCallback` authenticates against the OIDC scheme, reads the access token, and calls the provisioning
  service (§5.4).

### 5.3 SAML 2.0 (ITfoxtec)

- `Saml2ConfigurationFactory` builds a fresh `Saml2Configuration` **per request** from the current store's settings,
  for a purpose (`SamlConfigPurpose`): **Login** carries the SP signing certificate when `SignedRequest` is on,
  **Logout** when `SignLogoutRequest` is on, **Metadata** when either is. ITfoxtec signs whatever it binds when a
  certificate is present, so this is what keeps the two kinds of signing independent. A PFX is required when the
  purpose signs or `EncryptedAssertion` is on.
- **Challenge** (`SamlChallengeBuilder`): builds the `AuthnRequest`, binds with the configured binding, writes the
  `Login` audit row, and stores the request ID and return URL in the correlation cookie
  `.AspNetCore.ExternalAuthSSO.SamlCorrelation` (10 minutes). No session state, so several tabs can sign in at once.
- **Correlation cookies** (`SamlCookieOptions`, login and logout): `SameSite=None; Secure` over HTTPS, because the
  IdP comes back with a cross-site POST and browsers drop `Lax` cookies on those. Over plain HTTP they fall back to
  `Lax` and correlation cannot work — **SAML needs HTTPS end to end.**
- **ACS** (`POST /saml/acs`, `SamlResponseProcessor`): no correlation cookie → refused (`acs_pending_cookie_invalid`)
  unless the store has `AllowIdpInitiatedLogin` on. Then ITfoxtec reads and validates the response — signature,
  digest allowlist, audience, NotBefore/NotOnOrAfter with skew, assertion decryption, XXE-safe parsing — in every
  build. The plugin checks `InResponseTo` against the cookie, calls provisioning (§5.4), and saves NameID and
  SessionIndex on the signed-in customer for SLO.
- **SP metadata** (`GET /saml/metadata`, `SamlMetadataBuilder`): per-store `EntityDescriptor`, anonymous, no
  secrets.

### 5.4 Provisioning — one path for both protocols

`SsoUserProvisioningService.HandleAsync(principal, protocol, accessToken, returnUrl)`:

0. No `returnUrl` → `PostLoginRedirectUrl`, when it is a local URL.
1. Reads email, first/last name, username and groups through the per-store claim types, falling back to the
   standard `ClaimTypes` URIs. The link key is `sub` / NameID, or the email when the token has neither.
2. No email → `Failed` / `acs_no_email_claim`.
3. Email domain not in `AllowedEmailDomains` → `Failed` / `acs_email_domain_blocked`. Empty list = any domain.
4. An existing customer with that email who is in **Administrators** → `Failed` / `acs_admin_account_blocked`.
   Admin accounts can never be signed in through SSO.
5. No customer with that email and `AutoRegister` off → `Failed` / `acs_auto_register_disabled`.
6. **Link by email.** An existing (non-admin) customer not yet linked to this IdP account is linked with
   `IExternalAuthenticationService.AssociateExternalAccountWithUserAsync`. Without this, nop refuses an unlinked
   existing email ("email already exists").
7. Calls nop's `IExternalAuthenticationService.AuthenticateAsync` (`ProviderSystemName = ExternalAuth.SSO`, all
   claims). Registration, sign-in, cart migration and events are nop's — and nop's
   `CustomerSettings.UserRegistrationType` still applies: with registration *Disabled*, new users are refused
   whatever `AutoRegister` says.
8. **Checks the sign-in happened**: the work context's customer must be a registered customer with that email.
   - Not signed in, but nop created the customer (e-mail validation / admin approval pending) → `AutoRegistered`
     with a note; nothing else.
   - Not signed in otherwise → `Failed` / `acs_customer_login_failed`; nop's error message is on the login page.
9. On success: saves `SignInProtocol` and the OIDC `id_token` (empty for SAML), runs role sync if `RefreshRolesOnLogin` (§5.5), and writes `AutoRegistered` (new
   customer), `Linked` (linked by this login) or `SignedIn` (returning customer), with groups and granted/revoked
   role IDs in `Notes`.

**Names.** nop's external-auth parameters have no name fields. When nop registers an SSO customer it publishes
`CustomerAutoRegisteredByExternalMethodEvent`; `Infrastructure/SsoCustomerRegisteredEventConsumer` copies first and
last name from the claims (store claim types, then the standard URIs) — the same pattern as nop's Facebook plugin.

### 5.5 Group → role mapping

- **Token values** = the values of the `GroupsClaimType` claim and of the `RolesClaimType` claim (app roles),
  case-insensitive. A mapping row can name either a group object ID or an app-role value.
- **Resolved set** = active mappings for the store whose `IdpGroupId` is in the token values, plus the default
  roles.
- **Owned set** = every role that appears in any mapping for the store, plus the default roles.
- On login: add `(resolved ∩ owned) − current`; remove `(owned − resolved) ∩ current`. Roles outside the owned set
  are never touched. If the owned set is empty nothing happens.
- **Group overage** (Entra, user in more groups than fit in the token): the token carries a marker (`_claim_names` / `hasgroups` in OIDC, `…/claims/groups.link` in SAML) instead of the list. Role sync is then **skipped** — nothing granted or revoked — and the success row notes `groups_overage`. Avoid it by having the IdP send only groups assigned to the application, or map app roles.
- **Administrators** is refused by the mapping form, filtered out of the resolved set whatever the mapping rows
  say (with a warning in nop's log naming the bad mapping), and never applied as a default role.
- Admin page: *Group → Role Mappings* — DataTables grid with store and active filters, create/edit/delete, intro
  banner explaining the rules.

---

## 6. Filters (global MVC, constructor-injected)

### 6.1 `LoginActionsFilter` — `GET Customer/Login`

Does nothing unless the store has SSO enabled with `RedirectLoginToSso` on. Then:

- bypass cookie already present → local form renders;
- `?ssobypass=<token>` matches the decrypted `SsoBypassToken` (constant-time compare) → issues
  `.AspNetCore.ExternalAuthSSO.BypassUsed` (HttpOnly, Lax, 5 minutes) and renders the local form;
- otherwise → redirect to `ExternalAuthentication/Login`. A wrong token is treated as no token, so the page does
  not reveal whether a bypass exists.

### 6.2 `LocalLoginEnforcementFilter` — `POST Customer/Login` with the bypass cookie

Lets nop's login run, then checks the signed-in customer against `LocalLoginAllowedRoles`. Allowed → cookie
cleared. Not allowed → signed out, `BypassDenied` audit row, "access denied" notification, redirect to `/login`
(cookie kept so they can retry).

### 6.3 `LogoutActionsFilter` — `Customer/Logout`

**nop's logout runs first**, and does everything it normally does: ends an impersonation, writes the
`PublicStore.Logout` activity, signs out locally and publishes `CustomerLoggedOutEvent`. Before it runs, the filter
captures the customer's `SignInProtocol`, NameID and SessionIndex. Afterwards it clears those three attributes and
replaces nop's redirect:

- `Oidc` → `SignOutResult` on the OIDC scheme: the IdP's `end_session_endpoint`, then back to the post-logout page.
- `Saml2` → `ISamlLogoutHandler.InitiateAsync` (§6.4).
- No protocol, an impersonation ending, or the DEBUG mock IdP → nop's result stays.

`id_token_hint`: the filter also captures the `OidcIdToken` attribute saved at sign-in and puts it in the sign-out
properties; `OnRedirectToIdentityProviderForSignOut` (in `OidcAuthenticationRegistrar`) sets it as `IdTokenHint`.
nop's external-auth cookie is gone by logout time, so the handler could not find it on its own. With the hint Entra
skips the account picker and honours `post_logout_redirect_uri`. Not yet run against an IdP.

### 6.4 SAML Single Log-Out (`/saml/logout`, `SamlLogoutHandler`)

- SP-initiated (from the filter, after nop's local sign-out): builds a `LogoutRequest` from the captured
  NameID/SessionIndex — signed when `SignLogoutRequest` is on — keeps its ID in
  `.AspNetCore.ExternalAuthSSO.SamlLogoutCorrelation` and redirects to `IdpSloUrl`. With no IdP SLO URL the customer
  lands on the post-logout page.
- IdP response (either binding): validates `InResponseTo` against that cookie, lands on the post-logout page.
- IdP-initiated (either binding): validates the request, clears the attributes, signs the customer out, answers
  with a `LogoutResponse`.
- `/saml/logout` with no SAML message → nop's logout (so the filter runs).
- Writes `Slo` (or `Failed` / `slo_*`).

---

## 7. Security

### 7.1 Secrets at rest

`SsoSecretsService.Protect` = ASP.NET Core Data Protection (purpose `Nop.Plugin.ExternalAuth.SSO.Settings.v1`),
stored as `dp1:` + ciphertext — one layer, keyed outside the database. `Unprotect` reads that, and also the legacy
two-layer form (nop's `IEncryptionService` around Data Protection, written before 2026-10-19), which the plugin update
to `…_DATA_0.5` rewrites; a value the server cannot decrypt is left as it is. `TryUnprotect` returns false instead of
throwing. Applies to `ClientSecret`, `IdpCertificateBase64`, `SpPfxBase64`, `SpPfxPassword`, `SsoBypassToken`. The
Configure page never sends secrets back to the browser (`Has…` flags tell the validator a value is already stored).

> **Data Protection keys must persist and be shared across servers.** nop keeps them in
> `~/App_Data/DataProtectionKeys` (or Redis/Azure when configured). If the key ring changes (new server, wiped
> folder), every stored secret becomes unreadable and must be re-entered.

### 7.2 Controls

| Control | Where |
|---|---|
| Assertion signature, digest allowlist, audience, lifetime, decryption, XXE | ITfoxtec (`SamlResponseProcessor`) |
| `InResponseTo` correlation (login and logout); unsolicited SAML responses refused unless `AllowIdpInitiatedLogin` | correlation cookies (`SameSite=None; Secure`), not session |
| OIDC state / nonce / PKCE / token signature | ASP.NET Core OIDC handler |
| Admin accounts cannot link through SSO | `SsoUserProvisioningService` |
| Administrators never granted by mapping or default role | `Constants/SsoRoles` (one list), used by the mapping service, form, validator, provisioning and the Configure save |
| Email-domain allowlist with wildcards | provisioning |
| Local-login bypass limited to listed roles | the two login filters |
| Per-IP rate limit on the login entry point and on `/saml/acs` (checked before any XML work), separate counters | `SsoRateLimiter` |
| Outbound fetches (metadata import, handshake, Test connection): HTTPS only, no loopback/private addresses | `Services/Http/SafeUrl` |
| Metadata XML read by ITfoxtec (DTD/XXE refused); metadata signatures are **not** verified — trust is the certificate thumbprint | `IdpMetadataImporter` |
| Audit row written after the side effect, failures never break login | `SsoEventService` |
| Admin pages | `[AuthorizeAdmin]` + `[CheckPermission(AccessSsoSettings)]` |

### 7.3 Permissions

One permission, `AccessSsoSettings`, declared in `ExternalAuthSSOPermissionConfigManager`, granted to
Administrators on install and checked on every admin action. The former `AccessSsoPlugin` was never checked; the
plugin update removes its record.

---

## 8. Admin UI

Menu: an *SSO* node inserted before nop's **Configuration** menu (`EventConsumer : BaseAdminMenuCreatedEventConsumer`,
shown only while the plugin is installed).

Four controllers on `SsoAdminControllerBase` (admin area, `[AuthorizeAdmin]`, antiforgery; `[CheckPermission
(AccessSsoSettings)]` on every action), URLs `/Admin/{controller}/{action}`:
`SsoConfiguration` (Configure, ConfigureStore, ChangeStoreScopeSSOConfiguration, ImportIdpMetadata, OnboardingReport),
`SsoAccounts` (SsoAccounts, SsoAccountsList, AddSsoAccount, TestConnection, DeleteSsoAccount),
`SsoGroupRoleMappings` (GroupRoleMappings, …List, …Create, …Edit, …Delete), `SsoAuditLog` (AuditLog, AuditLogList,
ClearAuditLog). Names are in `Constants/SsoAdminControllers`. Page behaviour is in `Content/Admin/sso-admin.js`
(`window.SsoAdmin`), fed by a hidden `data-*` config div on each page — no inline scripts on Configure or SSO
Accounts. After editing the script, empty `Presentation/Nop.Web/wwwroot/bundles` (WebOptimizer disk cache).

| Page / endpoint | Action(s) | Notes |
|---|---|---|
| SSO Accounts | `SsoAccounts`, `SsoAccountsList` (POST) | One row per store with SSO configured: protocol, enabled, setup date, number of users, last successful login, IdP certificate expiry (near-expiry / expired flags). Buttons: Test, Edit, Delete |
| Add SSO account | `AddSsoAccount` (GET/POST) | Store picker → Configure for that store |
| Test connection | `TestConnection(storeId)` (POST, JSON) | OIDC: fetches the discovery document. SAML: parses the IdP certificate (expiry included), HEADs `IdpSsoUrl`, checks metadata. Writes `HandshakeRun` |
| Delete SSO account | `DeleteSsoAccount(storeId)` (POST) | §4.1 |
| Configure | `Configure` (GET/POST), `ConfigureStore/{id}` | Per-store / system-wide cards; protocol-aware card visibility; Save top-right; "Send to IdP team" card with SP metadata URL (SAML) or redirect / post-logout URIs (OIDC). Writes `ConfigChanged` |
| Store scope | `ChangeStoreScopeSSOConfiguration` | Plugin's own store picker (`StoreScopeSSOViewComponent`, shown with 2+ stores); keeps the scope in a customer attribute (`SSOAuthenticationDefaults.StoreScopeAttribute`) so grid Edit buttons open the right store; 0 = "All stores" |
| Import IdP metadata | `ImportIdpMetadata` (POST: URL or XML file) | Fills entity ID, SSO/SLO URLs and signing certificate |
| Onboarding report | `OnboardingReport` | Downloads a text file of the SP values to send to the IdP team |
| Group → Role Mappings | `GroupRoleMappings`, `…List`, `…Create`, `…Edit`, `…Delete` | §5.5 |
| Audit log | `AuditLog`, `AuditLogList`, `ClearAuditLog` | Filters: store, event type, protocol, email, date range. Clear writes a forced `LogCleared` row |

Public routes: `ExternalAuthentication/Login`, `ExternalAuthentication/LoginCallback`, `/signin-microsoft`,
`/signout-callback-microsoft`, `POST /saml/acs`, `GET /saml/metadata`, `GET|POST /saml/logout`.

---

## 9. Mock IdP (DEBUG builds only)

For local development without an IdP. Two gates: compiled only under `#if DEBUG`, and the per-store
`EnableMockMode` flag.

- `/mocksso/login` shows a form with four scenarios: `success-existing`, `success-new`, `success-with-groups`,
  `failure`.
- Both protocols: the mock builds a `ClaimsPrincipal` from the form and calls the provisioning service directly.
  `/saml/acs` and the OIDC handler are not involved and are never relaxed.
- The mock page itself lets anyone sign in as anyone, so never deploy a DEBUG build and never leave the flag on.
- Logout of a mock sign-in stays local (there is no IdP session to end).

---

## 10. Build, packages and test data

### 10.1 Packages — must match the host

A plugin compiled against a newer `Microsoft.IdentityModel.*` than Nop.Web has loaded fails at runtime
(`ReflectionTypeLoadException` / `FileLoadException`), even though it compiles. The pins follow
`Presentation/Nop.Web/bin/<config>/net10.0/Microsoft.IdentityModel.Tokens.dll`:

| Package | Version | Why this one |
|---|---|---|
| `Microsoft.IdentityModel.Tokens`, `.Protocols.OpenIdConnect` | **8.16.0** | what Nop.Web 5.00 ships |
| `Microsoft.IdentityModel.Tokens.Saml`, `.Xml` | **8.16.0** | not in Nop.Web; pinned so the family is one version |
| `Microsoft.AspNetCore.Authentication.OpenIdConnect` | **10.0.9** | last 10.0.x needing IdentityModel ≤ 8.16 (10.0.10+ need 8.19.2) |
| `ITfoxtec.Identity.Saml2.MvcCore` | **4.17.0** | last release whose net10.0 target accepts 8.16 (4.18+ need 8.18–8.22) |

On every nopCommerce upgrade: read the host's `Microsoft.IdentityModel.Tokens.dll` version, re-pin all four rows,
and confirm `dotnet build` raises no NU1605 (package downgrade).

**Runtime patching.** ITfoxtec references `System.Security.Cryptography.Xml` / `.Pkcs` 10.0.0, which have known
vulnerabilities; the warnings are hidden because nop's `Directory.Build.props` suppresses NU1901–NU1904. At runtime
both come from the ASP.NET Core shared framework (10.0.12 locally, patched), so **every server must keep its
ASP.NET Core 10 runtime up to date** — that, not the plugin, is what keeps SAML signature checking patched.

Project shape: `ProjectReference` to `Nop.Web.Framework` only, `CopyLocalLockFileAssemblies=true`,
`Build/ClearPluginAssemblies.proj` after build, `Nullable` enabled.

### 10.2 Test-store seed

**DEBUG builds only.** `AddSsoAccount` (POST) calls `SeedTestStoreAsync`, compiled under `#if DEBUG`, which pre-fills
a SAML configuration when the chosen store is named **"Goodtest"** (case-insensitive). Release builds contain neither
the call nor the values. The values are hard-coded in the controller (`SAMLSSOConfig_StoreID_110.json` is not read);
the certificate and PFX are placeholders that only satisfy validation, and no default role is set. Delete the
method when that test tenant goes.

### 10.3 Settings retyped on 2026-10-19 (plugin version `…_DATA_0.4`)

`LoginBinding` / `LogoutBinding` became the `SamlBinding` enum and `DefaultRoleSystemName` became `DefaultRoleIds`.
`UpdateAsync` → `ConvertRetypedSettingsAsync` rewrites existing rows for every store (`HTTP-Redirect` →
`HttpRedirect`; a role-id CSV or a role system name → ids) and deletes the old role rows. Until it runs, nop skips
values it cannot convert and uses the defaults.

> The seed block uses a placeholder tenant ID (`00000000-…`), because the repository is public; put the real one in
> locally and never commit it. `SAMLSSOConfig_StoreID_110.json` holds the real tenant ID and is excluded by
> `.gitignore`.

---

## 11. Known gaps and differences from the original plan

**Gaps G1–G14 were fixed on 2026-10-19** — design, decisions and test plan in `SSO_Gaps_Fix_Spec.md` (this folder).
In short: OIDC sign-out at the IdP and nop's own logout both run (G1, G11); OIDC options are per store and apply
without a restart (G2); `AutoRegister`, the two redirect URLs and `SignLogoutRequest` are honoured and
`CustomAttributesJson` is gone (G3); `AccessSsoPlugin` removed (G4); the SAML correlation cookie now reaches the ACS
and unsolicited responses are refused unless allowed (G5, G12); the mock never touches `/saml/acs` (G6); Test
connection has the URL guard (G7); the plugin's active flag is honoured (G8); bad privileged mappings are logged
(G9); `BypassDenied` has no protocol (G10); existing customers are linked by email and only real sign-ins are
audited as success (G13); default roles are applied (G14). **None of it has run against a real IdP yet** (§12).

**Still open:**

| Item | Effect |
|---|---|
| Web farm OIDC changes | A settings save reaches other servers only when their cached options expire (15 minutes) |
| nop registration type | nop's `UserRegistrationType` = *Disabled* refuses new SSO users even with `AutoRegister` on |

**Planned in `SSOPlan.v2.md` but not built:** IdP-metadata cache and auto-refresh from `IdpMetadataUrl`; a
a scheduled certificate-expiry warning (expiry is only shown
on the SSO Accounts grid and by Test connection); health checks; OpenTelemetry; Azure Key Vault for the SP PFX; automated tests.

**Built but not in the plan:** SSO Accounts page, Add SSO account, Test connection with audit, onboarding report,
per-IP rate limiting, `RedirectLoginToSso` opt-out, `IdpDisplayName`, `ConfigChanged` / `LogCleared` /
`HandshakeRun` events, machine-readable failure reasons, the plugin's own store-scope picker. Added 2026-10-19
(plugin version `…_DATA_0.6`, `SSO_Code_Review_2026-10.md` §5.3): `id_token_hint` at OIDC logout, app roles as a
mapping source, audit retention with a daily task, a rate cap on `/saml/acs`, an OIDC "Login started" row.

**Changed from the plan:** AutoMapper is not used (no mapper profile; models are mapped by hand); route names are
nop's default `ExternalAuthentication/*` rather than `/external-auth/microsoft/challenge`.

---

## 12. Testing

Not yet done on 5.00: real OIDC and SAML sign-in. A local Keycloak (Docker) covers both protocols; Auth0 / Okta
developer tenants also work. Run over **HTTPS** (SAML correlation needs it), on **two stores with different IdP
settings**. The full case list is `SSO_Gaps_Fix_Spec.md` §4; the minimum per protocol:

1. New user → `AutoRegistered`; existing unlinked user → `Linked`; returning user → `Login`; admin email → blocked.
2. Disallowed email domain → blocked; new user with `AutoRegister` off → blocked.
3. Group mapping grants and revokes roles; default roles applied; Administrators never granted.
4. `/login` redirects to the IdP; `?ssobypass=` lets an admin in and turns a non-admin away (`BypassDenied`).
5. Logout: nop's `PublicStore.Logout` activity, then the IdP sign-out, then the post-logout page. SAML writes `Slo`.
6. Admin impersonating an SSO customer clicks Logout → back on the admin customer page, still signed in.
7. Change store A's OIDC client ID, save, log in again → new client ID, no restart.
8. Store with SSO off: normal login, no button, logout untouched.
9. Release build: `/mocksso/login` returns 404.

---

## 13. Upgrade notes — 4.90 → 5.00 (2026-10-19)

What the port changed, for reuse on other plugins:

- `net9.0` → `net10.0`; `plugin.json` `SupportedVersions` → `"5.00"`.
- `IWebHelper` moved from `Nop.Core` to `Nop.Services.Helpers` — add `using Nop.Services.Helpers;`.
- AutoMapper replaced by Mapster: `AutoMapperConfiguration` → `MapperConfiguration`. (This plugin's unused
  `MappingExtensions.cs` was deleted.)
- `Create.TableFor<T>()` removed → `this.CreateTableIfNotExists<T>()` (also checks existence).
- Package pins re-derived from the host (§10.1).

## 14. Legacy clean-up (environments that had the pre-rename plugin)

The plugin was renamed `ExternalAuth.Microsoft` → `ExternalAuth.SSO`; nop treats it as a new plugin. On such an
environment: uninstall the old one, delete orphaned `Plugins.ExternalAuth.Microsoft.*` locale rows, and remove old
store-0 `SsoConfigurationSettings` rows before configuring stores.
