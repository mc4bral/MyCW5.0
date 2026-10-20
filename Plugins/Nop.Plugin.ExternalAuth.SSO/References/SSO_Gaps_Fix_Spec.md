# SSO plugin — gap fixes (spec)

| | |
|---|---|
| **Scope** | `Nop.Plugin.ExternalAuth.SSO` on nopCommerce 5.00. Fixes the gaps in `SSO_Plugin_Reference.md` §11 plus four found while specifying and building them (G11–G14) |
| **Tier** | 2 — several files, one design change (per-store OIDC options) |
| **Status** | Approved 2026-10-19 — Q1 reject + per-store `AllowIdpInitiatedLogin`; Q2 delete `CustomAttributesJson`; Q3 link by email. **Implemented 2026-10-19; builds; not yet run against an IdP** (§4) |
| **Nop core** | Untouched |

When this is verified, fold what survives into `SSO_Plugin_Reference.md` (already updated for the new behaviour)
and move this file to `References/Deprecated/`.

### Implementation notes — where the build differs from the text below

- **Correlation cookies (G5/G12).** *Both* SAML correlation cookies, login and logout, use one rule
  (`Services/Saml/SamlCookieOptions`): `SameSite=None; Secure` over HTTPS, `Lax` over plain HTTP. The logout
  response can also arrive by POST, which Lax does not survive. The logout handler now unbinds either binding.
- **G14 — default roles never applied (found while building).** The Configure page saves the default-role
  multi-select as a CSV of role **ids** in `DefaultRoleSystemName`, but provisioning looked it up as one role
  **system name**, so no default role was ever granted. Provisioning now reads both forms (ids from the page,
  a system name from older installs and the test seed); privileged roles are skipped.
- **Names.** nop's external-auth parameters have no name fields, so a new SSO customer had no first or last name.
  Provisioning copies the IdP's names onto a customer created by that login, where empty.
- **Empty subject.** When the token has no `sub` / NameID, the email is used as the link key, so subject-less
  logins never share one link.
- **Pending registration.** If nop holds a new customer for e-mail validation or admin approval, the audit row is
  `AutoRegistered` with a note, and no roles or sign-in protocol are written.
- **`/saml/logout` with no SAML message** now redirects to nop's logout, which runs the filter.
- **Plugin update.** `plugin.json` → `2026_10_19_DATA_0.2`. `UpdateAsync` refreshes the locale strings and removes
  the `CustomAttributesJson` strings and setting rows and the `AccessSsoPlugin` permission record.
- **`NopStartup`** no longer registers `OidcAuthenticationRegistrar` in DI; nop never resolved it from there.

---

## 1. What is wrong, and the fix

### G1 + G11 — logout (OIDC never federated; nop's logout skipped)

**Now.** `LogoutActionsFilter` reads `SignInProtocol` from the customer. Only SAML ever writes it, so the OIDC branch
never runs. For a SAML customer the filter sets the result **before** nop's `CustomerController.Logout` runs, so nop's
logout never happens. That means no `PublicStore.Logout` activity row, no `CustomerLoggedOutEvent`, and no
impersonation handling: an admin impersonating an SSO customer who clicks Logout is fully signed out and sent to the
customer's IdP, instead of being returned to the admin customer page.

**Fix.**
- `SsoUserProvisioningService` writes `SignInProtocol` (`Oidc` / `Saml2`) on the customer who is actually signed in
  after `AuthenticateAsync`, for both protocols. `SamlResponseProcessor` keeps writing only NameID / SessionIndex.
- `LogoutActionsFilter` is rebuilt as "run nop first, then federate":
  1. Before `next()`: capture the current customer, its `SignInProtocol`, NameID and SessionIndex, and whether
     `IWorkContext.OriginalCustomerIfImpersonated` is set.
  2. `await next()` — nop logs out normally (activity log, event, impersonation).
  3. After: if it was impersonation, or the protocol is empty, leave nop's result. Otherwise replace the result:
     - **OIDC** → `SignOutResult` on the OIDC scheme only (nop's cookie is already gone), `RedirectUri` = post-logout
       URL (G3).
     - **SAML** → `ISamlLogoutHandler.InitiateAsync(nameId, sessionIndex)`, which now takes the captured values,
       builds the `LogoutRequest` and redirects. It no longer signs out locally (nop did).
  4. Clear the three customer attributes on the captured customer.
- **Known limit, documented, not fixed:** the OIDC handler looks for `id_token` in nop's *external* cookie for the
  `id_token_hint`. That cookie may be gone by logout time. Entra still ends the session without the hint, but it may
  show an account picker, and some IdPs ignore `post_logout_redirect_uri` without it.

### G2 — OIDC options are per first store, cached for the life of the process

**Now.** The `AddOpenIdConnect` delegate resolves `SsoConfigurationSettings` through `EngineContext` the first time
the options are built (the first request after start). Whichever store served that request supplies `Authority`,
`ClientId`, `ClientSecret` and `CallbackPath` to **every** store until restart.

**Fix — per-store options through the options cache, no nop change.**
- `Infrastructure/Oidc/SsoOidcOptionsConfigurer : IConfigureNamedOptions<OpenIdConnectOptions>` — for our scheme
  only, loads the **current store's** settings (`IHttpContextAccessor` → request services → `IStoreContext`,
  `ISettingService`, `ISsoSecretsService`) and sets Authority, ClientId, ClientSecret, CallbackPath, Scopes,
  SignedOutCallbackPath and SignedOutRedirectUri. Keeps the placeholder Authority / ClientId when unconfigured, so
  `Validate()` never throws on non-SSO traffic.
- `Infrastructure/Oidc/StoreAwareOidcOptionsCache : IOptionsMonitorCache<OpenIdConnectOptions>, ISsoOidcOptionsCache`
  — keys the cached options by *scheme name + current store id*. ASP.NET Core's `OptionsMonitor` builds them through
  the factory, which runs the configurer and then the framework's `OpenIdConnectPostConfigureOptions`, so each store
  gets its own discovery-document manager. Other schemes and names pass straight through.
- `ISsoOidcOptionsCache.InvalidateStore(storeId)` — called wherever per-store settings are saved, deleted or seeded
  (`ConfigurationModelFactory` save / delete / seed). Entries also expire after **15 minutes** as a backstop for web
  farms, where a save on one server does not reach the others.
- `OidcAuthenticationRegistrar` registers both through `builder.Services` and keeps only the static parts
  (response type, SaveTokens, claim mapping, events) in its delegate. The `EngineContext` call goes.

Result: each store can have its own Entra app registration, and a settings change applies without a restart.

### G3 — settings saved but never read

| Setting | Proposed | Behaviour |
|---|---|---|
| `AutoRegister` | **wire** | Off, and no customer has that email → `Failed` / `acs_auto_register_disabled` (the reason code already exists), back to `/login` |
| `PostLoginRedirectUrl` | **wire** | Used when the login has no `returnUrl`; ignored unless local (`IUrlHelper.IsLocalUrl` / `IWebHelper.IsLocalUrl`) |
| `PostLogoutRedirectUrl` | **wire** | Final landing page after OIDC and SAML sign-out (local only; default `/`) |
| `SignLogoutRequest` | **wire** | The SAML config for logout gets the SP signing certificate when this is on, independent of `SignedRequest`. `Saml2ConfigurationFactory.CreateForCurrentStoreAsync(SamlConfigPurpose)` picks the signing flag for `Login` or `Logout`; the PFX is loaded when either flag or `EncryptedAssertion` is on |
| `CustomAttributesJson` | **delete** (question Q2) | No extension reads it. Remove the setting, model property, view field, locale strings, factory lines and seed value |

### G4 — `AccessSsoPlugin` declared, never checked
Remove it from `ExternalAuthSSOPermissionConfigManager` and `ExternalAuthSSOPermissions`. Keep the
`DeletePermissionAsync("AccessSsoPlugin")` call in `UninstallAsync` as a literal, so older installs are cleaned up.

### G5 + G12 — SAML correlation cookie never reaches the ACS

**Now.** `.AspNetCore.ExternalAuthSSO.SamlCorrelation` is `SameSite=Lax`. The IdP returns with a **cross-site POST**,
on which browsers do not send Lax cookies. So the cookie is never present at `/saml/acs`: the `InResponseTo` check is
always skipped and the original `returnUrl` is always lost. Unsolicited (IdP-initiated) responses are accepted as a
side effect.

**Fix.**
- Issue the login correlation cookie with `SameSite=None; Secure`, the same as ASP.NET Core's own OIDC correlation
  cookie. The logout correlation cookie stays Lax, because the IdP's logout response returns as a top-level GET
  redirect, which carries Lax cookies.
- Missing cookie at the ACS: policy per question Q1. Recommended: **reject** (`Failed` / `acs_pending_cookie_invalid`)
  unless a new per-store setting `AllowIdpInitiatedLogin` (default off) is on.
- Over plain HTTP (local dev) `Secure` cookies are dropped. Document that SAML needs HTTPS end to end.

### G6 — dead mock branch inside the real ACS
`SamlResponseProcessor.ProcessAcsAsync` has a `#if DEBUG` branch that skips ITfoxtec validation when mock mode is on.
Nothing posts to it: the mock IdP (`MockSsoController`) calls the provisioning service directly. Delete the branch,
`ProcessMockAcsAsync`, `LoadXmlSafely` and `CreateSamlNamespaceManager`. `/saml/acs` then always validates, in every
build.

### G7 — Test connection has no SSRF guard
Move `EnsureUrlIsSafe` from `IdpMetadataImporter` to a shared `Services/Http/SafeUrl.cs` (HTTPS only, no
loopback/private/link-local addresses after DNS resolution). Call it before the OIDC discovery fetch and the SAML HEAD
probe. A blocked URL returns a failed check with the reason; nothing is fetched.

### G8 — plugin "active" flag ignored at login
`ExternalAuthenticationController.Login` already computes `methodIsAvailable`; use it. Not active for this
store/customer → same "not available" notification and redirect as SSO-off.

### G9 — blocked privileged mapping is silent
The group-mapping validator already rejects Administrators, so this only triggers on hand-edited data. Log a nop
warning (`ILogger.WarningAsync`) naming the mapping, store and role. No audit row.

### G10 — `BypassDenied` rows say protocol `Oidc`
Write an empty protocol (a bypass is a local login).

### G13 — provisioning (newly found)

**Now, two faults.**
1. An existing customer whose account is not yet linked to the IdP cannot sign in by SSO. nop's
   `AuthenticateAsync` sees no external record, tries to register, and stops with
   *Account.AssociatedExternalAuth.EmailAlreadyExists*. Design decision 15 ("auto-link by verified email") was never
   implemented.
2. The plugin decides `Linked` / `AutoRegistered`, role sync and the audit row from "does a customer with that
   email exist", not "did the sign-in succeed". A failed sign-in therefore still writes `Linked` and syncs roles.

**Fix.**
- Before calling nop: if no customer is linked to `(ExternalAuth.SSO, externalIdentifier)` but a customer with that
  email exists (and passed the admin-block check), call
  `IExternalAuthenticationService.AssociateExternalAccountWithUserAsync(customer, parameters)`. nop then finds the
  link and signs the customer in.
- After calling nop: treat the login as successful only if `IWorkContext.GetCurrentCustomerAsync()` is a registered
  customer with that email. Only then: role sync, `SignInProtocol`, and the `AutoRegistered` / `Linked` row (`Linked`
  only when this call created the link; a returning customer gets `Login`). Otherwise: `Failed` /
  `acs_customer_login_failed`, and nop's own error result (its message is already queued) is returned.
- nop's `CustomerSettings.UserRegistrationType` still applies on top of `AutoRegister`. With registration *Disabled*,
  new users are refused even when `AutoRegister` is on. This is documented, not overridden.

Also: the Goodtest seed has `AcsUrl = …/Admin/saml/acs`. The route is `/saml/acs`. Correct the seed.

---

## 2. Files

| File | Change |
|---|---|
| `Infrastructure/OidcAuthenticationRegistrar.cs` | static options only; registers configurer + cache (G2) |
| `Infrastructure/Oidc/SsoOidcOptionsConfigurer.cs` | **new** (G2) |
| `Infrastructure/Oidc/StoreAwareOidcOptionsCache.cs` + `Services/ISsoOidcOptionsCache.cs` | **new** (G2) |
| `Factories/ConfigurationModelFactory.cs` | invalidate OIDC cache on save / delete / seed (G2); drop `CustomAttributesJson` (G3) |
| `Services/SsoUserProvisioningService.cs` | link-by-email, success test, `AutoRegister`, `PostLoginRedirectUrl`, `SignInProtocol` (G1, G3, G13) |
| `Filters/LogoutActionsFilter.cs` | run nop first, then federate (G1, G11) |
| `Services/Saml/ISamlLogoutHandler.cs`, `SamlLogoutHandler.cs` | `InitiateAsync(nameId, sessionIndex)`; no local sign-out; post-logout URL; logout signing (G1, G3) |
| `Services/Saml/ISaml2ConfigurationFactory.cs`, `Saml2ConfigurationFactory.cs` | purpose parameter; logout signing (G3) |
| `Services/Saml/SamlChallengeBuilder.cs` | correlation cookie `SameSite=None; Secure` (G12) |
| `Services/Saml/SamlResponseProcessor.cs` | missing-cookie policy (G5); delete mock branch (G6); stop writing `SignInProtocol` (G1) |
| `Services/Http/SafeUrl.cs` | **new**, from `IdpMetadataImporter` (G7) |
| `Services/Saml/IdpMetadataImporter.cs`, `Services/SsoTestConnectionService.cs` | use `SafeUrl` (G7) |
| `Controllers/ExternalAuthenticationController.cs` | honour `methodIsAvailable` (G8) |
| `Services/SsoGroupRoleMappingService.cs` | warning log (G9) |
| `Filters/LocalLoginEnforcementFilter.cs` | empty protocol (G10) |
| `Security/ExternalAuthSSOPermissions.cs`, `…PermissionConfigManager.cs`, `SSOAuthenticationMethod.cs` | drop `AccessSsoPlugin` (G4); locale strings for new/removed fields |
| `Settings/SsoConfigurationSettings.cs`, `Models/ConfigurationModel.cs`, `Views/Configure.cshtml` | `AllowIdpInitiatedLogin` if Q1 = yes; remove `CustomAttributesJson` if Q2 = yes |
| `Controllers/ExternalAuthSSOController.cs` | seed: ACS URL, drop `CustomAttributesJson` |
| `References/SSO_Plugin_Reference.md` | §5–§8 and §11 updated |

---

## 3. Questions

- **Q1 — IdP-initiated SAML login.** Should a SAML response the store did not ask for (for example from the Entra
  "My Apps" tile) be accepted? Recommended: rejected by default, with a per-store `AllowIdpInitiatedLogin` switch.
- **Q2 — `CustomAttributesJson`.** Delete it (recommended) or keep it as an unused extension slot?
- **Q3 — linking existing customers by email (G13).** Turn on automatic linking of an existing non-admin customer to
  the IdP account with the same email? It is design decision 15, but it does trust the IdP's email claim. Recommended
  yes, because `AllowedEmailDomains` and the admin block still apply.

---

## 4. Test plan

Build: `dotnet build Plugins/Nop.Plugin.ExternalAuth.SSO/Nop.Plugin.ExternalAuth.SSO.csproj -p:SolutionDir=C:\Work\CW_NOP_5\`
with no new warnings. Then, once an IdP is available (Keycloak covers both protocols), on **two stores with
different IdP settings**, over HTTPS:

| # | Case | Expected |
|---|---|---|
| 1 | OIDC login, store A then store B | each goes to its own IdP / client ID; no restart between (G2) |
| 2 | Change store A's client ID, save, log in again | new client ID used without restart (G2) |
| 3 | Existing non-admin customer, first SSO login | signed in; `Linked` row (G13) |
| 4 | Same customer again | signed in; `Login` row |
| 5 | Existing admin email | blocked, `acs_admin_account_blocked` |
| 6 | New email, `AutoRegister` off | blocked, `acs_auto_register_disabled` (G3) |
| 7 | New email, `AutoRegister` on, nop registration Standard | `AutoRegistered`, roles from mappings |
| 8 | Login without `returnUrl`, `PostLoginRedirectUrl` = `/cart` | lands on `/cart`; external URL ignored (G3) |
| 9 | OIDC logout | nop activity "PublicStore.Logout", redirect to IdP end-session, back to post-logout URL (G1) |
| 10 | SAML logout, `SignLogoutRequest` on, `SignedRequest` off | LogoutRequest signed, AuthnRequest not (G3) |
| 11 | Admin impersonating an SSO customer clicks Logout | back on the admin customer page, admin still signed in (G11) |
| 12 | SAML SP-initiated login | `InResponseTo` checked; lands on the original `returnUrl` (G12) |
| 13 | SAML response with no correlation cookie | rejected (or accepted when `AllowIdpInitiatedLogin`) (G5) |
| 14 | DEBUG build, mock mode on, forged POST to `/saml/acs` | rejected by ITfoxtec (G6) |
| 15 | Test connection with `https://127.0.0.1/...` authority | "blocked URL", no request made (G7) |
| 16 | Plugin inactive for the store, hit `ExternalAuthentication/Login` | "not available" (G8) |
| 17 | Non-admin uses bypass | `BypassDenied` row, empty protocol (G10) |
| 18 | **Feature off** — store with SSO disabled | normal login, no button, logout untouched, no OIDC options built from that store's empty settings breaking anything |
