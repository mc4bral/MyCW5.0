# SSO Plugin — Session handoff

For the next Claude Code session picking up this work.

---

## 1. Where you are

- **Plugin:** `Nop.Plugin.ExternalAuth.SSO` (was `.Microsoft`, renamed mid-session)
- **Path:** `...\Plugins\Nop.Plugin.ExternalAuth.SSO`
- **Stack:** nopCommerce 4.90, .NET 9, SAML via `ITfoxtec.Identity.Saml2` (4.10.x pin), OIDC via `Microsoft.AspNetCore.Authentication.OpenIdConnect` (8.0.x)
- **Repo entry point doc:** `...\AGENTS.md` — read it first
- **Plugin design doc:** `SSOPlan.v2.md` (this folder) — the authoritative spec

---

## 2. Working agreement carry-over (from AGENTS.md)

- **Never modify nop core.** All customisations stay in this plugin folder. Do not edit anything under `Libraries/Nop.*`, `Presentation/Nop.*`, `Tests/Nop.Tests`, or any OOTB `Nop.Plugin.*`.
- **Never mark tasks complete or commit without explicit human approval.** Verify-and-present tasks stay `in_progress` until the user signs off.
- **Tier 2 work needs a plan + clarifying questions upfront** (multi-file or architectural changes).
- **Stay in scope.** No drive-by refactors. Surface concerns separately.

---

## 3. CRITICAL — disk-write hazard

This session hit a recurring file-truncation bug ~12 times. **Read this before any large file write.**

**Symptoms:**
- `Edit` / `Write` tool reports success
- File on disk is truncated mid-statement, sometimes padded with NUL bytes (`0x00`) at the end
- `Read` tool may show the intended (cached/buffered) content even though disk is broken
- IDE compile errors point at apparently-complete files

**Detection (run after every non-trivial write):**
```bash
F=/path/to/file
wc -lc "$F"
echo -n 'NULs: '; tr -cd '\000' < "$F" | wc -c
echo -n 'braces: '; printf '%s/%s\n' "$(grep -c '{' $F)" "$(grep -c '}' $F)"
tail -c 8 "$F" | xxd
```

NULs > 0, brace mismatch, or a tail that doesn't end with `}\n` / `</script>\n` / `</form>\n` — file is broken.

**Recovery:**
1. Strip NULs: `tr -d '\000' < f > f.tmp && mv f.tmp f`
2. Inspect disk tail with `tail -c 60 f` to see where it cut off
3. Append the missing tail via `cat >> f << 'EOFILE' ... EOFILE`
4. Re-verify

**Mitigation strategies (in order of preference):**
1. **Multiple small `Edit` calls** (~5-20 lines each) instead of one large block
2. **`sed -i` for line-level surgical changes** (e.g., the 39-line override-checkbox strip worked great)
3. **`cat >> file << 'EOF'` to append** (heredoc) — survives the gremlin most of the time
4. **Avoid `Write` for files >300 lines** — that's the danger zone

---

## 4. What was delivered this session

13 distinct features across the plugin. Final state of each is described in `SSOPlan.v2.md` — this is the concise inventory.

| # | Feature | Files of note |
|---|---|---|
| 1 | **Protocol-aware card visibility** — SAML cards hide under OIDC, OIDC card hides under SAML, mixed Bindings card hides only SAML rows | `Views/Configure.cshtml` (inline `<script>` at bottom) |
| 2 | **Mock IdP mode** (DEBUG-only) with 4 test scenarios — controller, view, settings flag, redirect from real `Login`, Configure card. Compile-time gated by `BuildInfo.IsDebugBuild` + runtime `EnableMockMode` per-store | `Controllers/MockSsoController.cs`, `Views/MockSso/Login.cshtml`, `Constants/BuildInfo.cs`, `Constants/MockSsoDefaults.cs`, `Constants/SsoMockScenarios.cs`, `Models/MockLoginModel.cs`, `Controllers/ExternalAuthenticationController.cs` (`Login` action) |
| 3 | **OIDC "Send to IdP team" card** — Redirect URI + Post-logout URI rows alongside the SAML SP-metadata URL row, toggled by protocol | `Views/Configure.cshtml` |
| 4 | **Multi-store config UX** — standard nop `StoreScopeConfigurationViewComponent`, NO per-field `_OverrideForStore` checkboxes (override is implicit when a store is selected). Per-store cards render only when scope>0. System-wide cards (Login Bypass, Audit Logging) render only when scope=0 (All Stores). Upper-right Save button. | `Models/ConfigurationModel.cs`, `Factories/ConfigurationModelFactory.cs`, `Views/Configure.cshtml`, `Controllers/ExternalAuthSSOController.cs` |
| 5 | **SSO Accounts grid Delete button** — red button in Actions column; AJAX POST to `DeleteSsoAccount`; reloads DataTable on success | `Views/SsoAccounts.cshtml`, `Controllers/ExternalAuthSSOController.cs` |
| 6 | **Delete writes defaults back** to block storeId=0 fallback (nop's `loadSharedValueIfNotFound: true` would otherwise inherit install-time defaults or legacy "All stores" values) | `Factories/ConfigurationModelFactory.cs` (`DeleteSettingsForStoreAsync`) |
| 7 | **Dropdown placeholders** — `*` replaced with localised "All stores" / "All event types" / "All protocols" in audit log + group-role-mapping filter dropdowns | `Factories/SsoAuthEventModelFactory.cs`, `Factories/SsoGroupRoleMappingModelFactory.cs` |
| 8 | **Plugin rename** `ExternalAuth.Microsoft → ExternalAuth.SSO` — namespaces, folders, locale keys, plugin.json, csproj, view paths, OIDC scheme, ViewLocationExpander all updated. Single stale `MicrosoftAuthenticationDefaults` reference fixed in `Configure.cshtml` |  — |
| 9 | **OIDC validation crash fix** — placeholder `Authority` + `ClientId` so unconfigured OIDC scheme doesn't crash every request | `Infrastructure/OidcAuthenticationRegistrar.cs` |
| 10 | **Goodstart auto-seed** — `AddSsoAccount(POST)` detects `store.Name == "Goodstart"` and seeds SAML 2.0 config inline from `SAMLSSOConfig_StoreID_110.json` (cert encrypted via `_secretsService.Protect`). All 12 required form fields populated with sensible test values. | `Controllers/ExternalAuthSSOController.cs` (inline in `AddSsoAccount(POST)`) |
| 11 | **Group → Role Mappings page intro banner** — Info card explaining the feature's purpose, owned-role-set behaviour, privileged-role guard, per-store scoping, Group ID convention | `Views/GroupRoleMappings.cshtml` |
| 12 | **Configure page Save button moved to upper-right** content-header per `AGENTS.md §9.4` | `Views/Configure.cshtml` |
| 13 | **`URL → admin-store-scope` attribute sync** (later refactored by user into a factory method `ChangeSSOSope`) — fixed the SSO-Accounts-Edit-button-opens-wrong-store bug | `Factories/ConfigurationModelFactory.cs`, `Controllers/ExternalAuthSSOController.cs` |

---

## 5. Pending approvals (handoff to user)

These tasks stayed `in_progress` per the project rule "never mark complete without explicit human approval":

- **#3, #11, #14, #37, #42, #47, #53, #55, #56, #59, #60, #61, #62, #63**

Each is a "verify and present" task for the feature batch above it. The work is done; the user just hasn't signed off.

If user approves, mark them `completed` via `TaskUpdate`. Otherwise, leave them.

---

## 6. Known caveats / follow-ups

1. **OIDC options cached at startup** — `OidcAuthenticationRegistrar.Configure` resolves `SsoConfigurationSettings` once. Changes to per-store OIDC settings require an app restart to take effect. Documented in the registrar's docstring. Full per-store dynamic options via `IPostConfigureOptions<OpenIdConnectOptions>` is deferred (`SSOPlan.v2.md §3.5`).

2. **`MicrosoftAuthenticationMethod.cs` was renamed** to `SSOAuthenticationMethod.cs` at the plugin root. If you see references to the old name in obj/bin folders, ignore — they're build cache and regenerate.

3. **SAML cert in Goodstart seed is a placeholder string** (`"CERTIFICATEKEYHEREGTxD+JqsjvOsvEND"`) from the test JSON file. SP PFX placeholder too. QA admin must replace before testing against live IdP. The values exist only to satisfy `[Required]` form validators.

4. **Locale resources after the rename** — the keys are now `Plugins.ExternalAuth.SSO.*` (was `.Microsoft.*`). If a DB has an existing plugin install, the old rows in `LocaleStringResource` are orphan; clean with:
   ```sql
   DELETE FROM LocaleStringResource WHERE ResourceName LIKE 'plugins.externalauth.microsoft.%';
   ```

5. **Plugin SystemName changed** (`ExternalAuth.Microsoft → ExternalAuth.SSO`) — nop treats this as a new plugin. The old plugin record stays orphaned. Reinstall under the new SystemName via Admin → Plugins.

6. **No tests yet** — `Tests/Nop.Tests` is off-limits (Cardinal Rule). `Note A` in `AGENTS.md` flags this as an unresolved decision: parallel `Tests/CW.Tests` project vs no tests for TIG plugins. Pending.

7. **Mock IdP UI** — anonymous-accessible by design (per spec). DEBUG-build-only as the primary safety gate. Both `EnableMockMode` per-store flag AND `#if DEBUG` must be true.

8. **Disk-write gremlin recurrence** — if it keeps happening, consider:
   - Splitting `MicrosoftAuthenticationMethod.cs` / `SSOAuthenticationMethod.cs` (currently ~480 lines) into separate locale-resource files
   - Splitting `Views/Configure.cshtml` (currently ~1000 lines) into partials
   - These would reduce the "danger zone" for the truncation bug

---

## 7. File map (most touched)

```
Plugins/Nop.Plugin.ExternalAuth.SSO/
├── SSOAuthenticationMethod.cs              ← plugin entry, locale seeding, install/uninstall
├── SSOPlan.v2.md                           ← authoritative design doc (READ THIS)
├── SSO_SESSION_HANDOFF.md                  ← this file
├── SAMLSSOConfig_StoreID_110.json          ← test seed file for Goodstart
├── plugin.json                             ← SystemName: ExternalAuth.SSO
├── Constants/
│   ├── BuildInfo.cs                        ← IsDebugBuild compile-time const
│   ├── MockSsoDefaults.cs                  ← #if DEBUG; routing paths
│   ├── SsoMockScenarios.cs                 ← #if DEBUG; scenario consts
│   ├── SSOAuthenticationDefaults.cs        ← renamed from MicrosoftAuthenticationDefaults
│   └── (others — event types, customer attrs, failure reasons)
├── Controllers/
│   ├── ExternalAuthenticationController.cs ← public Login/Callback + mock redirect (#if DEBUG)
│   ├── ExternalAuthSSOController.cs        ← admin actions (Configure, SsoAccounts, AddSsoAccount, DeleteSsoAccount, GroupRoleMapping*, AuditLog*) + Goodstart auto-seed
│   ├── MockSsoController.cs                ← #if DEBUG; mock IdP endpoints
│   └── SamlController.cs                   ← /saml/acs, /saml/metadata, /saml/logout
├── Factories/
│   ├── ConfigurationModelFactory.cs        ← prepare/save/delete settings; SeedSettingsForStoreAsync
│   ├── IConfigurationModelFactory.cs       ← interface
│   ├── SsoAuthEventModelFactory.cs         ← audit log grid + dropdowns
│   ├── SsoGroupRoleMappingModelFactory.cs  ← mappings grid + dropdowns
│   └── SsoStoreSummaryModelFactory.cs      ← SSO accounts grid
├── Infrastructure/
│   ├── NopStartup.cs                       ← DI registration, filter registration, MVC options
│   ├── OidcAuthenticationRegistrar.cs      ← OIDC scheme registration (placeholder fallback for unconfigured)
│   └── ViewLocationExpander.cs             ← PLUGIN_FOLDER = "ExternalAuth.SSO"
├── Models/
│   ├── ConfigurationModel.cs               ← NO _OverrideForStore flags (implicit override)
│   ├── MockLoginModel.cs                   ← #if DEBUG
│   └── (others)
├── Settings/
│   ├── SsoConfigurationSettings.cs         ← per-store settings (Protocol, IsEnabled, BaseUrl, Idp*, Sp*, claim mapping, EnableMockMode, etc.)
│   ├── SsoSystemSettings.cs                ← system-wide (bypass token, allowed roles, audit logging)
│   └── SsoProtocol.cs                      ← enum: Off / OpenIdConnect / Saml2
├── Views/
│   ├── Configure.cshtml                    ← BIG (~1000 lines); protocol-aware visibility JS; per-store + system-wide conditional cards; Save button top-right
│   ├── SsoAccounts.cshtml                  ← grid with Test/Edit/Delete actions
│   ├── GroupRoleMappings.cshtml            ← grid + intro banner
│   ├── AddSsoAccount.cshtml                ← store picker for "Add new"
│   ├── MockSso/Login.cshtml                ← mock IdP UI (standalone layout)
│   └── (others — AuditLog, CreateOrUpdateGroupRoleMapping, etc.)
└── Services/
    ├── SsoUserProvisioningService.cs       ← claim → role provisioning chokepoint
    ├── SsoSecretsService.cs                ← IDataProtector wrapper for ciphertext fields
    └── (many others — events, rate limit, onboarding report, test connection, group→role mapping)
```

---

## 8. How to resume

1. Read `AGENTS.md` (one level up) for the working agreement
2. Read `SSOPlan.v2.md` (same folder) for the authoritative design
3. Read this doc for what's been done and what's pending
4. Run a build to confirm everything compiles (sandbox here has no `dotnet`; resume sessions on a machine that does)
5. If touching files, **start with small Edits and verify disk state immediately** (see §3 above)

---

*Session ended May 30, 2026. Open approvals listed in §5 are with the user.*
