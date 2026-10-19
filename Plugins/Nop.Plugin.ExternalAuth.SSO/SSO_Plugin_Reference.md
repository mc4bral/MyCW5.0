# SSO Authentication plugin — Reference

| | |
|---|---|
| **Project** | `Nop.Plugin.ExternalAuth.SSO` |
| **SystemName** | `ExternalAuth.SSO` · Group `ExternalAuth methods` · FriendlyName *SSO Authentication* |
| **Host** | nopCommerce **5.00**, .NET **10** (`C:\Work\CW_NOP_5`). Ported from the 4.90 / .NET 9 build on 2026-10-19 |
| **Protocols** | OpenID Connect (primary) and SAML 2.0 (secondary), one per store |
| **Status** | Builds and installs on 5.00; admin pages work. **OIDC and SAML sign-in have not been tested against a real IdP on 5.00** — see §12 |
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
├── Infrastructure/                   NopStartup, OidcAuthenticationRegistrar, EventConsumer (admin menu),
│                                     ViewLocationExpander
├── Models/                           admin view models + result records (TestConnectionResult, HandshakeResult, ...)
├── Security/                         ExternalAuthSSOPermissions, ExternalAuthSSOPermissionConfigManager
├── Services/                         provisioning, events, group→role, secrets, rate limiter, store summary,
│   └── Saml/                         test connection, onboarding report; SAML config, challenge, ACS,
│                                     metadata, logout, IdP-metadata import
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
| `EventType` | `Login`, `AutoRegistered`, `Linked`, `Slo`, `Failed`, `BypassDenied`, `ConfigChanged`, `LogCleared`, `HandshakeRun` |
| `Protocol` | `Oidc`, `Saml2`, or empty |
| `OccurredOnUtc`, `IpAddress`, `Notes` | `Notes` carries a machine-readable reason (`SsoFailureReasons`, e.g. `acs_email_domain_blocked`) plus detail |

Indexes: `IX_SSO_AuthEvent_OccurredOnUtc`, `IX_SSO_AuthEvent_StoreId_OccurredOnUtc`.

**`SSO_GroupRoleMapping`** (`SsoGroupRoleMapping`) — `StoreId`, `IdpGroupId` (Entra object-ID GUID, validated by
regex), `IdpGroupDisplayName` (display only), `CustomerRoleId`, `DisplayOrder`, `IsActive`.
Index: `IX_SSO_GroupRoleMapping_StoreId_IdpGroupId`.

**Customer generic attributes** (`SsoCustomerAttributes`): `ExternalAuth.SSO.SamlNameId`,
`ExternalAuth.SSO.SamlSessionIndex`, `ExternalAuth.SSO.SignInProtocol` — written on a SAML sign-in, cleared on
SAML sign-out, read by the logout filter.

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
| SAML SP | `SpEntityId`, `AcsUrl`, `SpLogoutUrl`, `SpPfxBase64`*, `SpPfxPassword`*, `SignedRequest`, `EncryptedAssertion`, `LoginBinding`, `LogoutBinding` (`HTTP-Redirect` default) |
| Claims | `EmailClaimType`, `UsernameClaimType`, `FirstNameClaimType`, `LastNameClaimType`, `GroupsClaimType` |
| Provisioning | `DefaultRoleSystemName` (`Registered`), `AllowedEmailDomains` (CSV, `*.example.com` wildcards), `RefreshRolesOnLogin` (true) |
| Stored but not used at runtime | `AutoRegister`, `PostLoginRedirectUrl`, `PostLogoutRedirectUrl`, `SignLogoutRequest`, `CustomAttributesJson` — see §11, gap G3 |

\* encrypted at rest (§7.1).

**Deleting a store's SSO account** (SSO Accounts grid → Delete) removes the store's rows **and writes the
defaults back** for that store. Without the write-back, nop's `loadSharedValueIfNotFound` would fall back to the
store-0 values and the "deleted" store would silently inherit them.

### 4.2 `SsoSystemSettings` — store 0, system-wide

| Property | Default | Purpose |
|---|---|---|
| `SsoBypassToken`* | empty (bypass off) | value for `?ssobypass=` (§6.1) |
| `LocalLoginAllowedRoles` | `Administrators` | role system names allowed to finish a bypass login |
| `LoginAttemptCapPerHour` | 60 | per-IP cap on the SSO login entry point; 0 = off |
| `EnableAuditLogging` | true | off = no audit rows, except `HandshakeRun` (forced) and `LogCleared` (written directly) |

These cards render on the Configure page only at scope "All stores"; the per-store cards render only when a
store is selected.

---

## 5. Sign-in flows

### 5.1 Entry point

The public button (`SSOAuthenticationViewComponent` → `PublicInfo.cshtml`) renders only when the store has
`IsEnabled` and `Protocol != Off`. It links to `ExternalAuthentication/Login`, which:

1. Applies the per-IP rate limit (`SsoRateLimiter`, `IStaticCacheManager`, 60-minute window). Over the cap →
   `Failed` / `login_retry_cap_hit`, warning, back to `/login`.
2. Refuses if SSO is off for the store.
3. DEBUG + `EnableMockMode` → redirects to the mock IdP (§9).
4. **SAML** → `SamlChallengeBuilder` (§5.3). **OIDC** → `Challenge(...)` on scheme `ExternalAuthSSO-Oidc`, with
   `LoginCallback` as the redirect and `/login` as the error callback.

### 5.2 OpenID Connect

- `OidcAuthenticationRegistrar` registers the handler: authorization code flow, `SaveTokens`, userinfo claims,
  `SignedOutCallbackPath = /signout-callback-microsoft`, scopes from settings, `OnRemoteFailure` → redirect to
  `/login` instead of a 500.
- **The options are built once, at application start, from the store-0 settings** (`EngineContext` resolve inside
  the registrar). Changing OIDC settings needs an app restart, and every store shares one OIDC client. See gap G2.
- When `Authority` / `ClientId` are empty the registrar uses placeholders so `OpenIdConnectOptions.Validate()`
  does not throw — the OIDC handler is initialised on every request, and an unconfigured plugin would otherwise
  take the whole site down.
- `LoginCallback` authenticates against the OIDC scheme, reads the access token, and calls the provisioning
  service (§5.4).

### 5.3 SAML 2.0 (ITfoxtec)

- `Saml2ConfigurationFactory` builds a fresh `Saml2Configuration` **per request** from the current store's settings
  (this is what makes SAML genuinely multi-store, unlike OIDC). A signing PFX is required when `SignedRequest` or
  `EncryptedAssertion` is on.
- **Challenge** (`SamlChallengeBuilder`): builds the `AuthnRequest`, binds with the configured binding, writes the
  `Login` audit row, and stores the request ID and return URL in the correlation cookie
  `.AspNetCore.ExternalAuthSSO.SamlCorrelation` (HttpOnly, SameSite=Lax, 10 minutes). No session state, so
  several tabs can sign in at once.
- **ACS** (`POST /saml/acs`, `SamlResponseProcessor`): ITfoxtec reads and validates the response — signature,
  digest allowlist, audience, NotBefore/NotOnOrAfter with skew, assertion decryption, XXE-safe parsing. Then the
  plugin checks `InResponseTo` against the cookie, clears the cookie, calls provisioning (§5.4), and saves NameID,
  SessionIndex and `SignInProtocol = Saml2` on the customer for SLO.
  When the cookie is absent the `InResponseTo` check is skipped, so IdP-initiated responses are accepted
  (gap G5).
- **SP metadata** (`GET /saml/metadata`, `SamlMetadataBuilder`): per-store `EntityDescriptor`, anonymous, no
  secrets.

### 5.4 Provisioning — one path for both protocols

`SsoUserProvisioningService.HandleAsync(principal, protocol, accessToken, returnUrl)`:

1. Reads email, first/last name, username and groups through the per-store claim types, falling back to the
   standard `ClaimTypes` URIs.
2. No email → `Failed` / `acs_no_email_claim`.
3. Email domain not in `AllowedEmailDomains` → `Failed` / `acs_email_domain_blocked`. Empty list = any domain.
4. An existing customer with that email who is in **Administrators** → `Failed` / `acs_admin_account_blocked`.
   Admin accounts can never be signed in through SSO.
5. Builds `ExternalAuthenticationParameters` (`ProviderSystemName = ExternalAuth.SSO`, `ExternalIdentifier = sub`
   / NameID, all claims) and calls nop's `IExternalAuthenticationService.AuthenticateAsync`. Linking, registration
   rules (nop's `ExternalAuthenticationSettings` / customer settings), sign-in and events are nop's.
6. If `RefreshRolesOnLogin`: role sync (§5.5).
7. Writes `AutoRegistered` (no customer before) or `Linked` (customer existed), with groups and granted/revoked
   role IDs in `Notes`.

### 5.5 Group → role mapping

- **Resolved set** = active mappings for the store whose `IdpGroupId` is in the token's groups, plus
  `DefaultRoleSystemName`.
- **Owned set** = every role that appears in any mapping for the store, plus `DefaultRoleSystemName`.
- On login: add `(resolved ∩ owned) − current`; remove `(owned − resolved) ∩ current`. Roles outside the owned set
  are never touched. If the owned set is empty nothing happens.
- **Administrators** is filtered out of the resolved set in the service, whatever the mapping rows say, and is
  never added as the default role.
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

Reads the customer's `SignInProtocol`. `Saml2` → redirect to `/saml/logout`. `Oidc` → `SignOutResult` on nop's
cookie scheme and the OIDC scheme (end-session at the IdP). Anything else → nop's normal logout.

### 6.4 SAML Single Log-Out (`/saml/logout`, `SamlLogoutHandler`)

- SP-initiated: builds a `LogoutRequest` from the stored NameID/SessionIndex, keeps its ID in
  `.AspNetCore.ExternalAuthSSO.SamlLogoutCorrelation`, signs the customer out locally and redirects to `IdpSloUrl`.
- IdP response: validates `InResponseTo` against that cookie.
- IdP-initiated: unbinds the request, signs the customer out, answers with a `LogoutResponse`.
- Clears the three customer attributes; writes `Slo` (or `Failed` / `slo_*`).

---

## 7. Security

### 7.1 Secrets at rest

`SsoSecretsService.Protect` = ASP.NET Core Data Protection (purpose
`Nop.Plugin.ExternalAuth.SSO.Settings.v1`) **then** nop's `IEncryptionService`. `Unprotect` reverses both;
`TryUnprotect` returns false instead of throwing. Applies to `ClientSecret`, `IdpCertificateBase64`, `SpPfxBase64`,
`SpPfxPassword`, `SsoBypassToken`. The Configure page never sends secrets back to the browser
(`Has…` flags tell the validator a value is already stored).

> **Data Protection keys must persist and be shared across servers.** nop keeps them in
> `~/App_Data/DataProtectionKeys` (or Redis/Azure when configured). If the key ring changes (new server, wiped
> folder), every stored secret becomes unreadable and must be re-entered.

### 7.2 Controls

| Control | Where |
|---|---|
| Assertion signature, digest allowlist, audience, lifetime, decryption, XXE | ITfoxtec (`SamlResponseProcessor`) |
| `InResponseTo` correlation (login and logout) | correlation cookies, not session |
| OIDC state / nonce / PKCE / token signature | ASP.NET Core OIDC handler |
| Admin accounts cannot link through SSO | `SsoUserProvisioningService` |
| Administrators never granted by mapping or default role | `SsoGroupRoleMappingService`, provisioning |
| Email-domain allowlist with wildcards | provisioning |
| Local-login bypass limited to listed roles | the two login filters |
| Per-IP rate limit on the login entry point | `SsoRateLimiter` |
| IdP-metadata URL fetch: HTTPS only, no loopback/private addresses, DTD prohibited | `IdpMetadataImporter` |
| Audit row written after the side effect, failures never break login | `SsoEventService` |
| Admin pages | `[AuthorizeAdmin]` + `[CheckPermission(AccessSsoSettings)]` |

### 7.3 Permissions

Declared in `ExternalAuthSSOPermissionConfigManager`, granted to Administrators on install:
`AccessSsoSettings` (checked on every admin action) and `AccessSsoPlugin` (declared, **not checked anywhere**).

---

## 8. Admin UI

Menu: an *SSO* node inserted before nop's **Configuration** menu (`EventConsumer : BaseAdminMenuCreatedEventConsumer`,
shown only while the plugin is installed).

| Page / endpoint | Action(s) | Notes |
|---|---|---|
| SSO Accounts | `SsoAccounts`, `SsoAccountsList` (POST) | One row per store with SSO configured: protocol, enabled, setup date, number of users, last successful login, IdP certificate expiry (near-expiry / expired flags). Buttons: Test, Edit, Delete |
| Add SSO account | `AddSsoAccount` (GET/POST) | Store picker → Configure for that store |
| Test connection | `TestConnection(storeId)` (POST, JSON) | OIDC: fetches the discovery document. SAML: parses the IdP certificate (expiry included), HEADs `IdpSsoUrl`, checks metadata. Writes `HandshakeRun` |
| Delete SSO account | `DeleteSsoAccount(storeId)` (POST) | §4.1 |
| Configure | `Configure` (GET/POST), `ConfigureStore/{id}` | Per-store / system-wide cards; protocol-aware card visibility; Save top-right; "Send to IdP team" card with SP metadata URL (SAML) or redirect / post-logout URIs (OIDC). Writes `ConfigChanged` |
| Store scope | `ChangeStoreScopeSSOConfiguration` | Plugin's own store picker (`StoreScopeSSOViewComponent`); keeps the scope in a customer attribute so grid Edit buttons open the right store |
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
- OIDC mock: builds a `ClaimsPrincipal` and calls the provisioning service directly.
- SAML mock: posts a forged response to `/saml/acs`. **With mock mode on, `/saml/acs` skips ITfoxtec validation
  and parses the response itself.** In a DEBUG build with the flag on, anyone can sign in as anyone through
  `/saml/acs`. Never deploy a DEBUG build, and never leave the flag on.

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

Project shape: `ProjectReference` to `Nop.Web.Framework` only, `CopyLocalLockFileAssemblies=true`,
`Build/ClearPluginAssemblies.proj` after build, `Nullable` enabled.

### 10.2 Test-store seed

`AddSsoAccount` (POST) pre-fills a SAML configuration when the chosen store is named **"Goodtest"**
(case-insensitive). The values are **hard-coded in the controller**; `SAMLSSOConfig_StoreID_110.json` is not read.
The certificate and PFX are placeholders that only satisfy validation. Delete the block when that test tenant goes.

> The seed block uses a placeholder tenant ID (`00000000-…`), because the repository is public; put the real one in
> locally and never commit it. `SAMLSSOConfig_StoreID_110.json` holds the real tenant ID and is excluded by
> `.gitignore`.

---

## 11. Known gaps and differences from the original plan

**Gaps — behaviour that is missing or wrong:**

| # | Gap | Effect |
|---|---|---|
| G1 | Nothing saves `SignInProtocol = Oidc` after an OIDC sign-in | The logout filter's OIDC branch never runs; OIDC users are signed out of nop only, not at the IdP |
| G2 | OIDC options are built once at startup from store 0 | All stores share one OIDC client; settings changes need a restart. Fix: `IPostConfigureOptions<OpenIdConnectOptions>` / per-store options |
| G3 | `AutoRegister`, `PostLoginRedirectUrl`, `PostLogoutRedirectUrl`, `SignLogoutRequest`, `CustomAttributesJson` are saved but never read | Registration follows nop's settings; redirects follow `returnUrl` / `/`; logout requests are never signed |
| G4 | `AccessSsoPlugin` permission is declared but never checked | Harmless; remove it or use it |
| G5 | `InResponseTo` is checked only when the correlation cookie exists | IdP-initiated SAML responses are accepted. Decide whether that is wanted |
| G6 | SAML mock bypass lives inside the real `/saml/acs` | §9 |
| G7 | Test connection (OIDC discovery, SAML HEAD) has no SSRF guard, unlike metadata import | Admin-only, low risk |
| G8 | `ExternalAuthenticationController.Login` computes `methodIsAvailable` and ignores it | The plugin's active/inactive state in nop's external-auth list does not stop the flow |
| G9 | Blocked privileged-role mappings are filtered silently, not audit-logged | Plan §3.8 said they would be logged |
| G10 | `BypassDenied` rows always record protocol `Oidc` | Cosmetic |

**Planned in `SSOPlan.v2.md` but not built:** IdP-metadata cache and auto-refresh from `IdpMetadataUrl`; a
scheduled audit-log purge (only *Clear all* exists); a scheduled certificate-expiry warning (expiry is only shown
on the SSO Accounts grid and by Test connection); health checks; OpenTelemetry; Azure Key Vault for the SP PFX; automated tests.

**Built but not in the plan:** SSO Accounts page, Add SSO account, Test connection with audit, onboarding report,
per-IP rate limiting, `RedirectLoginToSso` opt-out, `IdpDisplayName`, `ConfigChanged` / `LogCleared` /
`HandshakeRun` events, machine-readable failure reasons, the plugin's own store-scope picker.

**Changed from the plan:** AutoMapper is not used (no mapper profile; models are mapped by hand); route names are
nop's default `ExternalAuthentication/*` rather than `/external-auth/microsoft/challenge`.

---

## 12. Testing

Not yet done on 5.00: real OIDC and SAML sign-in. A local Keycloak (Docker) covers both protocols; Auth0 / Okta
developer tenants also work. Minimum run per protocol:

1. New user → `AutoRegistered`; existing user → `Linked`; admin email → blocked.
2. Disallowed email domain → blocked.
3. Group mapping grants and revokes roles; Administrators never granted.
4. `/login` redirects to the IdP; `?ssobypass=` lets an admin in and turns a non-admin away (`BypassDenied`).
5. Logout: SAML SLO round trip writes `Slo`. OIDC: confirm G1.
6. Store with SSO off: normal login, no button.
7. Change an OIDC setting and confirm whether a restart is needed (G2).
8. Release build: `/mocksso/login` returns 404.

Run each against two stores with different IdPs to cover the multi-store behaviour.

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
