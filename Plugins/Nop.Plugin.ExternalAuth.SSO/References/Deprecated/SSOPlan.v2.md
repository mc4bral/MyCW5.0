> **Project:** CW_Nop_4_9 — nopCommerce 4.90 on .NET 9
> **Plugin (new):** `Nop.Plugin.ExternalAuth.SSO`
> **Plugin (folder being replaced):** `Plugins/Nop.Plugin.ExternalAuth.SSO/`
> **Group:** `ExternalAuth methods` · **System name:** `ExternalAuth.SSO`
> **Author:** Total Image Group
> **Status:** **Planned — design spec, not yet implemented**
> **Supersedes:** `SSOPlan.md` (the legacy as-built for nopCommerce 4.10 / netcoreapp2.1)
> **Last updated:** May 2026
>
> This document is the **forward-looking design** for the new SSO plugin that
> will replace the legacy `Nop.Plugin.ExternalAuth.SSO` (originally a port of MiniOrange
> SAML SSO). §1 carries the problem statement forward; §2 records the
> architectural decisions made jointly with the engineering owner; §3 specifies
> the planned design; §4–§6 describe the protocol flows; §7 the security
> posture; §8 the items deferred from this phase; §9 the open product
> questions; §10 the implementation milestones.

---

## 1. Background

The legacy `Nop.Plugin.ExternalAuth.SSO` plugin shipped as a SAML-only authentication
adapter against Microsoft Entra ID (Azure AD), originally replacing the
MiniOrange SAML SSO plugin on nopCommerce 4.10 / netcoreapp2.1. It worked, but
two classes of constraint shaped it in ways that no longer apply on
nopCommerce 4.90 / .NET 9:

- **No modern SAML library was available on netcoreapp2.1.** Result: ~1,000
  lines of hand-rolled SAML 2.0 (`SignedXml`, XXE-prohibiting XML readers,
  hand-built replay cache, manual signature/digest allowlists, manual
  EncryptedAssertion decryption). Every line in that file was security-critical
  and most of the open P1 punch list (P1-1 unsigned AuthnRequest, P1-2
  EncryptedAssertion unwired, P1-7 InResponseTo recovery, P1-16 cert-decrypt
  fallback) sits on this codebase.
- **An older data-layer style.** The legacy used a custom `SSOObjectContext`
  (`IDbContext`) and EF model mappings rather than the `IRepository<T>` +
  FluentMigrator approach that nopCommerce 4.90 mandates. This kept the plugin
  off the standard nop data plumbing.

Plus a list of residual issues carried over from the legacy plan's §4 (open P0
through P2 items) and §3.7 (the local-login bypass had to be bolted on after
the fact to recover when Entra is unavailable).

**Goal of v2:** rebuild as a generic Microsoft Entra ID SSO plugin on
nopCommerce 4.90's standard plugin architecture, with OIDC as the primary
protocol and SAML 2.0 as a secondary option for legacy IdP compatibility.
Eliminate the hand-rolled SAML code, plug into the standard external-auth
pipeline, and clear the residual punch list from `SSOPlan.md` §4.

> **Ops action carried over from the legacy plan** (independent of this
> implementation): the IdP certificate and Azure Tenant ID committed to the
> public Git repository in `SAMLSSOConfig_StoreID_110 (1).json` must be
> rotated and the file purged from Git history. The file will be deleted as
> part of the rename to `Nop.Plugin.ExternalAuth.SSO`, but its presence
> in Git history remains a separate ops concern.

---

## 2. Decisions and requirements

| # | Topic | Decision |
|---|---|---|
| 1 | Primary protocol | **OpenID Connect** (`Microsoft.AspNetCore.Authentication.OpenIdConnect`) |
| 2 | Secondary protocol | **SAML 2.0** via **ITfoxtec.Identity.Saml2** (BSD-3 licensed, .NET 9 compatible, controller-style flow) |
| 3 | Per-store protocol selection | One protocol active per store, picked via admin dropdown (`Protocol = OIDC \| SAML \| Off`) |
| 4 | Multi-store | Per-store configuration via `ISettings` + `SaveSettingOverridablePerStoreAsync`, using nop's standard `StoreScopeConfigurationViewComponent` picker (`?storeid=N`). **No per-field `_OverrideForStore` checkboxes** — when a specific store is selected the override is implicit (`overrideForStore: true` for every per-store field). When the picker is on **"All stores"** (scope = 0) the Configure view hides every per-store card and the factory only saves `SsoSystemSettings`; per-store fields are not touched. When scope > 0 the per-store cards render and the factory saves them for that store plus the system-wide settings. The Save button lives in the upper-right content-header (AGENTS.md §9.4). |
| 5 | Plugin name | `Nop.Plugin.ExternalAuth.SSO` (`SystemName: "ExternalAuth.SSO"`, `Group: "ExternalAuth methods"`) |
| 6 | CW.Core dependency | None — this is a generic plugin, reusable outside CW |
| 7 | Data access | `IRepository<T>` only — no `DbContext`, no EF, no LinqToDB direct |
| 8 | Migrations | **FluentMigrator** with `[NopMigration]` + `MigrationVersions.cs` |
| 9 | Validation | **FluentValidation** for admin models |
| 10 | Object mapping | **AutoMapper** via `IOrderedMapperProfile` |
| 11 | DI | Single `INopStartup`, constructor injection only (no `EngineContext.Current.Resolve` in new code) |
| 12 | External auth | Implement `IExternalAuthenticationMethod` + `IExternalAuthenticationRegistrar` — pattern after `Nop.Plugin.ExternalAuth.Facebook` |
| 13 | Filters | Keep `LoginActionsFilter` and `LocalLoginEnforcementFilter` (constructor-injected; no `EngineContext.Current`) |
| 14 | Login button | Public view component returns empty when SSO off for the current store; otherwise shows a single "Sign in with Microsoft" button — the protocol is opaque to the customer |
| 15 | Conflict resolution | Auto-link by verified email match; existing admin accounts hard-blocked from SSO link |
| 16 | Auto-registration | Configurable per store; default role + group→role mapping |
| 17 | Privileged-role protection | `Administrators` are hard-blocked from any SSO-driven role assignment |
| 18 | Group → role mapping | Dedicated entity `SSO_GroupRoleMapping`, keyed by Entra Object ID (GUID), per-store, with a DataTables admin grid |
| 19 | Refresh cadence | Group→role mapping re-applied on every successful SSO login, scoped to the plugin's "owned" role set only |
| 20 | Single Log-Out | Native support in both protocols — OIDC via `end_session_endpoint`, SAML via ITfoxtec's `Saml2RedirectBinding` SLO flow |
| 21 | Local-login bypass | System-wide `ssobypass=<token>` query string lets ops reach the local form when SSO is enabled; gated by `LocalLoginEnforcementFilter` checking the resulting customer is in a configured admin role set |
| 22 | Sensitive field encryption | `IEncryptionService` (nop convention) layered with ASP.NET Core Data Protection for key rotation; optional Azure Key Vault reference for the SAML SP PFX private key in production |
| 23 | Audit log | Single entity `SSOAuthEvent` via `IRepository<T>`, indexed on `(OccurredOnUtc)` and `(StoreId, OccurredOnUtc)`, retention via scheduled bulk delete |
| 24 | Mock IdP | **Reinstated as a `#if DEBUG`-only dev aid.** `MockSsoController` + `Views/MockSso/Login.cshtml` render an in-plugin mock IdP page. Gated by `SsoConfigurationSettings.EnableMockMode` (per-store) and the `DEBUG` compile symbol. Both SAML 2.0 and OIDC are mocked — the mock builds a `ClaimsPrincipal` from form fields and dispatches to `ISsoUserProvisioningService.HandleAsync`, so the real cryptographic paths (`SamlResponseProcessor`, OIDC handler) are not relaxed or touched. Scenarios: `success-existing`, `success-new`, `success-with-groups`, `failure`. A long-term integration-test harness is still the right destination — this mock is a dev convenience until that lands. |
| 25 | csproj shape | Mirror `Nop.Plugin.ExternalAuth.Facebook.csproj`: `ProjectReference` to `Nop.Web.Framework` only, `CopyLocalLockFileAssemblies=true`, `Build/ClearPluginAssemblies.proj` MSBuild target for dedup |
| 26 | Cert expiry, health checks, OpenTelemetry, automated tests | **Deferred** — see §8 |

---

## 3. Architecture

### 3.1 Project layout

    Plugins/Nop.Plugin.ExternalAuth.SSO/
    ├── Nop.Plugin.ExternalAuth.SSO.csproj
    ├── plugin.json                              -- Group: "ExternalAuth methods", SystemName: "ExternalAuth.SSO"
    ├── logo.png
    ├── MicrosoftAuthenticationMethod.cs         -- BasePlugin + IExternalAuthenticationMethod
    │
    ├── Infrastructure/
    │   ├── NopStartup.cs                        -- INopStartup: DI registration, MVC filter registration, options binding
    │   ├── OidcAuthenticationRegistrar.cs       -- IExternalAuthenticationRegistrar for the OIDC scheme
    │   ├── RouteProvider.cs                     -- /signin-microsoft (OIDC callback), /saml/* (SAML endpoints)
    │   └── PluginEventConsumer.cs               -- IConsumer<AdminMenuCreatedEvent> (admin menu hookup)
    │
    ├── Domain/
    │   ├── SsoAuthEvent.cs                      -- BaseEntity, audit row
    │   └── SsoGroupRoleMapping.cs               -- BaseEntity, group→role mapping row
    │
    ├── Data/
    │   ├── Mapping/
    │   │   ├── SsoAuthEventBuilder.cs           -- NopEntityBuilder<SsoAuthEvent>
    │   │   └── SsoGroupRoleMappingBuilder.cs    -- NopEntityBuilder<SsoGroupRoleMapping>
    │   ├── Migrations/
    │   │   └── SchemaMigration.cs               -- [NopMigration(MigrationVersions.InitialSchema, ...)]
    │   ├── MigrationVersions.cs
    │   └── ExternalAuthSSONameCompatibility.cs  -- INameCompatibility (SSO_AuthEvent, SSO_GroupRoleMapping)
    │
    ├── Settings/
    │   ├── SsoConfigurationSettings.cs          -- ISettings, per-store
    │   ├── SsoSystemSettings.cs                 -- ISettings, system-wide (bypass token + LocalLoginAllowedRoles)
    │   └── SsoProtocol.cs                       -- enum: Off, OpenIdConnect, Saml2
    │
    ├── Services/
    │   ├── ISsoConfigurationService.cs / SsoConfigurationService.cs
    │   │                                        -- resolves effective per-store settings, decrypts sensitive fields
    │   ├── ISsoEventService.cs / SsoEventService.cs
    │   │                                        -- audit log writer + reader (Paged), bulk purge
    │   ├── ISsoGroupRoleMappingService.cs / SsoGroupRoleMappingService.cs
    │   │                                        -- CRUD + "resolve roles for groups" + privileged-role guard
    │   ├── ISsoUserProvisioningService.cs / SsoUserProvisioningService.cs
    │   │                                        -- claims → ExternalAuthenticationParameters; auto-register/link;
    │   │                                            role sync within owned set; admin-account block
    │   ├── Protocols/
    │   │   ├── IOidcChallengeBuilder.cs / OidcChallengeBuilder.cs
    │   │   ├── ISamlChallengeBuilder.cs / SamlChallengeBuilder.cs       -- wraps ITfoxtec.Saml2.Saml2AuthnRequest
    │   │   └── ISamlResponseProcessor.cs / SamlResponseProcessor.cs    -- wraps ITfoxtec.Saml2.Saml2AuthnResponse
    │   ├── Metadata/
    │   │   ├── IIdpMetadataImporter.cs / IdpMetadataImporter.cs        -- XML file upload + URL fetch + auto-refresh
    │   │   └── IIdpMetadataCache.cs / IdpMetadataCache.cs              -- IStaticCacheManager backed
    │   └── ISsoSecretsService.cs / SsoSecretsService.cs                -- IEncryptionService + IDataProtector wrap
    │
    ├── Controllers/
    │   ├── Public/
    │   │   ├── ExternalAuthenticationController.cs  -- /external-auth/microsoft/challenge → either OIDC challenge
    │   │   │                                            (handled by framework) or SAML AuthnRequest build+redirect
    │   │   └── SamlController.cs                    -- /saml/acs (POST), /saml/logout (GET+POST), /saml/metadata (GET)
    │   └── Admin/
    │       ├── ExternalAuthSSOController.cs   -- /Admin/ExternalAuthSSO/Configure,
    │       │                                            /Admin/ExternalAuthSSO/AuditLog,
    │       │                                            /Admin/ExternalAuthSSO/GroupRoleMappings,
    │       │                                            /Admin/ExternalAuthSSO/TestConnection,
    │       │                                            /Admin/ExternalAuthSSO/ImportIdpMetadata
    │       └── Factories/
    │           ├── IConfigurationModelFactory.cs / ConfigurationModelFactory.cs
    │           └── IGroupRoleMappingModelFactory.cs / GroupRoleMappingModelFactory.cs
    │
    ├── Filters/
    │   ├── LoginActionsFilter.cs                -- front-door: GET /login → /external-auth/microsoft/challenge when
    │   │                                            SSO on for the current store, unless ?ssobypass=<token> matches
    │   └── LocalLoginEnforcementFilter.cs       -- back-door: after Customer/Login POST, verify the resulting
    │                                                customer is in SsoSystemSettings.LocalLoginAllowedRoles
    │
    ├── Components/
    │   └── SSOAuthenticationViewComponent.cs  -- "Sign in with Microsoft" button on the public login page
    │
    ├── Security/
    │   └── ExternalAuthSSOPermissionProvider.cs   -- IPermissionProvider: AccessSsoPlugin + AccessSsoSettings
    │
    ├── Mapping/
    │   └── MapperConfiguration.cs               -- IOrderedMapperProfile entries
    │
    ├── Validators/
    │   ├── ConfigurationModelValidator.cs       -- FluentValidation
    │   └── GroupRoleMappingModelValidator.cs
    │
    ├── Models/
    │   └── (public — minimal; the login button has no form fields)
    │
    ├── Areas/Admin/Models/
    │   ├── ConfigurationModel.cs                -- record : BaseNopModel
    │   ├── SsoAuthEventModel.cs / SsoAuthEventListModel.cs / SsoAuthEventSearchModel.cs
    │   ├── GroupRoleMappingModel.cs / GroupRoleMappingListModel.cs / GroupRoleMappingSearchModel.cs
    │   └── ImportIdpMetadataModel.cs
    │
    └── Views/
        ├── _ViewImports.cshtml
        ├── _ViewStart.cshtml
        ├── PublicInfo.cshtml                    -- the login button partial
        └── Admin/ExternalAuthSSO/
            ├── Configure.cshtml
            ├── _CreateOrUpdateGroupRoleMapping.cshtml
            ├── GroupRoleMappings.cshtml
            └── AuditLog.cshtml

### 3.2 Data layer

Two entities are persisted by the plugin. Both follow the AGENTS.md §8
patterns: `BaseEntity`, `NopEntityBuilder<T>`, FluentMigrator `[NopMigration]`,
and `INameCompatibility` for the `SSO_*` table name.

**`SsoAuthEvent`** — append-only audit row (carried forward from the legacy
`SSOAuthEvent`, schema unchanged except for indexes):

| Column | Type | Notes |
|---|---|---|
| Id | int PK | identity |
| StoreId | int | which store processed the event |
| CustomerId | int? | nop `Customer.Id`; null when no customer resolved |
| CustomerEmail | nvarchar(200) | from the assertion/token |
| EventType | nvarchar(50) | `Login`, `AutoRegistered`, `Linked`, `Slo`, `Failed`, `BypassDenied` |
| Protocol | nvarchar(20) | `Oidc` or `Saml2` |
| IdpSubject | nvarchar(500) | OIDC `sub` or SAML `NameID` |
| OccurredOnUtc | datetime | UTC |
| IpAddress | nvarchar(50) | client IP at the time |
| Notes | nvarchar(max) | error detail or contextual info |

Indexes: `(OccurredOnUtc)`, `(StoreId, OccurredOnUtc)`. Bulk-purge via
`IRepository.DeleteAsync(predicate)`.

Table name (via `INameCompatibility`): `SSO_AuthEvent`.

**`SsoGroupRoleMapping`** — per-store entry that maps an Entra group to a nop
customer role (P1-14 fix):

| Column | Type | Notes |
|---|---|---|
| Id | int PK | identity |
| StoreId | int | which store this mapping applies to |
| IdpGroupId | nvarchar(100) | Entra Object ID (GUID) — survives group renames |
| IdpGroupDisplayName | nvarchar(200) | shown in the admin grid, not used for matching |
| CustomerRoleId | int | FK to `CustomerRole` |
| DisplayOrder | int | tie-breaker when multiple groups map to overlapping roles |
| IsActive | bit | allows soft-disable without delete |

Indexes: `(StoreId, IdpGroupId)`. The service-layer "resolve roles for groups"
method filters out `Administrators` regardless of
mapping; that guard is not bypassable from the data layer.

Table name (via `INameCompatibility`): `SSO_GroupRoleMapping`.

`MigrationVersions.cs`:

    public static class MigrationVersions
    {
        public const string InitialSchema = "2026/06/01 00:00:00";
    }

The legacy `SSOConfigurationDefaults.cs` (with the Goodstart tenant ID baked
in) is dropped; per-store config lives entirely in the `Setting` table via
`ISettings`. There are **no** core-data seed values from this plugin.

### 3.3 Settings & secrets

Two `ISettings` classes:

**`SsoConfigurationSettings`** (per-store, via `SaveSettingOverridablePerStoreAsync`):

- **General** — `Protocol` (enum: `Off` / `OpenIdConnect` / `Saml2`), `IsEnabled`, `BaseUrl`
- **OIDC** — `Authority` (e.g. `https://login.microsoftonline.com/<tenant>/v2.0`), `ClientId`, `ClientSecret` (encrypted), `CallbackPath` (default `/signin-microsoft`), `Scopes` (default `openid profile email`)
- **SAML** — `IdpEntityId`, `IdpSsoUrl`, `IdpSloUrl`, `IdpCertificateBase64` (encrypted), `SpEntityId`, `AcsUrl`, `SpLogoutUrl`, `SignedRequest`, `EncryptedAssertion`, `SpPfxBase64` (encrypted), `SpPfxPassword` (encrypted), `LoginBinding`, `LogoutBinding`, `IdpMetadataUrl` (optional, enables auto-refresh)
- **Claim mapping** — `EmailClaimType`, `UsernameClaimType`, `FirstNameClaimType`, `LastNameClaimType`, `GroupsClaimType` (default OIDC `groups`, SAML group attribute)
- **User provisioning** — `AutoRegister`, `DefaultRoleSystemName`, `AllowedEmailDomains` (CSV, supports `*.example.com` wildcards), `RefreshRolesOnLogin` (default true)
- **Redirects** — `PostLoginRedirectUrl`, `PostLogoutRedirectUrl` (open-redirect-guarded via `IUrlHelper.IsLocalUrl`)

**`SsoSystemSettings`** (system-wide, `storeId = 0`):

- `SsoBypassToken` — encrypted; empty disables the bypass
- `LocalLoginAllowedRoles` — CSV of role `SystemName`s permitted to complete a
  local login that arrived through the bypass URL
  (default `Administrators)

**Encryption layering.** All `(encrypted)` fields are wrapped by
`SsoSecretsService`: `IEncryptionService` (nop convention, primary at-rest
encryption) layered with `IDataProtector` (purpose-string keyed, supports
ASP.NET Core key rotation). For production, the SP PFX may instead be sourced
from Azure Key Vault — the setting then stores the Key Vault URI, and
`SsoSecretsService.LoadSpPfxAsync()` resolves it. Key Vault wiring is
configured per-environment in `appsettings.{env}.json`, not in the plugin.

### 3.4 DI / startup

A single `INopStartup` registers everything:

    public class NopStartup : INopStartup
    {
        public int Order => 3000; // after nop's standard startup

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<ISsoConfigurationService, SsoConfigurationService>();
            services.AddScoped<ISsoEventService, SsoEventService>();
            services.AddScoped<ISsoGroupRoleMappingService, SsoGroupRoleMappingService>();
            services.AddScoped<ISsoUserProvisioningService, SsoUserProvisioningService>();
            services.AddScoped<IIdpMetadataImporter, IdpMetadataImporter>();
            services.AddScoped<IIdpMetadataCache, IdpMetadataCache>();
            services.AddScoped<ISsoSecretsService, SsoSecretsService>();
            services.AddScoped<IOidcChallengeBuilder, OidcChallengeBuilder>();
            services.AddScoped<ISamlChallengeBuilder, SamlChallengeBuilder>();
            services.AddScoped<ISamlResponseProcessor, SamlResponseProcessor>();

            services.AddScoped<ConfigurationModelFactory>();
            services.AddScoped<GroupRoleMappingModelFactory>();

            // global MVC filters
            services.Configure<MvcOptions>(o =>
            {
                o.Filters.Add<LoginActionsFilter>();
                o.Filters.Add<LocalLoginEnforcementFilter>();
            });

            // view location for the plugin's Admin views
            services.Configure<RazorViewEngineOptions>(o =>
                o.ViewLocationExpanders.Add(new ViewLocationExpander()));
        }

        public void Configure(IApplicationBuilder application) { /* nothing here */ }
    }

The two MVC filters resolve their dependencies via **constructor injection**.
No `EngineContext.Current.Resolve<T>()` in new code. This is a deliberate
break from the legacy plugin's `LoginActionsFilter` / `LocalLoginEnforcementFilter`
which used the service locator pattern; it is required by AGENTS.md §4.

The OIDC handler is registered separately by `OidcAuthenticationRegistrar :
IExternalAuthenticationRegistrar` so it participates in the standard nop
external-auth pipeline alongside Facebook/Google.

### 3.5 Protocol layer

The plugin presents **one logical authentication method** ("Microsoft") to nop
and to the customer, but internally branches to one of two protocol
implementations based on the per-store `SsoConfigurationSettings.Protocol`
value. The branching point is `ExternalAuthenticationController.Challenge`
(see §3.6).

**OIDC (primary).** Standard ASP.NET Core `OpenIdConnectHandler` registered as
an authentication scheme `"ExternalAuthSSO-Oidc"` via
`IExternalAuthenticationRegistrar`. Per-store config is bound dynamically via
`IPostConfigureOptions<OpenIdConnectOptions>` keyed by the store ID resolved
from `IStoreContext.GetCurrentStoreAsync()`. The handler's
`OnTokenValidated` event hands control to `ISsoUserProvisioningService`,
which constructs `ExternalAuthenticationParameters` from the claims and
delegates to nop's `IExternalAuthenticationService.AuthenticateAsync(...)`.

Group claim handling: Entra issues groups by Object ID GUID. The provisioning
service resolves these GUIDs against `SsoGroupRoleMapping` for the store and
applies the resulting role set (privileged-role guard always in force).

OIDC SLO uses the standard `OpenIdConnectHandler.SignOutAsync` flow against
the discovered `end_session_endpoint`.

**SAML 2.0 (secondary).** ITfoxtec.Identity.Saml2, controller-style. The
challenge flow:

    SamlChallengeBuilder.BuildAsync(store):
      var config = LoadSaml2Configuration(store);   -- per-store, dynamic
      var request = new Saml2AuthnRequest(config) { ... };
      var binding = new Saml2RedirectBinding();
      binding.Bind(request);
      return binding.ToActionResult();              -- yields a 302 to IdP

ACS:

    SamlResponseProcessor.HandleAsync(httpRequest, store):
      var config = LoadSaml2Configuration(store);
      var binding = new Saml2PostBinding();
      var response = new Saml2AuthnResponse(config);
      binding.Unbind(httpRequest, response);
      response.Status -> Success expected
      var claims = response.ClaimsIdentity.Claims;
      var parameters = MapToExternalAuthParameters(claims);
      await _externalAuthenticationService.AuthenticateAsync(parameters);

SLO: `Saml2LogoutRequest` + `Saml2RedirectBinding` for SP-initiated;
`Saml2LogoutResponse` for IdP-initiated. The session-keys collision flagged in
the legacy plan as P2-7 (`SSO_NameId` / `SSO_SessionIndex` colliding on
multi-tab logins) is fixed by storing these as **generic attributes on the
`Customer`** via `IGenericAttributeService` instead of in `HttpContext.Session`.

InResponseTo correlation: the request ID is stored in a short-lived,
`SameSite=Lax`, `HttpOnly` correlation cookie (not in `Session`). Multi-tab
logins now work correctly. Cookie name: `.AspNetCore.ExternalAuthSSO.SamlCorrelation`.

ITfoxtec handles signature validation, EncryptedAssertion decryption, replay
protection, XXE-prohibited XML reading, and digest/signature method allowlist
enforcement. The legacy plan's P1-1, P1-2, P1-4, P1-7, P1-16 mitigations all
become library behaviour rather than plugin code.

### 3.6 External auth integration

The plugin implements `IExternalAuthenticationMethod` and pattern-matches
`Nop.Plugin.ExternalAuth.Facebook`:

- `MicrosoftAuthenticationMethod : BasePlugin, IExternalAuthenticationMethod`
- `OidcAuthenticationRegistrar : IExternalAuthenticationRegistrar` registers
  the OIDC scheme
- `SSOAuthenticationViewComponent` renders the login button on the
  public login page (empty when `Protocol = Off` for the current store)

The challenge flow:

1. Customer clicks "Sign in with Microsoft" on `/login`.
2. The view component's form posts to `/external-auth/microsoft/challenge`.
3. `ExternalAuthenticationController.Challenge`:
   - Resolves the current store and its `SsoConfigurationSettings`.
   - If `Protocol == Off`: redirect to `/login` (defensive; the button
     shouldn't have rendered).
   - If `Protocol == OpenIdConnect`: issue an ASP.NET Core challenge for
     the `"ExternalAuthSSO-Oidc"` scheme. The framework drives the
     redirect and the callback to `/signin-microsoft`.
   - If `Protocol == Saml2`: invoke `SamlChallengeBuilder.BuildAsync(store)`
     and return its `IActionResult` (a 302 to the IdP).

Both paths land back in user provisioning via a common code path
(`SsoUserProvisioningService`), so account-link / auto-register / role-sync
logic lives in one place regardless of protocol.

### 3.7 Filters

Two global MVC filters survive from the legacy plan. Their semantics are
unchanged; the implementation is rewritten for constructor injection and to
use a correlation cookie instead of `Session`.

**`LoginActionsFilter`** — front door. On `GET /login`:

1. If the current store's `Protocol == Off`, do nothing.
2. If the request has a `ssobypass=<token>` query param and the decrypted
   token matches `SsoSystemSettings.SsoBypassToken`: set a short-lived
   `.AspNetCore.ExternalAuthSSO.BypassUsed` cookie and let the request
   reach the standard `Customer/Login` view.
3. Otherwise: redirect to `/external-auth/microsoft/challenge` (which then
   branches to OIDC or SAML per §3.6).

**`LocalLoginEnforcementFilter`** — back door. On `POST` to `Customer/Login`
(matched by controller name string, not by type — drops the legacy plan's
requirement to reference `Nop.Web` from the plugin):

1. If the bypass cookie is not present, do nothing.
2. After the action runs and the user is authenticated, verify the resulting
   customer carries at least one role in `LocalLoginAllowedRoles`.
3. If yes: clear the bypass cookie and let the response proceed.
4. If no: sign the user out, write a `BypassDenied` audit row, queue a
   generic `Plugins.ExternalAuth.SSO.Login.AccessDenied` notification,
   and redirect back to `/login` (cookie kept so they land on the local form
   to retry).

Filter dependencies (`ISsoConfigurationService`, `ISsoSystemSettings` via
`ISettingService`, `ISsoEventService`, `IStoreContext`, `INotificationService`,
`IAuthenticationService`, `IWorkContext`, `IUrlHelperFactory`) are all
**injected through the constructor**.

### 3.8 Group → role mapping

Implemented by `ISsoGroupRoleMappingService` against `IRepository<SsoGroupRoleMapping>`.

API:

    Task<IList<int>> ResolveCustomerRoleIdsAsync(int storeId, IEnumerable<string> idpGroupIds);

The method:

1. Loads active mappings for `storeId` matching any of the supplied group IDs.
2. Returns the union of their `CustomerRoleId` values, **with `Administrators`
   filtered out unconditionally** (mirrors the
   legacy's `AssignDefaultRole` privileged-role guard).
3. Audit-logs any filtered-out attempts as a `Failed` event with `Notes`
   explaining which privileged role was blocked.

`SsoUserProvisioningService` calls this on every successful SSO login. Per the
decision in §2 row 19, the resulting role set **replaces** the customer's
membership across the plugin's "owned" set (= the distinct `CustomerRoleId`
values that appear in any mapping for the store, plus the configured
`DefaultRoleSystemName`). Roles outside the owned set are never touched.

Admin grid: `/Admin/ExternalAuthSSO/GroupRoleMappings` — DataTables, CRUD,
per-store filter, ordered by `DisplayOrder`. FluentValidation on the edit form
requires a non-empty `IdpGroupId` matching the Entra GUID format.

### 3.9 Audit log

Implemented by `ISsoEventService` against `IRepository<SsoAuthEvent>`.

Writes:

- `Login` — challenge initiated
- `AutoRegistered` — new customer created from the assertion
- `Linked` — existing customer linked to an IdP subject
- `Slo` — single log-out completed
- `Failed` — any error during the flow (invalid signature, blocked admin
  account, blocked email domain, replay attempt)
- `BypassDenied` — local-login bypass cookie set but the resulting customer
  was not in `LocalLoginAllowedRoles`

The write happens **after** the corresponding side effect succeeds (e.g.,
audit row written after `SignInAsync` returns, in a `try`/`catch` so a logging
failure does not break login). This carries forward the legacy plan's P1-10
fix.

Reads: `GetPagedAsync(SsoAuthEventSearchModel)` for the admin grid, with
filters on `EventType`, `Protocol`, `CustomerEmail`, `StoreId`, `OccurredFrom`,
`OccurredTo`.

Purge: `Task<int> PurgeOlderThanAsync(DateTime cutoffUtc)` — uses
`IRepository.DeleteAsync(e => e.OccurredOnUtc < cutoffUtc)` (the bulk
predicate-delete path that wasn't available in the legacy data layer). Wiring
this to a scheduled task is deferred (see §8).

### 3.10 Admin UI

Pages:

| Page | Route | Notes |
|---|---|---|
| Configure | `GET/POST /Admin/ExternalAuthSSO/Configure` | Full settings form, per-store via the standard nop store-scope dropdown. Sensitive fields encrypted on POST. |
| Import IdP metadata | `POST /Admin/ExternalAuthSSO/ImportIdpMetadata` | Accepts a federation XML file or a metadata URL; pre-fills `IdpEntityId`, `IdpSsoUrl`, `IdpSloUrl`, `IdpCertificateBase64` from the parsed metadata. |
| Group → role mappings | `GET /Admin/ExternalAuthSSO/GroupRoleMappings` (list), `GET/POST .../GroupRoleMappings/Edit/{id}` | DataTables grid; create/edit form. |
| Audit log | `GET /Admin/ExternalAuthSSO/AuditLog` | DataTables grid with `EventType` / `Protocol` / `Email` / `OccurredFrom` / `OccurredTo` filters. |
| Test connection | `POST /Admin/ExternalAuthSSO/TestConnection` | Async (fixes legacy P1-17). For OIDC: fetches the discovery document; for SAML: parses the IdP cert and HEADs the IdP URL with the SSRF host allowlist. |

Conventions (AGENTS.md §9):

- Controllers inherit `BasePluginController` (this plugin does not reference
  `CW.Core`).
- Each controller declares `[AuthorizeAdmin]`, `[Area(AreaNames.ADMIN)]`,
  `[AutoValidateAntiforgeryToken]` explicitly.
- Permissions via `[CheckPermission(...)]` attributes — not inline
  `_permissionService.AuthorizeAsync(...)`.
- Models are `record` types deriving `BaseNopEntityModel` / `BaseSearchModel`
  with `[NopResourceDisplayName(...)]`.
- Grids use `DataTablesModel` + `PrepareToGridAsync` — no KendoUI.
- Layout: `_ConfigurePlugin` for Configure; `_AdminLayout` for the others.
- Bootstrap 4.6 markup; nop tag helpers (`<nop-label>`, `<nop-editor>`,
  `<nop-select>`).

Admin menu: the plugin attaches its admin menu item via
`IConsumer<AdminMenuCreatedEvent>` against a published anchor (decided at
implementation time — likely under the nop "Configuration → Authentication
methods" branch since this plugin is not CW-namespaced).

### 3.11 Plugin lifecycle

`MicrosoftAuthenticationMethod.InstallAsync`:

1. Save default `SsoConfigurationSettings` with `Protocol = Off`,
   `IsEnabled = false`.
2. Save default `SsoSystemSettings` with an empty `SsoBypassToken` and
   `LocalLoginAllowedRoles = "Administrators"`.
3. Install permissions via `ExternalAuthSSOPermissionProvider`
   (default-granted only to `Administrators`).
4. Install locale resources under `Plugins.ExternalAuth.SSO.*`.
5. Call `base.InstallAsync()`.

`UninstallAsync`:

1. Delete settings (both classes).
2. Delete permissions.
3. Delete locale resources under `Plugins.ExternalAuth.SSO`.
4. Call `base.UninstallAsync()`.

FluentMigrator's reversible migration is responsible for table creation on
install and table drop on uninstall — driven by nop's migration runner, not
by the plugin's `InstallAsync` / `UninstallAsync`.

### 3.12 csproj shape

Mirroring `Nop.Plugin.ExternalAuth.Facebook.csproj`:

    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>net9.0</TargetFramework>
        <Copyright>Total Image Group</Copyright>
        <Company>Total Image Group</Company>
        <Authors>Total Image Group</Authors>
        <OutputPath>$(SolutionDir)\Presentation\Nop.Web\Plugins\ExternalAuth.SSO</OutputPath>
        <OutDir>$(OutputPath)</OutDir>
        <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
      </PropertyGroup>

      <ItemGroup>
        <None Remove="logo.png" />
        <None Remove="plugin.json" />
        <None Remove="Views\PublicInfo.cshtml" />
        <None Remove="Views\Admin\ExternalAuthSSO\Configure.cshtml" />
        <!-- ...one entry per .cshtml... -->
      </ItemGroup>

      <ItemGroup>
        <Content Include="logo.png"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>
        <Content Include="plugin.json"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>
        <Content Include="Views\PublicInfo.cshtml"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>
        <!-- ...one entry per .cshtml... -->
      </ItemGroup>

      <ItemGroup>
        <ProjectReference Include="$(SolutionDir)\Presentation\Nop.Web.Framework\Nop.Web.Framework.csproj" />
        <ClearPluginAssemblies Include="$(SolutionDir)\Build\ClearPluginAssemblies.proj" />
      </ItemGroup>

      <ItemGroup>
        <!--
            IMPORTANT: pin OpenIdConnect to 8.0.* so that its transitive
            Microsoft.IdentityModel.* dependencies (7.7.x) match the version
            Nop.Web 4.90 ships. Using 9.0.* pulls in Microsoft.IdentityModel 8.0.x
            which collides with the framework's 7.7.x copy at AssemblyLoadContext
            resolution time and throws ReflectionTypeLoadException during
            AddControllersAsServices(). See §3.12 Notes for the framework-upgrade
            checkpoint.
        -->
        <PackageReference Include="Microsoft.AspNetCore.Authentication.OpenIdConnect" Version="8.0.*" />
        <PackageReference Include="ITfoxtec.Identity.Saml2" Version="4.*" />
        <PackageReference Include="ITfoxtec.Identity.Saml2.MvcCore" Version="4.*" />
      </ItemGroup>

      <Target Name="NopTarget" AfterTargets="Build">
        <MSBuild Projects="@(ClearPluginAssemblies)" Properties="PluginPath=$(OutDir)" Targets="NopClear" />
      </Target>
    </Project>

Notes:

- **No `Nop.Web` reference.** The legacy plan's P0-7/P0-8 issues are designed
  out: `LocalLoginEnforcementFilter.IsCustomerLoginPost` matches by controller
  name string. No `<Private>false</Private>` is needed because
  `Build/ClearPluginAssemblies.proj` strips duplicated framework DLLs from the
  plugin output after build — this is the OOTB pattern (confirmed in
  `Nop.Plugin.ExternalAuth.Facebook.csproj`).
- `CopyLocalLockFileAssemblies=true` is required because we pull NuGet
  packages (`Microsoft.AspNetCore.Authentication.OpenIdConnect`, ITfoxtec).
- `<Nullable>enable</Nullable>` per the AGENTS.md §6 recommendation for new
  CW-shaped plugins (this plugin isn't CW-namespaced but follows the same
  conventions).
- ITfoxtec package versions: pin to the latest 4.x .NET 9-compatible release
  at implementation time.
- **`Microsoft.IdentityModel.*` version coupling (FRAMEWORK-UPGRADE CHECKPOINT).**
  `Microsoft.AspNetCore.Authentication.OpenIdConnect` transitively pulls in the
  `Microsoft.IdentityModel.*` assemblies (`Tokens`, `Protocols`,
  `Protocols.OpenIdConnect`, `JsonWebTokens`, `Logging`, `Abstractions`).
  **These must match the major version that ships in Nop.Web/bin**, otherwise the
  plugin's `AssemblyLoadContext` resolves a different copy than the framework's
  default ALC and `mvcBuilder.AddControllersAsServices()` throws
  `ReflectionTypeLoadException` at startup with `Unable to load one or more of the
  requested types`. Verified against nopCommerce 4.90:
  - **Nop.Web 4.90 ships `Microsoft.IdentityModel.*` 7.7.1.x.**
  - **Therefore pin `Microsoft.AspNetCore.Authentication.OpenIdConnect` to `8.0.*`**
    (which has transitive deps on `Microsoft.IdentityModel.*` 7.x). Using `9.0.*`
    pulls in 8.0.x and breaks the load.
  - **Pin `ITfoxtec.Identity.Saml2.MvcCore` to `4.10.*`** for the same reason.
    Versions 4.11.0 and later bumped their transitive
    `Microsoft.IdentityModel.Tokens.Saml` reference from 7.x to 8.x, which
    cascades into `Microsoft.IdentityModel.Tokens 8.x` and produces the
    identical `ReflectionTypeLoadException` at startup
    (`Could not load file or assembly 'Microsoft.IdentityModel.Tokens,
    Version=8.2.1.0…' The located assembly's manifest definition does not
    match the assembly reference.`). 4.10.x is the last 4.x line that targets
    the 7.x family.

  When the host framework is upgraded (likely end of 2026 to nopCommerce 5.0 / .NET 10),
  check the `FileVersion` of
  `Presentation/Nop.Web/bin/<config>/<tfm>/Microsoft.IdentityModel.Tokens.dll`
  after pulling the upgrade and bump the plugin's
  `Microsoft.AspNetCore.Authentication.OpenIdConnect` major to whatever line
  transitively references that version (typically `OpenIdConnect 9.0.*` pairs
  with `Microsoft.IdentityModel.* 8.x`, `OpenIdConnect 10.0.*` with `9.x`, etc.).
  The capture-and-restore MSBuild targets (`CaptureOidcDependencies` /
  `RestoreOidcDependencies`) in the csproj are defensive belt-and-braces — they
  keep the plugin's `bin/` populated even if `ClearPluginAssemblies` strips the
  DLLs, but they cannot rescue a real version mismatch. Version pinning is the
  primary fix.

### 3.13 Locale resource key namespace

All keys under `Plugins.ExternalAuth.SSO.*` (parallel to
`Plugins.ExternalAuth.Facebook.*`). This is **not** the CW convention
(`Plugins.CW.{PluginName}.Fields.{PropertyName}`) because the plugin is not
CW-namespaced; it follows the nop OOTB external-auth plugin convention
instead.

---

## 4. OpenID Connect flow

**SP-initiated login (the only mode for OIDC).**

1. Customer clicks "Sign in with Microsoft" → form POST to
   `/external-auth/microsoft/challenge`.
2. `ExternalAuthenticationController.Challenge` sees `Protocol == OpenIdConnect`
   and issues an ASP.NET Core challenge for the
   `"ExternalAuthSSO-Oidc"` scheme with `RedirectUri = "/signin-microsoft"`.
3. `OpenIdConnectHandler` (configured per-store via
   `IPostConfigureOptions<OpenIdConnectOptions>`) builds the
   authorization request with PKCE and redirects the browser to Entra's
   `/authorize` endpoint.
4. Entra authenticates, issues the authorization code, redirects to
   `/signin-microsoft`.
5. `OpenIdConnectHandler` exchanges the code for an ID token + access token,
   validates the ID token signature against the JWKS endpoint (auto-refreshed
   from the discovery document), creates a `ClaimsPrincipal`, and raises
   `OnTokenValidated`.
6. `OnTokenValidated` calls `SsoUserProvisioningService.HandleAsync`:
   - Extracts `email` (preferred claim) / `preferred_username` / `name` /
     `groups` (Entra Object IDs).
   - Domain allowlist (`AllowedEmailDomains`, supports wildcards).
   - Admin-account link block: if a nop customer with that email already
     exists and is in `Administrators`, fail with a
     `Failed` audit row.
   - Resolves roles via `SsoGroupRoleMappingService.ResolveCustomerRoleIdsAsync`.
   - Builds `ExternalAuthenticationParameters`:
       * `ProviderSystemName = "ExternalAuth.SSO"`
       * `ExternalIdentifier = sub`
       * `ExternalDisplayIdentifier = email`
       * `Email`, `FirstName`, `LastName`, `Claims`
   - Calls `IExternalAuthenticationService.AuthenticateAsync(parameters)`.
7. nop's external auth pipeline handles account link, auto-register (per
   `AutoRegister`), sign-in, cart migration, `CustomerLoggedinEvent`,
   `ActivityLog.PublicStore.Login`.
8. `SsoEventService.LogAsync` writes the audit row (after sign-in succeeds,
   in a `try`/`catch`).
9. Redirect to `PostLoginRedirectUrl` or `~/` (open-redirect-guarded).

**OIDC SLO.** Triggered by nop's standard logout endpoint. The OIDC handler's
`SignOutAsync` redirects to Entra's `end_session_endpoint` with an
`id_token_hint` and a `post_logout_redirect_uri`. Entra clears its session and
redirects back. Audit row written with `EventType.Slo`.

---

## 5. SAML 2.0 flow (ITfoxtec)

**SP-initiated login.**

1. Customer clicks "Sign in with Microsoft" → POST to
   `/external-auth/microsoft/challenge`.
2. `ExternalAuthenticationController.Challenge` sees `Protocol == Saml2`,
   calls `SamlChallengeBuilder.BuildAsync(store)`:
   - Loads per-store SAML config + decrypts IdP cert and (if applicable)
     SP PFX.
   - Builds `Saml2AuthnRequest(config) { NameIdPolicy = ..., Subject = ... }`.
   - Bind via `Saml2RedirectBinding` (with optional signing if
     `SignedRequest = true` — ITfoxtec handles this end-to-end, no
     hand-rolled signature code).
   - Stores the request ID in a `SameSite=Lax`, `HttpOnly` correlation
     cookie (`.AspNetCore.ExternalAuthSSO.SamlCorrelation`).
   - Writes a `Login` audit row.
   - Returns the redirect.
3. IdP authenticates and POSTs `SAMLResponse` to `/saml/acs`.
4. `SamlController.Acs` calls `SamlResponseProcessor.HandleAsync(httpRequest, store)`:
   - Loads per-store config.
   - Unbinds via `Saml2PostBinding` — ITfoxtec performs signature validation,
     digest method allowlist enforcement, EncryptedAssertion decryption,
     NotBefore/NotOnOrAfter window with skew, AudienceRestriction, replay
     protection, XXE-prohibited XML reading.
   - Validates `InResponseTo` against the request ID from the correlation
     cookie (cookie cleared on success).
   - Extracts claims, mapped through the same provisioning service as OIDC
     (§4 step 6). `NameID` is stored as `IdpSubject` for SLO.
5. Same downstream as OIDC (`IExternalAuthenticationService.AuthenticateAsync`,
   audit, redirect).

**SAML metadata.** `GET /saml/metadata` returns a per-store `<EntityDescriptor>`
generated by ITfoxtec from the loaded SAML config. Anonymous endpoint, no
secrets exposed.

**IdP metadata import.** `IdpMetadataImporter.ImportFromXmlAsync(xml)` and
`ImportFromUrlAsync(url)` parse `<EntityDescriptor>` and extract
`IdpEntityId`, `IdpSsoUrl`, `IdpSloUrl`, and the IdP signing certificate.
`IIdpMetadataCache` holds parsed metadata for a configurable TTL (default 24h)
and refreshes from `IdpMetadataUrl` if configured. Fixes legacy P2-1.

---

## 6. Single Log-Out

**OIDC SLO** — handled by `OpenIdConnectHandler.SignOutAsync` against the
discovered `end_session_endpoint`. No plugin code beyond enabling the option
and configuring `SignedOutCallbackPath`.

**SAML SLO** — controller-side, via ITfoxtec:

- **SP-initiated** (customer clicks Logout): `SamlController.Logout` reads the
  customer's `IdpSubject` and `SessionIndex` from generic attributes, builds
  a `Saml2LogoutRequest`, binds via `Saml2RedirectBinding`, redirects to
  `IdpSloUrl`.
- **IdP-initiated** (POST `/saml/logout`): ITfoxtec's `Saml2PostBinding`
  unbinds the request, the controller signs the user out locally, builds a
  `Saml2LogoutResponse`, returns the redirect to the IdP's reply URL.
- Audit row `EventType.Slo` written on completion.

`POST /saml/logout` is decorated with `[IgnoreAntiforgeryToken]` (the binding
is a different security model than the standard nop form-POST).

---

## 7. Security mitigations

Mapping legacy plan §3.9 mitigations to the new implementation. The "Where"
column shows that most security-critical code is now library behaviour
(ITfoxtec or `Microsoft.AspNetCore.Authentication.OpenIdConnect`) rather than
plugin code.

| Legacy ref | Mitigation | Where in v2 |
|---|---|---|
| C1 | Sensitive fields encrypted at rest | `SsoSecretsService` (IEncryptionService + IDataProtector) |
| C2 | Replay protection | ITfoxtec (SAML) / framework JWT nonce + state validation (OIDC) |
| C3 | Unsigned assertions rejected | ITfoxtec hard-fails when signature is missing or invalid |
| C4 | Open-redirect guard | `IUrlHelper.IsLocalUrl` on `PostLoginRedirectUrl` / `PostLogoutRedirectUrl` |
| C5 | Privileged roles hard-blocked from assignment | `SsoGroupRoleMappingService.ResolveCustomerRoleIdsAsync` + `SsoUserProvisioningService` |
| H1 | Encryption round-trip on sensitive settings | `SsoSecretsService` with explicit fail-fast on decrypt failure (fixes legacy P1-16) |
| H2 | Issuer + Audience + InResponseTo + NotBefore/NotOnOrAfter | ITfoxtec (SAML) / framework token validation (OIDC) |
| H3 | InResponseTo correlation | Correlation cookie (not session — fixes legacy P2-7) |
| H4 | Email-domain allowlist + admin link block | `SsoUserProvisioningService` (wildcard support fixes legacy P1-19) |
| H5 | TestConnection SSRF guard | `TestConnection` controller action (host allowlist, HTTPS-only, private-IP block) |
| M2 | 5-min clock skew | ITfoxtec / framework defaults |
| P1-3 | XXE prohibited | ITfoxtec XML readers (`DtdProcessing.Prohibit`) |
| P1-4 | Strong signature/digest algorithms only | ITfoxtec allowlist (SHA-256/384/512 RSA) |
| P1-9 | Mock IdP removed in Release builds | **Dev mock retained behind `#if DEBUG` + per-store `EnableMockMode` toggle** — controller, view, model, and Configure-page card all compile out in Release. Long-term: replaced by a proper integration-test harness (deferred). See decisions row 24. |
| P1-10 | Audit row after `SignIn`, try/catch | `SsoUserProvisioningService` |
| P1-20 | Permissions default-granted only to `Administrators` | `ExternalAuthSSOPermissionProvider` |

Two legacy items are **superseded by design** and don't need explicit fixes:

- **P0-11** (filter registered when uninstalled): solved by `INopStartup`,
  which only runs for installed plugins.
- **P1-13** (double `CustomerRegisteredEvent`): solved by delegating to
  `IExternalAuthenticationService.AuthenticateAsync`, which publishes the
  event once internally.

---

## 8. Deferred items

These are intentionally **not** in scope for the initial implementation and
will be revisited after the core plugin is functional:

- **Automated tests.** Unit tests for claim mapping, allowlist matchers,
  bypass-token comparator, group→role resolver, open-redirect guard.
  Integration tests against a fake IdP (likely `IdentityServer` test host for
  OIDC; ITfoxtec's test samples for SAML). Where the test project lives
  (parallel `Tests/CW.Tests` vs `Tests/Nop.Tests`) is a separate decision
  blocked by AGENTS.md Note A.
- **Certificate-expiry surfacing.** Read `NotAfter` from the SAML IdP/SP
  certificates and surface on the Configure page; scheduled task to warn N
  days before expiry. OIDC has no equivalent (JWKS auto-rotates).
- **Health checks.** `IHealthCheck` for SSO readiness (cert valid, IdP
  reachable).
- **OpenTelemetry traces / metrics** for auth events.
- **Scheduled audit-log retention.** Wire `PurgeOlderThanAsync` to an
  `IScheduleTask`.
- **Azure Key Vault wiring beyond design.** The settings layer supports a
  Key Vault URI for the SAML SP PFX but the production wiring lives in
  per-environment configuration and is not built in this phase.
- **Framework-upgrade checkpoint — `Microsoft.IdentityModel.*` version coupling.**
  When the next nopCommerce framework upgrade lands (planned end of 2026), both
  the plugin's `Microsoft.AspNetCore.Authentication.OpenIdConnect` package
  **and** its `ITfoxtec.Identity.Saml2.MvcCore` package must be re-pinned so
  that their transitive `Microsoft.IdentityModel.*` deps match what the
  upgraded `Nop.Web` ships. Currently pinned to `OpenIdConnect 8.0.*` and
  `ITfoxtec 4.10.*` because Nop.Web 4.90 carries `Microsoft.IdentityModel.*`
  7.7.1.x; using a mismatched major on either package causes
  `ReflectionTypeLoadException` at startup with the message
  *`Could not load file or assembly 'Microsoft.IdentityModel.Tokens,
  Version=8.x.x.x'`*. Full procedure documented in §3.12 Notes.

---

## 9. Open product questions

Resolved during the design discussion:

- **Q1** (group→role mapping shape): dedicated entity, GUID-keyed.
- **Q2** (refresh cadence): every login, within the plugin's owned role set.
- **Q3** (multi-IdP per store): one protocol per store, one IdP per protocol.
- **Q4** (CW.Core dependency): none — generic plugin.
- **Q5** (legacy sample config retirement): the file is deleted as part of
  the rename from `Nop.Plugin.ExternalAuth.SSO` to
  `Nop.Plugin.ExternalAuth.SSO`. Git history concern remains an ops
  task.

No outstanding product questions for v1.

---

## 10. Implementation milestones

Proposed phasing, each gated by explicit human approval per the project
instructions:

**M0 — Scaffold and rename.** Create the new folder
`Plugins/Nop.Plugin.ExternalAuth.SSO/` (or rename the existing
`Plugins/Nop.Plugin.ExternalAuth.SSO/`), apply the csproj template from §3.12, update
`plugin.json`. Delete the legacy `SAMLSSOConfig_StoreID_110 (1).json`. No
runtime code yet — just the project boots and shows in the plugin list as
"not installed."

**M1 — Data layer.** `SsoAuthEvent` + `SsoGroupRoleMapping` entities, builders,
`MigrationVersions.cs`, `SchemaMigration`, `INameCompatibility`. The plugin
installs cleanly, creates the two tables, drops them on uninstall.

**M2 — Settings and admin Configure page.** Both `ISettings` classes,
encryption layering, the Configure form (no IdP metadata import yet, no Test
Connection yet), per-store store-scope dropdown. Plugin admin menu hookup via
`AdminMenuCreatedEvent`.

**M3 — Group→role mapping admin.** `SsoGroupRoleMapping` entity already exists
from M1; this milestone adds the service, the list/edit pages, FluentValidation,
the DataTables grid. Privileged-role guard tests (manual, by inspection).

**M4 — OIDC end-to-end.** `OidcAuthenticationRegistrar`, per-store options
binding, `ExternalAuthenticationController.Challenge`, `SsoUserProvisioningService`
wired against `IExternalAuthenticationService`, login button view component.
End-to-end test with a developer Entra tenant: auto-register, link, group→role
mapping applied, audit log populated, SLO works.

**M5 — Filters.** `LoginActionsFilter` + `LocalLoginEnforcementFilter`,
including the bypass-token + correlation cookie. End-to-end test: SSO
auto-redirect, bypass works for admin, bypass denied for non-admin.

**M6 — SAML end-to-end.** ITfoxtec config, `SamlController`, challenge +
ACS + metadata endpoints, SLO (SP- and IdP-initiated). Same E2E test surface
as M4 against a SAML IdP.

**M7 — IdP metadata import.** `IdpMetadataImporter` for XML and URL, admin
page integration, optional auto-refresh from URL.

**M8 — Test Connection.** Diagnostic endpoint for both protocols. SSRF guard.

After M8 the plugin is feature-complete for the initial scope. The deferred
items in §8 become a follow-up backlog.

---

## 11. Migration from the legacy plugin

Out-of-scope for v1: there are no production users of the legacy
`Nop.Plugin.ExternalAuth.SSO` plugin on this branch (nopCommerce 4.90 is itself the
target uplift). When the new plugin replaces the legacy one in a deployed
environment, the migration is:

1. Uninstall `Auth.SSO` (it drops `SSO_AuthEvent`).
2. Install `ExternalAuth.SSO` (creates fresh `SSO_AuthEvent` and
   `SSO_GroupRoleMapping`).
3. Re-enter per-store configuration via the Configure page (no automated
   `Setting`-table migration — the schemas are intentionally different).
4. Re-enter group→role mappings (no legacy equivalent existed; this is net
   new).

If a production environment **does** have audit history worth preserving,
that's a separate one-off data migration not covered here.

---

## 12. Integration points

### 12.1 External files / tables / endpoints

| Thing | Interaction |
|---|---|
| `Setting` table | Per-store `SsoConfigurationSettings.*` + system-wide `SsoSystemSettings.*` rows |
| `PermissionRecord` / `PermissionRecord_Role_Mapping` | `AccessSsoPlugin` + `AccessSsoSettings`, default-granted to `Administrators` |
| `SSO_AuthEvent` | Created by FluentMigrator on install |
| `SSO_GroupRoleMapping` | Created by FluentMigrator on install |
| `Customer.GenericAttribute` | `SamlNameId`, `SamlSessionIndex` (for SP-initiated SLO; multi-tab safe per §3.5) |

### 12.2 Routes added

| Method | Route | Action |
|---|---|---|
| POST | `/external-auth/microsoft/challenge` | branches to OIDC challenge or SAML AuthnRequest |
| GET | `/signin-microsoft` | OIDC callback (standard ASP.NET Core handler) |
| GET | `/signout-callback-microsoft` | OIDC post-logout callback |
| POST | `/saml/acs` | SAML response handler |
| GET | `/saml/logout` | SAML SLO (SP-initiated and response-callback) |
| POST | `/saml/logout` | SAML SLO (IdP-initiated) |
| GET | `/saml/metadata` | SP metadata |
| GET/POST | `/Admin/ExternalAuthSSO/Configure` | per-store config |
| GET | `/Admin/ExternalAuthSSO/AuditLog` | DataTables grid |
| POST | `/Admin/ExternalAuthSSO/AuditLogList` | DataTables AJAX endpoint |
| GET | `/Admin/ExternalAuthSSO/GroupRoleMappings` | list |
| GET/POST | `/Admin/ExternalAuthSSO/GroupRoleMappings/Edit/{id}` | edit |
| POST | `/Admin/ExternalAuthSSO/GroupRoleMappings/Delete/{id}` | delete |
| POST | `/Admin/ExternalAuthSSO/ImportIdpMetadata` | metadata import |
| POST | `/Admin/ExternalAuthSSO/TestConnection` | diagnostic |

### 12.3 Locale resources

All under `Plugins.ExternalAuth.SSO.*`. Installed via
`AddOrUpdateLocaleResourceAsync` in `InstallAsync`, removed via
`DeleteLocaleResourcesAsync("Plugins.ExternalAuth.SSO")` in
`UninstallAsync`.

---

## Session log — May 2026

Snapshot of changes layered onto the plan above. Detailed context + file map in `SSO_SESSION_HANDOFF.md` (this folder).

### Renamed
- Plugin `Nop.Plugin.ExternalAuth.Microsoft` → `Nop.Plugin.ExternalAuth.SSO`. Folder, namespace, SystemName (in `plugin.json`), assembly name, csproj OutputPath, locale keys (`Plugins.ExternalAuth.Microsoft.* → .SSO.*`), OIDC scheme (`ExternalAuthMicrosoft-Oidc → ExternalAuthSSO-Oidc`), `ViewLocationExpander.PLUGIN_FOLDER`, class names (`MicrosoftAuthenticationMethod → SSOAuthenticationMethod`, `MicrosoftAuthenticationDefaults → SSOAuthenticationDefaults`, view component, etc.) all moved.

### Added / completed (beyond the original milestone plan)
- **Mock IdP** (row 24, P1-9): reinstated as a `#if DEBUG`-only dev aid with per-store `EnableMockMode` toggle. 4 scenarios (success-existing / success-new / success-with-groups / failure). Mock complete endpoints build a `ClaimsPrincipal` directly and dispatch to `ISsoUserProvisioningService.HandleAsync` — real cryptographic paths are not touched.
- **Protocol-aware visibility JS** in `Views/Configure.cshtml` — SAML cards hide under OIDC and vice versa; mixed Bindings & Redirects card hides only the SAML binding rows.
- **OIDC variant of "Send to IdP team" card** — Redirect URI + Post-logout Redirect URI rows toggled alongside the SAML SP-metadata-URL row.
- **Configure-page UX:** Save button moved to the upper-right content-header (`AGENTS.md §9.4`). Per-store cards render only when scope > 0. System-wide cards (Login Bypass, Audit Logging) render only when scope = 0 ("All stores").
- **Multi-store UX revision** (row 4): standard nop `StoreScopeConfigurationViewComponent` retained; per-field `_OverrideForStore` checkboxes REMOVED from view and model. Override is implicit when a specific store is selected — factory writes every per-store field with `overrideForStore: true`. At scope = 0, only system-wide settings save.
- **Delete on SSO Accounts grid:** red button in the Actions column → POST `DeleteSsoAccount` → wipes per-store SSO settings rows AND writes defaults back to block nop's `loadSharedValueIfNotFound: true` fallback to storeId = 0.
- **OIDC validation crash fix** (`Infrastructure/OidcAuthenticationRegistrar.cs`): placeholder `Authority` + `ClientId` so the request-handler scheme doesn't throw `ArgumentException` on every request when admin hasn't configured OIDC yet.
- **Dropdown placeholders:** `*` replaced with localised "All stores" / "All event types" / "All protocols" in audit-log + group→role-mapping filter dropdowns.
- **Group → Role Mappings page intro banner** — info card at the top explaining purpose, owned-set logic, privileged-role guard, per-store scoping, Group ID convention.
- **Goodstart test-store auto-seed:** when `AddSsoAccount(POST)` is invoked for a store literally named "Goodstart" (case-insensitive), the controller seeds SAML 2.0 config inline from `SAMLSSOConfig_StoreID_110.json` (cert encrypted via `_secretsService.Protect`). All required form fields populated with valid placeholder values. Delete the `if (store.Name == "Goodstart")` block when the test tenant is decommissioned.
- **URL → admin-store-scope attribute sync** — fixed bug where SSO Accounts grid's Edit button opened the wrong store (nop's `StoreScopeConfigurationViewComponent` reads from a generic attribute, not the URL). Refactored by user into a factory method `IConfigurationModelFactory.ChangeSSOSope(int)`.

### Deferred (carry over to next session)
- Per-store dynamic OIDC options binding via `IPostConfigureOptions<OpenIdConnectOptions>` so changes to OIDC settings don't require an app restart (`§3.5`).
- Integration test harness — current Mock IdP is a dev aid, not automated coverage (`§8`, AGENTS.md Note A).
- Cert-expiry health check + OpenTelemetry hookups (`§8`).
- Migration script to clean up legacy `storeId=0` `SsoConfigurationSettings` rows AND legacy `Plugins.ExternalAuth.Microsoft.*` locale rows on environments that had the pre-rename install.

### Open approvals
- Implementation tasks delivered but not yet signed off by user — listed in `SSO_SESSION_HANDOFF.md` §5. Verify-and-present tasks stayed `in_progress` per the "no auto-complete" project rule.

### Recurring infrastructure note
- File-write tool hit a recurring truncation/NUL-padding bug ~12 times this session. Recovery pattern (`tr -d '\000'` + bash heredoc append) documented in `SSO_SESSION_HANDOFF.md` §3. Consider splitting `SSOAuthenticationMethod.cs` (locale-seeding only, ~480 lines) and `Views/Configure.cshtml` (~1000 lines) to reduce the danger zone.

---

*Plan document maintained alongside the plugin source. Supersedes
`SSOPlan.md`. Implementation begins on explicit human approval of M0 in §10.*
r).

### Renamed
- Plugin `Nop.Plugin.ExternalAuth.Microsoft` → `Nop.Plugin.ExternalAuth.SSO`. Folder, namespace, SystemName (in `plugin.json`), assembly name, csproj OutputPath, locale keys (`Plugins.ExternalAuth.Microsoft.* → .SSO.*`), OIDC scheme (`ExternalAuthMicrosoft-Oidc → ExternalAuthSSO-Oidc`), `ViewLocationExpander.PLUGIN_FOLDER`, class names (`MicrosoftAuthenticationMethod → SSOAuthenticationMethod`, `MicrosoftAuthenticationDefaults → SSOAuthenticationDefaults`, view component, etc.) all moved.

### Added / completed (beyond the original milestone plan)
- **Mock IdP** (row 24, P1-9): reinstated as a `#if DEBUG`-only dev aid with per-store `EnableMockMode` toggle. 4 scenarios (success-existing / success-new / success-with-groups / failure). Mock complete endpoints build a `ClaimsPrincipal` directly and dispatch to `ISsoUserProvisioningService.HandleAsync` — real cryptographic paths are not touched.
- **Protocol-aware visibility JS** in `Views/Configure.cshtml` — SAML cards hide under OIDC and vice versa; mixed Bindings & Redirects card hides only the SAML binding rows.
- **OIDC variant of "Send to IdP team" card** — Redirect URI + Post-logout Redirect URI rows toggled alongside the SAML SP-metadata-URL row.
- **Configure-page UX:** Save button moved to the upper-right content-header. Per-store cards render only when scope > 0. System-wide cards render only when scope = 0.
- **Multi-store UX revision** (row 4): standard nop `StoreScopeConfigurationViewComponent` retained; per-field `_OverrideForStore` checkboxes REMOVED. Override is implicit when a store is selected — factory writes every per-store field with `overrideForStore: true`. At scope = 0, only system-wide settings save.
- **Delete on SSO Accounts grid:** red button → POST `DeleteSsoAccount` → wipes per-store rows AND writes defaults back to block nop's `loadSharedValueIfNotFound: true` fallback.
- **OIDC validation crash fix** (`Infrastructure/OidcAuthenticationRegistrar.cs`): placeholder `Authority` + `ClientId` so unconfigured OIDC scheme doesn't throw on every request.
- **Dropdown placeholders:** `*` replaced with localised "All stores" / "All event types" / "All protocols".
- **Group → Role Mappings page intro banner** — info card explaining purpose, owned-set logic, privileged-role guard, per-store scoping, Group ID convention.
- **Goodstart test-store auto-seed:** `AddSsoAccount(POST)` detects `store.Name == "Goodstart"` (case-insensitive) and seeds SAML 2.0 config inline from `SAMLSSOConfig_StoreID_110.json`. Cert encrypted via `_secretsService.Protect`. All required form fields populated. Delete the `if (store.Name == "Goodstart")` block when the test tenant goes away.
- **URL → admin-store-scope sync** — fixed bug where SSO Accounts Edit button opened the wrong store. Refactored by user into `IConfigurationModelFactory.ChangeSSOSope(int)`.

### Deferred (carry over to next session)
- Per-store dynamic OIDC options binding via `IPostConfigureOptions<OpenIdConnectOptions>` so changes don't require an app restart (§3.5).
- Integration test harness — current Mock IdP is a dev aid, not automated coverage (§8, AGENTS.md Note A).
- Cert-expiry health check + OpenTelemetry hookups (§8).
- Migration script to clean up legacy `storeId=0` `SsoConfigurationSettings` rows AND legacy `Plugins.ExternalAuth.Microsoft.*` locale rows on environments with the pre-rename install.

### Open approvals
- Implementation tasks delivered but not yet signed off by user — listed in `SSO_SESSION_HANDOFF.md` §5. Verify-and-present tasks stayed `in_progress` per the "no auto-complete" project rule.

### Recurring infrastructure note
- File-write tool hit a recurring truncation/NUL-padding bug ~12 times this session. Recovery: `tr -d '\000'` + bash heredoc append. Documented in `SSO_SESSION_HANDOFF.md` §3. Consider splitting `SSOAuthenticationMethod.cs` (~480 lines, locale-seeding only) and `Views/Configure.cshtml` (~1000 lines) to reduce the danger zone.

---

*Plan document maintained alongside the plugin source. Supersedes `SSOPlan.md`. Implementation begins on explicit human approval of M0 in §10.*

### Recurring infrastructure note
- File-write tool hit a recurring truncation bug ~12 times this session. Recovery: `tr -d '\000'` + bash heredoc append. Documented in `SSO_SESSION_HANDOFF.md` §3.

---

*Plan document maintained alongside the plugin source. Supersedes `SSOPlan.md`.*
 only, ~480 lines) and `Views/Configure.cshtml` (~1000 lines) to reduce the danger zone.

---

*Plan document maintained alongside the plugin source. Supersedes `SSOPlan.md`. Implementation begins on explicit human approval of M0 in §10.*
10.*
