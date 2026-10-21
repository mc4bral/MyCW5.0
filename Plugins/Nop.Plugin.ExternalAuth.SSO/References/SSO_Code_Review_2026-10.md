# SSO plugin — code review (nopCommerce 5.00 / .NET 10)

| | |
|---|---|
| **Scope** | `Nop.Plugin.ExternalAuth.SSO` as it stands on 2026-10-19, after the 5.00 port and the G1–G14 fixes |
| **Method** | Read every service, filter, controller and view; compared with nopCommerce 5.00's own code and its `ExternalAuth.Facebook` plugin; checked library APIs in the restored NuGet packages; smoke-tested the running site |
| **Status** | Findings. **D1–D6, R1–R9 and features F1–F5 done 2026-10-19** (approved); P3, the larger items and the planned-but-never-built items done 2026-10-20, with D7. **First real-IdP test (Keycloak) 2026-10-21:** C1 and C2 settled, D8–D14 found and fixed (D10 on 2026-10-21, plugin `…_DATA_0.10`) |

**"SSL" in the request is read as SSO** (sign-on libraries). TLS itself is handled by Kestrel / IIS and needs no
library; certificate handling already uses .NET 9+'s `X509CertificateLoader`, which is the current API.

---

## 1. Summary

The plugin's architecture is sound: one provisioning path for both protocols, nop's external-auth pipeline for
sign-in, ITfoxtec for SAML, ASP.NET Core's handler for OIDC, secrets encrypted at rest, an audit log. Most of what
needs work is **duplication, file size and a few framework idioms the earlier model did not use**. There are also
**three defects** worth fixing before anyone tests against a real IdP, and **two behaviours to confirm** on that
first test.

| Priority | Count | What |
|---|---|---|
| **P0 — defect** | 14 | broken view on multi-store sites; audit regression from G13; Entra group overage revokes roles; Configure save blocked (D4); store-0 audit rows rejected (D5); a store-scope save blanked the system-wide settings (D6); saving Configure needed the secret re-entered (D7); **from the real-IdP test (§2.1):** crash for customers without a password (D8), app roles ignored over OIDC (D9), admin lockout (D10), SAML request corrupted by nop's redirect (D11), response binding not requested (D12), self-signed IdP certificates refused (D13), SAML log-out response unreadable (D14) |
| **P1 — confirm on first IdP test** | 2 | SAML certificate chain validation; OIDC claim mapping — **both settled 2026-10-21** |
| **P2 — design / reuse** | 9 | metadata parser, certificate helper, audit API, controller split, scripts, … |
| **P3 — style** | 7 | field modifiers, naming, constants, … |

Recommended libraries: **keep both** — ASP.NET Core `OpenIdConnect` for OIDC and **ITfoxtec.Identity.Saml2** for
SAML (§5). Use more of ITfoxtec than the plugin does today.

---

## 2. P0 — defects

### D1. Store-scope view fails to compile on any site with two or more stores — FIXED 2026-10-19

`Views/Components/StoreScopeSSO/Default.cshtml` line 5: `@inject Nop.Core.IWebHelper webHelper`. nop 5 moved
`IWebHelper` to `Nop.Services.Helpers`. Plugin views compile **at runtime** (`AddRazorRuntimeCompilation`), so the
build passes and the error only appears when the view renders — and the component renders only when there are
2+ stores. The local database has one store, which is why the smoke test passed.

**Fix:** `@inject Nop.Services.Helpers.IWebHelper webHelper`. **Lesson for every ported plugin:** grep `.cshtml`
files for types that moved (`Nop.Core.IWebHelper`, AutoMapper types) — the compiler will not find them.

### D2. Returning customers no longer counted on the SSO Accounts page (introduced by G13) — FIXED 2026-10-19

The G13 fix writes `Login` for a returning customer. But `Login` already means "SAML challenge started"
(`SamlChallengeBuilder`, `SsoAuthEventTypes` doc comment), and `SsoStoreSummaryService` counts only
`AutoRegistered` / `Linked` as successes. So "Last successful login" and "Users" stop moving for returning customers,
and the audit filter "Login" mixes two meanings.

**Fix:** a new `SsoAuthEventTypes.SignedIn` for a returning customer, added to `SuccessEventTypes` and the audit-log
event filter; `Login` keeps "challenge started". (OIDC writes no challenge row at all; either add one in
`ExternalAuthenticationController.Login` or drop the SAML one — today the two protocols differ.)

### D3. Entra group "overage" silently revokes mapped roles — FIXED 2026-10-19 (option 1)

Entra stops sending the `groups` claim when a user is in more groups than fit in the token (200 for JWT, 150 for
SAML). It sends an overage marker instead (`_claim_names` / `hasgroups` in OIDC,
`http://schemas.microsoft.com/claims/groups.link` in SAML). The plugin reads no groups, so role sync treats the user
as having **none** and removes every owned role.

**Fix (pick one, smallest first):**
1. Detect the overage markers in provisioning; when present, **skip role sync** and write a `Failed` /
   `groups_overage` note. Nothing is revoked on missing data.
2. Ask the IdP team to emit only *groups assigned to the application* (Entra setting), which avoids overage.
3. Map **app roles** (`roles` claim) instead of groups — Entra's recommended pattern; no overage, readable names.
4. Fetch groups from Microsoft Graph on overage (needs `GroupMember.Read.All` and a Graph client — most work).

### D4. Configure could not be saved without a default role — FIXED 2026-10-19 (found in the R1–R5 smoke test)

`ConfigurationModel.SelectedCustomerRoleIds` was a non-nullable `IList<int>` with no initialiser. In a nullable
context MVC treats that as `[Required]`, so any Configure save — per store or "All stores" — with no default role
selected came back with "The SelectedCustomerRoleIds field is required." and saved nothing. Now initialised to an
empty list (also clears its CS8618 warning).

### D5. Audit rows for store 0 are rejected by the database — FIXED 2026-10-19 (found in the R1–R5 smoke test)

`SsoAuthEventBuilder` declares `StoreId … .ForeignKey<Store>()`, but the plugin writes store **0** for
system-wide rows. Store 0 is not a store, so the insert fails:
- a Configure save at "All stores" → its `ConfigChanged` row is lost (logged to nop's log as "failed to write");
- **Clear audit log** → the `LogCleared` breadcrumb is inserted *outside* the try/catch, so the action fails;
- `ConfigChanged` also takes the scope from the **posted** `ActiveStoreScopeConfiguration`, not the scope the
  factory actually saved, so a per-store save can be recorded as store 0 (seen in the test).

Present since 4.9. The local `SSO_AuthEvent` table was empty because every earlier write had hit this.
**Fix:** a migration that drops `FK_SSO_AuthEvent_StoreId_Store_Id` (an audit log should outlive a deleted store and
never block a write), remove `.ForeignKey<Store>()` from the builder, and log `ConfigChanged` with the factory's
scope. `SSO_GroupRoleMapping.StoreId` keeps its foreign key — mappings are always for a real store.

> **Done 2026-10-19.** `DropAuditStoreForeignKeyMigration` (an `Update` migration, plugin version `…_DATA_0.3`) drops
> the key; `SsoAuthEventBuilder` no longer declares it; `ConfigChanged` logs the factory's saved scope. Verified
> locally: the key is gone, an "All stores" save writes its store-0 `ConfigChanged` row, and Clear audit log works
> (its `LogCleared` row is the only one left).

### D6. A store-scope Configure save overwrote the system-wide settings — FIXED 2026-10-19 (found in the F3 test)

`ApplyModelToSettingsAsync` saved `SsoSystemSettings` (store 0) on **every** save. At a store scope the page shows
only the per-store cards, so the system-wide fields arrived empty or 0 and were written over the real values:
`LocalLoginAllowedRoles` became empty (local sign-in for nobody on SSO stores), `LoginAttemptCapPerHour` 0 (rate cap
off), `AuditRetentionDays` 0 (keep forever). Present before this review.
**Fix:** at a store scope the factory saves the per-store settings and returns; system-wide settings are saved only
at "All stores". Verified: the values restored at "All stores" (Administrators / 60 / on / 365) were unchanged by a
following store-1 save.

### 2.1 Defects found by the first real-IdP test (Keycloak 26, 2026-10-21)

Keycloak ran in a GitHub Codespace with a test realm (two users, two groups, one app role, an OIDC and a SAML client);
nop ran locally. Results and the test log are in the reference (§12). None of these could show with the DEBUG mock IdP,
which hands provisioning a ready-made principal; D9, D11 and D13 would have broken sign-in with Microsoft Entra too.

#### D8. Existing customer without a password: nop throws after an SSO sign-in — FIXED
nop's `CookieAuthenticationService.GetAuthenticatedCustomerAsync` compares the customer's latest password date with the
cookie on every request and assumes every registered customer has a password record. A customer created by an admin
without a password (or imported) has none: the sign-in succeeded, the next page threw `NullReferenceException`.
nop-registered SSO customers were unaffected (nop gives them a random password). **Fix (plugin, step 6b of
provisioning):** before signing in an existing customer with no password record, set a random 32-byte password through
`ICustomerRegistrationService.ChangePasswordAsync`. Verified with a password-less test customer.

#### D9. App roles ignored over OIDC — FIXED
.NET's OIDC handler renames inbound JWT claims (`JsonWebTokenHandler.DefaultInboundClaimTypeMap`): `roles` and `role`
become `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`; `groups` is not renamed. Provisioning matched
the configured `RolesClaimType` (`roles`) exactly, so F2 never saw an app role over OIDC — Entra included. **Fix:**
`IsConfiguredClaimType` also accepts the renamed form. Verified: a user with app role `Store.Manager` got its mapped
role; 7 unit tests.

#### D10. "Redirect login to SSO" with no bypass token locks admins out — FIXED 2026-10-21 (plugin `…_DATA_0.10`)
With `RedirectLoginToSso` on, `/login` always goes to the IdP; the way past it is `?ssobypass=<token>`, and an empty
token turns the bypass off; admin accounts are refused by SSO by design. So no admin can reach the store's own login,
and the setting can only be undone in the database. Likely in practice: the redirect is on by default and the token
starts empty. **Fix:** one rule, `SsoMonitoringService.LocksAdminsOut`, used in three places —
1. Configure refuses a store save that turns the redirect on while no token is stored;
2. Configure refuses an "All stores" save that clears the token while any store redirects;
3. System → Warnings reports a store already in that state as a **failure**.

Found with it, same family: **a redirect loop** when the plugin is switched off in nop's external sign-in methods but
a store still redirects — the challenge refuses and returns to `/login`, which redirected again. `LoginActionsFilter`
now redirects only while the plugin is active for the store and customer. Verified: both refusals, the bypass link
(`/login` → SSO, `/login?ssobypass=…` → nop's form), the warning on a simulated lockout, and `/login` → 200 with the
plugin off; 6 unit tests.

#### D11. Every SAML request corrupted by nop's redirect handling — FIXED
nop replaces ASP.NET's `RedirectResult` executor (`NopRedirectResultExecutor`); with
`SecuritySettings.AllowNonAsciiCharactersInHeaders` on it `WebUtility.UrlDecode`s the whole address. ITfoxtec's
correctly escaped `SAMLRequest` came out with raw `+` `/` `=`; the IdP read `+` as a space and refused the request
(Keycloak: `invalid_token`, no client). OIDC was not affected (its handler redirects by itself). **Fix:**
`Services/Saml/SamlRedirectResult` sets the `Location` header directly; used for the AuthnRequest and the SP-initiated
LogoutRequest. Proven by decoding the request and replaying it re-encoded.

#### D12. SAML response binding not requested — FIXED
The AuthnRequest named no `ProtocolBinding`; Keycloak answered on the binding the request came by and put the
`SAMLResponse` in the URL, which the POST-only ACS refused (404). **Fix:** `ProtocolBinding = HTTP-POST` on the
AuthnRequest. (Entra always posts; the SAML web SSO profile requires POST or Artifact for responses.)

#### D13. Self-signed IdP signing certificates refused — FIXED (settles C1)
See C1 below. `Invalid X509 certificate chain … terminated in a root certificate which is not trusted`.

#### D14. SAML log-out response unreadable — FIXED
`UnbindEither(HttpRequest, Saml2Request)` was called with a `Saml2LogoutResponse`. In ITfoxtec that type derives from
`Saml2Request` (which derives from `Saml2Response`), so overload resolution always chose the *request* `Unbind`, which
looks for `SAMLRequest` — "HTTP Query String does not contain SAMLRequest". The IdP session still ended, but the
answer was never validated. **Fix:** a separately named `UnbindResponseEither(HttpRequest, Saml2Response)`. Verified:
`Slo` with no `Failed` after it.

---

## 3. P1 — confirm on the first real IdP test

### C1. SAML signing-certificate validation mode

`Saml2ConfigurationFactory` never sets `Saml2Configuration.CertificateValidationMode` or `RevocationMode`, so
ITfoxtec's defaults apply. Entra's SAML signing certificates are self-signed; chain validation of a self-signed
certificate fails, and online revocation checks add latency or fail behind a proxy. Trust here comes from the
**pinned** certificate in `SignatureValidationCertificates`, so the usual setting is
`CertificateValidationMode = None` and `RevocationMode = NoCheck`.
**Do:** if the first Entra SAML login fails signature validation, set both explicitly in the factory (one place) and
document why.

> **Settled 2026-10-21 (D13).** Keycloak's self-signed certificate was refused exactly as predicted. The factory now
> sets `CertificateValidationMode = None` and `RevocationMode = NoCheck`, with the reason in a comment. ITfoxtec checks
> a signature only against `SignatureValidationCertificates`, so the pinned certificate is the trust. With `None` the
> library also skips expiry: an expired pinned certificate is still accepted (usual for SAML pinning); System → Warnings
> reports it (reference §7.4).

### C2. OIDC claim mapping

The handler maps inbound claims by default (`sub` → `ClaimTypes.NameIdentifier`, etc.), and provisioning reads the
mapped names. That works, but it is implicit. Current ASP.NET Core guidance is `MapInboundClaims = false` and reading
the raw JWT names (`sub`, `email`, `given_name`, `groups`, `roles`). **Do:** decide once — either keep mapping and
say so in the registrar, or turn it off and change the default claim types in `SsoConfigurationSettings`. Changing it
alters every store's claim-type defaults, so do it before go-live, not after.

> **Settled 2026-10-21: keep the mapping.** Against Keycloak the defaults worked for email, names, subject and groups
> with no change. The one gap was `roles`, which the handler renames (D9); provisioning now accepts both forms, so the
> stored claim-type defaults stay as they are and no store needs reconfiguring.

---

## 4. P2 — design and code reuse

### R1. Use ITfoxtec's metadata reader instead of a hand-written parser
`IdpMetadataImporter` (358 lines) parses `EntityDescriptor` XML with `XDocument`. ITfoxtec 4.17 already has
`EntityDescriptor.ReadIdPSsoDescriptor(xml)` and `ReadIdPSsoDescriptorFromUrlAsync(IHttpClientFactory, Uri)`,
returning entity ID, SSO/SLO services and signing certificates. Keep `SafeUrl` in front of the URL fetch and the
size cap; drop the parsing.

> **Done 2026-10-19.** Both the import and the handshake now go through one `ReadSnapshot` on ITfoxtec's reader
> (the two hand-written parsers are gone); URL import also gains the 1 MB cap and BOM handling. Probed before the
> switch: plain, XML-declared and federation `EntitiesDescriptor` documents read correctly; DTD/XXE is refused; a
> leading BOM is not accepted by ITfoxtec, so the plugin decodes and strips it. ITfoxtec does **not** verify a
> metadata signature — neither did the old code; "signed" is still reported as presence only.

### R2. One certificate helper
PEM-stripping + `Convert.FromBase64String` + `X509CertificateLoader` appears in **five** places
(`Saml2ConfigurationFactory`, `SsoStoreSummaryService`, `SsoTestConnectionService` ×2, `IdpMetadataImporter`).
One `SsoCertificates` helper: `TryLoadIdpCertificate(settings, out cert)`, `LoadSpPfx(settings)`, `NotAfter`.

> **Done 2026-10-19.** `Services/SsoCertificates`: `Load` / `TryLoad`, `TryLoadIdpCertificate`, `LoadSpPfx`,
> `Sha256Thumbprint`. Callers now dispose the certificates they load (`using`), which the copies did not.

### R3. Store summary queries the database properly
`SsoStoreSummaryService.PopulateTelemetryAsync` loads **every** audit row for a store into memory to compute a min,
a max and a distinct count, once per store. It grows with the audit log and resets when the log is cleared.
**Fix:** one grouped LINQ query (`GroupBy(StoreId)` with `Min`/`Max`) executed by LinqToDB, and count users from
nop's own `ExternalAuthenticationRecord` table (`ProviderSystemName = "ExternalAuth.SSO"`), which is the real link
table and survives a log clear.

> **Done 2026-10-19, with a correction.** `ExternalAuthenticationRecord` has **no store column**, so it cannot give
> a per-store count; the audit log stays the source (and still resets on Clear). The figures are now three grouped
> SQL queries for all configured stores together, instead of loading every row per store.

### R4. Audit-writing API
`ISsoEventService.LogAsync` takes seven positional arguments and is called ~30 times; three classes each grew their
own `FailAsync` wrapper. Replace with a small record (`SsoAuditEntry`) or two helpers on the service —
`LogFailureAsync(protocol, reason, detail, email, subject)` and `LogSuccessAsync(...)` — and delete the wrappers.

> **Done 2026-10-19.** `LogAsync` takes only what the row needs (store, customer, email, subject and notes are
> optional; store defaults to the current one). `LogFailureAsync(protocol, reason, detail, …)` formats the
> `[reason=…]` prefix. The `FailAsync` helpers stay as one-liners, because they also return each flow's redirect.
> Two failures gained reason codes: `oidc_callback_failed`, `mock_failure`. Unused store lookups and one unused
> dependency (`IStoreContext` in `LocalLoginEnforcementFilter`) went with them.

### R5. New-customer names through nop's event, as nop's Facebook plugin does
nop publishes `CustomerAutoRegisteredByExternalMethodEvent` for exactly this. Move `ApplyNamesAsync` (added in G13)
into an `IConsumer<CustomerAutoRegisteredByExternalMethodEvent>` filtered on `ProviderSystemName ==
"ExternalAuth.SSO"`, reading the configured claim types. Provisioning gets shorter and the pattern matches nop.

> **Done 2026-10-19.** `Infrastructure/SsoCustomerRegisteredEventConsumer` (auto-discovered by nop). It also covers
> customers nop holds for validation or approval, which the inline version handled as a special case.

### Security note — `System.Security.Cryptography.Xml` (found during R1)
ITfoxtec 4.17 references `System.Security.Cryptography.Xml` 10.0.0 (5 high-severity advisories) and `.Pkcs` 10.0.0
(1 moderate). The build never shows this: nop's `Directory.Build.props` suppresses NU1901–NU1904. At runtime both
assemblies come from the **ASP.NET Core shared framework**, not the package — the installed 10.0.12 runtime carries
patched copies. **Keep every server's ASP.NET Core 10 runtime patched**; that is what closes these, not the plugin.

### R6. Split the admin controller; attributes at class level
`ExternalAuthSSOController` (640 lines, 25 actions, 14 dependencies) covers four features, and repeats
`[AuthorizeAdmin]` + `[Area]` + `[CheckPermission]` on all 22 actions. nop's pattern: one controller per area of
work, attributes on the class. Suggested: `SsoConfigurationController` (Configure, import, onboarding report,
scope), `SsoAccountsController` (list, add, test, delete), `SsoGroupRoleMappingController`, `SsoAuditLogController`.
While there: **`ChangeStoreScopeSSOConfiguration` has no `[CheckPermission]`** — any admin can change the scope
(low impact, but every other action is gated).

> **Done 2026-10-19.** `SsoConfigurationController`, `SsoAccountsController`, `SsoGroupRoleMappingsController`,
> `SsoAuditLogController` on an abstract `SsoAdminControllerBase` (`[AuthorizeAdmin]`, `[Area]`,
> `[AutoValidateAntiforgeryToken]` once; `PluginView(name)`); controller names in `Constants/SsoAdminControllers`.
> nop's `[CheckPermission]` is method-only, so it stays on each action. URLs moved from `/Admin/ExternalAuthSSO/…` to
> `/Admin/Sso{Configuration|Accounts|GroupRoleMappings|AuditLog}/…` (menu, plugin Configure link and views updated;
> the old URLs 404). Fields `protected readonly`, actions `virtual`, as nop does. Found and fixed on the way:
> - **choosing "All stores" in the scope dropdown did nothing** (the action only accepted an existing store) — fixed,
>   and the action now has its permission check;
> - a failed Configure save redisplayed the form with **empty role and binding dropdowns** — the lists are now kept;
> - `ChangeSSOSope` → `SetStoreScopeAsync`, `GetCurrentCustomer` → `GetCurrentCustomerAsync`; the scope attribute
>   name is a constant.

### R7. Scripts out of the views
`Configure.cshtml` has a 199-line inline `<script>` with 22 Razor expressions inside it; `SsoAccounts.cshtml` a
141-line one that builds `onclick="ssoTestConnection(' + storeId + …)"` buttons calling globals. That is the pattern
CLAUDE.md §9.14–9.15 rules out (encoding bugs, no bundling, globals). Move each into `Content/Admin/*.js`, pass
URLs and labels through a `data-*` config div, bind delegated handlers with a namespace. `Configure.cshtml` (1,090
lines) also splits naturally into one partial per card.

> **Done 2026-10-19** (the partial split is not — Configure is ~880 lines now, still one file).
> `Content/Admin/sso-admin.js` (`window.SsoAdmin`): one module for both pages, reading `.sso-configure-config` /
> `.sso-accounts-config` divs (URLs, store id, a JSON resource bundle); delegated, namespaced handlers; grid renderers
> as `SsoAdmin.render.certExpiry` / `.accountActions`; no `onclick`, no Razor in script; registered with
> `NopHtml.AddScriptParts` and in the csproj. **Three UI bugs the old scripts hid, fixed:** nop's admin JSON is
> PascalCase, but the scripts read camelCase, so (1) **Import IdP metadata filled nothing**, (2) **Test connection
> always showed an empty red ✗**, (3) **certificate-expiry badges never turned amber/red**. The module reads either
> casing. Also: the optional default-roles field no longer shows nop's "required" marker; the handshake checklist only
> shows when metadata was actually read. Verified in the browser: protocol visibility, Select2, onboarding report,
> import fill, Test (green for a reachable OIDC authority), grid buttons, Delete with grid reload; no console errors.
> Not moved: the small inline script on `MockSso/Login.cshtml` (DEBUG only) and the `onclick` that sets
> "continue editing" on `CreateOrUpdateGroupRoleMapping.cshtml`.

### R8. Settings that say what they hold
- `DefaultRoleSystemName` holds a CSV of role **ids** (G14). Rename to `DefaultRoleIds` (`List<int>`, which nop
  settings serialise) with a one-off update step that converts old values.
- `LoginBinding` / `LogoutBinding` are free strings compared with `Contains("POST")`. Make them an enum.
- The "Administrators" literal appears four times (two services, a factory, a validator) — use
  `NopCustomerDefaults.AdministratorsRoleName` from one shared `SsoRoles.Privileged` set.

> **Done 2026-10-19** (with the "Goodtest" seed moved behind `#if DEBUG`, in `SeedTestStoreAsync`).
> - `DefaultRoleIds` (`List<int>`) on settings and model; the picker no longer offers Administrators (it did), and the
>   save drops unknown or privileged ids even when posted directly. Locale key renamed to `Fields.DefaultRoles`.
> - `SamlBinding` enum (`HttpRedirect` / `HttpPost`) with `ToUrn()` / `ToDisplayName()`; the two free-text boxes are
>   dropdowns; three private string parsers deleted.
> - `Constants/SsoRoles` replaces three private lists and a validator literal; the bypass-role default uses
>   `NopCustomerDefaults.AdministratorsRoleName`.
> - Plugin version `…_DATA_0.4`: `ConvertRetypedSettingsAsync` converted the local rows (`Registered` → id 2,
>   `HTTP-Redirect` → `HttpRedirect`) for stores 0 and 1. Verified: Configure shows the converted values, a save
>   round-trips, and a tampered Administrators id is dropped.

### R9. Secrets: one layer, not two
`SsoSecretsService` wraps nop's `IEncryptionService` around ASP.NET Core Data Protection. nop's key lives in the same
database as the ciphertext, so the outer layer adds little protection but doubles the failure modes (either key
changing makes secrets unreadable). **Recommendation:** Data Protection only, with its key ring persisted outside the
database (nop already persists keys to `App_Data/DataProtectionKeys`, or Redis/Azure when configured). Needs a
one-off re-encrypt step for existing values, so schedule it with another release that touches settings.

> **Done 2026-10-19.** `SsoSecretsService` writes `dp1:` + Data Protection ciphertext (the prefix records the
> format); values without the prefix are read the old two-layer way. Plugin version `…_DATA_0.5`:
> `ReprotectLegacySecretsAsync` rewrites every legacy secret row (all stores) and leaves alone any value this server
> cannot decrypt rather than destroy it. Verified with four real legacy values (client secret, IdP certificate, PFX
> password, bypass token): all converted (e.g. 428 → 159 characters), the certificate preview on Configure matched
> its pre-conversion value, and a new save is stored in the new format. Test values removed afterwards.
> **Follow-up:** once no install holds legacy values, delete the legacy branch and the `IEncryptionService`
> dependency from `SsoSecretsService`.

---

## 5. Libraries

### 5.1 OIDC — keep `Microsoft.AspNetCore.Authentication.OpenIdConnect`

| Option | Verdict |
|---|---|
| **ASP.NET Core OIDC handler** (current) | **Keep.** Standard, any IdP, supported with .NET. PKCE on by default for code flow; .NET 9+ adds Pushed Authorization Requests (`PushedAuthorizationBehavior`), used automatically where the IdP advertises it |
| Microsoft.Identity.Web | Entra-only conveniences (Graph client for group overage, token cache, multi-tenant issuer validation). Brings its own `Microsoft.IdentityModel.*` floor, which collides with the host-version pin (§10.1 of the reference) and ties the plugin to Entra. **Not now.** Revisit only if Graph group lookup (D3 option 4) is chosen |

Improvements inside the current handler:
- **`id_token_hint` at logout** — done as F1 (§5.3).
- **App roles** (`roles` claim) as a mapping source — done as F2 (§5.3).

### 5.2 SAML — keep ITfoxtec.Identity.Saml2, use more of it

| Option | Verdict |
|---|---|
| **ITfoxtec.Identity.Saml2** (current) | **Keep.** BSD-3, active, targets .NET 10. Its controller-style API builds a `Saml2Configuration` per request, which is exactly what per-store SAML needs. Use its metadata reader (R1) and set validation modes explicitly (C1). Constraint: its `Microsoft.IdentityModel.*` floor must stay at or below the host's, which pins it at 4.17.0 today |
| Sustainsys.Saml2 | Mature, but built as an ASP.NET Core *authentication handler* configured at startup — the same per-store-options problem OIDC had (G2), with no hook as simple as ITfoxtec's per-request configuration. Its next major version has been a long-running rewrite. **No benefit for a multi-store nop site** |
| ComponentSpace SAML for ASP.NET Core | Commercial. Strong multi-tenancy (per-request configuration resolver), good Entra docs, vendor support. Worth it only if SAML becomes business-critical across many tenants and support contracts matter |
| Rock Solid Knowledge SAML2P | Commercial, designed around Duende IdentityServer. Wrong shape for an SP-only plugin |

**Strategic note:** Entra recommends OIDC for new applications. If every customer IdP is Entra, steer new stores to
OIDC and keep SAML for IdPs that require it.

### 5.3 Features F1–F5 — done 2026-10-19 (plugin version `…_DATA_0.6`)

| # | Feature | What was built | Verified |
|---|---|---|---|
| F1 | `id_token_hint` at OIDC logout | `LoginCallback` reads the `id_token`; provisioning saves it on the customer (`SsoCustomerAttributes.OidcIdToken`). `LogoutActionsFilter` puts it in the sign-out properties and `OnRedirectToIdentityProviderForSignOut` sets `IdTokenHint`. Cleared at logout with the other SSO attributes | Build only — needs an IdP |
| F2 | App roles as a mapping source | New per-store setting `RolesClaimType` (default `roles`). Provisioning reads the values of `GroupsClaimType` **and** `RolesClaimType`; a mapping takes a group Object ID or an app-role value (pattern `^[A-Za-z0-9][A-Za-z0-9._:/\-]{0,99}$`). No schema change | Validator accepts `Store.Manager` and a GUID, rejects `has space` |
| F3 | Audit retention | System-wide `AuditRetentionDays` (default 365, 0 = keep forever). Daily nop scheduled task **SSO audit log retention** (`SsoAuditRetentionTask`) deletes older rows; registered on install and update, removed on uninstall | Task registered (86 400 s); *Run now* deleted a two-year-old row and kept a new one; a negative value is rejected |
| F4 | Rate cap on `/saml/acs` | `ISsoRateLimiter` has buckets (`login`, `acs`), each its own per-IP counter under `LoginAttemptCapPerHour`; the ACS checks it before any XML or signature work | With a cap of 2 the third POST was refused (`login_retry_cap_hit`) |
| F5 | OIDC "Login started" row | `Login` writes a `Login`/`Oidc` audit row before the challenge, as SAML does | `GET /ExternalAuthentication/Login` → 302 to `login.microsoftonline.com`, row written |

---

## 6. .NET 10 / nop 5 idioms

Follow **nop's** style where nop has one, because the plugin lives inside it:

| Item | Recommendation |
|---|---|
| Primary constructors, collection expressions | **Do not adopt.** nop 5's own services use neither (0 of ~850 services); mixing styles costs more than it saves |
| Field modifiers | nop uses `protected readonly` (852 vs 2 in Nop.Services); the plugin uses `private readonly` (139 vs 1). Switch, so subclasses and overrides work the nop way |
| `ConstantTimeEquals` in `LoginActionsFilter` | Replace with `CryptographicOperations.FixedTimeEquals` over the UTF-8 bytes of SHA-256 hashes of both values — fixed-time *and* no length leak, framework-provided |
| GUID regex in the mapping validator | `[GeneratedRegex]` (source-generated) or simply `Guid.TryParseExact(value, "D", …)` |
| Outbound HTTP (metadata, discovery, HEAD probe) | `Microsoft.Extensions.Http.Resilience` → `AddStandardResilienceHandler()` on the named client gives timeouts, retries and a circuit breaker in one line. Optional |
| Rate limiter | `IStaticCacheManager` get-then-set is not atomic and the window restarts on each write. Fine for "stop runaway loops"; if a real limit is needed, `System.Threading.RateLimiting.PartitionedRateLimiter` per IP (single server) — the cache approach is the one that works on a web farm with Redis |
| `DateTime.UtcNow` (10 uses) | `TimeProvider` would make the code testable, but nop 5 does not use it; adopt only together with the test project (CLAUDE.md §22 Note A) |
| Mock IdP behind `#if DEBUG` | Keep the compile-time gate — it is stronger than `IWebHostEnvironment.IsDevelopment()`, which a mis-set environment variable defeats |

---

## 7. P3 — style

> **Done 2026-10-19 (plugin version `…_DATA_0.7`), awaiting approval.** The rename and the scope-attribute constant
> were already done during the R work. Fields are now `protected readonly`. The locale strings moved to
> `Infrastructure/SsoLocaleResources.cs`: 11 unused keys removed (the update deletes them) and the mapping intro
> mentions app roles. Milestone tags and `SSOPlan.v2.md` pointers are replaced with plain text or reference-doc
> sections. `NopStartup` is grouped by feature, and the store-scope component's remarks say why it exists.
> Verified: the build is clean, all SSO admin pages return 200, and 251 strings are installed.
>
> **Follow-up 2026-10-20 (CLAUDE.md compliance check):** 12 files had their public region before `Utilities`; reordered
> to Fields → Ctor → Utilities → public, with several topic regions nested under one `Utilities` (code lines
> unchanged). The mock login page's inline script and style moved to `Content/MockSso/` (hints as `data-hint` on the
> radios; tags written raw because the page has no layout). The mapping form's "Save and continue" `onclick` and hidden
> field were replaced by nop's `[ParameterBasedOnFormName("save-continue", "continueEditing")]`. Verified in the browser:
> continue-editing on create and edit, Save back to the list, every mock scenario's hint and Groups toggle.
>
> **Small fixes 2026-10-20 (plugin version `…_DATA_0.8`):**
> - The *IdP metadata URL* hint claimed the IdP fields are auto-refreshed from it. Nothing does that — the URL is read
>   only by Test connection's handshake. The hint now says so and points to *Import IdP metadata*.
> - Bypass token: `ConstantTimeEquals` replaced by SHA-256 of both values + `CryptographicOperations.FixedTimeEquals`
>   (§6), so the stored token's length no longer shows. Verified: no token and a wrong token redirect to SSO, the
>   right one serves the login page and sets the bypass cookie.
> - New `AddSsoAccountModelValidator`: the store must be one the picker offers (a store without SSO). It replaces the
>   controller's hard-coded English warning and also refuses a tampered store id. Verified: 0 and 999 refused.
> - `sso-admin.js` moved to `Content/Admin/Sso/` (CLAUDE.md §9.8).
> - `StoreScopeConfigurationModel`: three nullable warnings fixed — the plugin now builds with 0 warnings.
>
> **Larger items 2026-10-20 (decisions by the user):**
> - **Configure view split** — one partial per card in `Views/Configure/` (12); `Configure.cshtml` keeps the form,
>   scope picker and `<nop-card>` wrappers (900 → ~270 lines). Content moved line for line (checked: same lines,
>   none lost or doubled). Three stale comments corrected. Verified: both scopes render, the protocol switch shows and
>   hides the right cards, a store save round-trips across partials.
> - **Old-format secrets removed** — no install held them. `SsoSecretsService` reads `dp1:` only (no prefix → refused);
>   the `IEncryptionService` dependency, `IsCurrentFormat` and the update's converter are gone.
> - **Tests** — new `Tests/CW.Tests` project (CLAUDE.md §22 Note A), first suite for this plugin: 66 cases, pure
>   logic. `ComputeRoleChanges` was pulled out of `SyncCustomerRolesAsync` so the role diff is testable; helpers the
>   tests reach are `internal` (`InternalsVisibleTo CW.Tests`). A deliberately broken wildcard rule was caught by
>   its test, then restored.
>
> **Planned-but-never-built items 2026-10-20 (plugin version `…_DATA_0.9`; decisions by the user):** daily
> "SSO certificate and metadata check" task (metadata comparison, opt-in automatic apply per store, one
> `ScheduledCheck` audit row per finding); certificate and client-secret expiry warnings in nop's System → Warnings
> (`SystemWarningCreatedEvent` — the plugin now references `Nop.Web`); new settings `AutoApplyIdpMetadata`,
> `ClientSecretExpiresOnUtc`, `CertificateWarningDays`. Health checks: decided not needed. Detail: reference §7.4.
>
> ### D7. Saving Configure without re-entering a stored secret failed — FIXED 2026-10-20 (found in the item-4 test)
>
> The validator accepts an empty client secret or IdP certificate when `HasClientSecret` / `HasIdpCertificate` says one
> is stored — but the view never posted those flags, so they bound to false and every save of an OIDC or SAML store
> needed the secret typed again ("…is required"). Present before this review; earlier tests always posted a value.
> **Fix:** both flags are posted as hidden fields. Verified: SAML and OIDC saves without the secret keep the stored one.

- `IConfigurationModelFactory.ChangeSSOSope` (typo) → `SetStoreScopeAsync`; `GetCurrentCustomer` → `…Async`, or
  remove (callers have `IWorkContext`).
- `"SSOStoreScopeConfigurationAttribute"` literal used twice → constant in `SSOAuthenticationDefaults`.
- The plugin's own store-scope component is a near copy of nop's (diff: route names, and the check-all box removed).
  Keep it — it exists because nop's scope is shared with every settings page — but say so in its doc comment.
- `SSOAuthenticationMethod.cs` is ~500 lines, ~260 of them locale strings. Move the dictionary to
  `Infrastructure/SsoLocaleResources.cs`; the plugin class shrinks to its lifecycle.
- Milestone tags (`M4`, `M6.c`, `M13`) in comments refer to the deprecated plan; replace with plain explanations as
  files are touched.
- `NopStartup` registers everything in one long method — group by feature with short comments, or extension methods
  per feature (`services.AddSsoSaml()` …) for readability.
- `MockSso/Login.cshtml` has an inline script too (R7 applies, low priority — DEBUG only).

---

## 8. Suggested order

| Step | Items | Size |
|---|---|---|
| 1 | D1 view fix, D2 `SignedIn` type, D3 overage guard | small — before any IdP test |
| 2 | First real IdP test (Keycloak or an Entra dev tenant); settle C1 and C2 from what it shows | test |
| 3 | R1 metadata reader, R2 certificate helper, R3 summary query, R4 audit API, R5 names consumer | medium, mostly deletions |
| 4 | R6 controller split + permission on scope action, R7 scripts out of views, R8 settings types | larger — one at a time |
| 5 | R9 secrets layer; F1–F5 (id_token_hint, app roles, audit retention, ACS cap, OIDC login row) | done 2026-10-19 |
| 6 | P3 style items as files are touched | ongoing |
