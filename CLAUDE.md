# CW nopCommerce 5.00 — Project Reference for Claude & Contributors

> **Read this document first on every task.** It is the working agreement for all contributors (human and AI) on this codebase.
>
> Adapted on 2026-10-20 from the 4.9 repository's `CLAUDE.md` (`C:\Work\TIG_Nop_4_9`). The rules are the same;
> what describes code lists what is in **this** repository. Sections that still describe 4.9 code carry a banner
> saying so (§13–§21).

---

## 0. Agent / AI Workflow Rules

These rules govern how Claude and any AI agent must behave on this codebase. They take precedence over any general defaults.

### 0.1 Code Touch Policy

At the **start of each session**, request permission **once** to modify code directly. Ask a single time per session and abide by the answer for the rest of that session — do not re-ask after a decision has been made.

- **If permission is granted:** you may edit, write, and modify source files to carry out the session's tasks — subject to every other rule here (Approval Gates §0.2, Scope Discipline §0.5, Quality Standards §0.6) and the Cardinal Rule (§2: never modify original nopCommerce code — customisations live in plugins).
- **If permission is denied (or not yet requested):** **Never change the code directly.** Always provide the information and instructions needed to make the change, but leave the programmer to implement it. Do not edit, write, or delete source files in the project folders.
  - **How a change is handed over.** Every change is a literal FIND / REPLACE WITH pair of whole lines, under a heading
    that names the full file path. **Every FIND states the line numbers it sits at** (`lines 868–872`), counted in the
    file as it stands when that change is reached — after every earlier change in the list. When one file takes several
    blocks they are listed **bottom to top** and applied in that order, so no block moves the lines of the next; a FIND
    that anchors a member includes its doc comment; a file that changes in many places is given whole. One numbered list
    per phase in apply order; later additions continue the numbering. Line numbers and bottom-to-top are two halves of
    one rule — a spec with the order but no numbers cannot be applied the way it was written to be.

### 0.2 Approval Gates

- **Never mark a task complete or indicate work is done without explicit human approval.**
- Do not use sub-agents without user approval.
- Do not commit any changes to the repository. **Commits and pushes are the user's**, even when asked to "add it to
  the repository": prepare it (`.gitignore`, secret scrub, file review) and hand over the exact git commands. Never
  retry a refused push through another tool or the user's terminal (`tasks/lessons.md` #1).

### 0.3 Complexity Tiers

Before starting any task, self-classify its complexity:

**Tier 1 — Simple** (act directly, no plan mode required):
- Single-file change or an obvious fix with a clear, unambiguous solution
- No architectural decisions involved
- Fewer than 3 meaningful steps

**Tier 2 — Complex** (plan mode required):
- 3 or more distinct steps, or touches multiple files or layers
- Involves architectural decisions, new patterns, or new entities
- Requirements are ambiguous or underspecified

### 0.4 Process Rules

- Write a detailed spec or task breakdown upfront for Tier 2 tasks only.
- Ask clarifying questions for Tier 2 tasks, or when a Tier 1 task reveals hidden complexity mid-execution.
- If a Tier 1 task turns out to be more complex than expected, stop and re-classify before continuing.
- If something goes wrong mid-task, stop and re-plan immediately. Do not keep pushing through a broken approach.
- Use plan mode for verification steps on Tier 2 tasks, not just for building.
- **Working documents are consumed, not kept.** An exact-change spec describes a state that stops existing the
  moment it is applied. When work lands, fold what survives — the design, the decisions, the traps, what is
  still open — into **one** document per topic, then **move the originals to the plugin's
  `References/Deprecated/` folder** (never delete them). Do **not** carry a "documents this replaces" list into
  the consolidated file.
- **Plugin documents live in the plugin's own `References/` folder** (e.g.
  `Plugins/Nop.Plugin.ExternalAuth.SSO/References/`); superseded ones go in `References/Deprecated/`.
- **A phase's test plan lives inside that phase's document**, not beside it. One file to hand someone, not two.
- **Check every feature reader against the feature switch before specifying it** (CW.Core features, once ported).
  Any change that reads a feature's data — C#, Razor, JavaScript or SQL — is checked against
  `CWPlans/Feature_Toggles_Reference.md` §1 (in the 4.9 repository until `CWPlans` is brought across): the entry
  points close when the feature is off (§1.1), and so does every restriction the feature puts on the rest of the
  store (§1.5). SQL is a reader too; subsidy SQL reads the switch from
  `vw_CORE_SUB_StoreSubsidyVariationSetting.SubsidyEnabled`. The test plan includes the feature-off case.

### 0.5 Scope Discipline

> **Read §0.8 first.** While this project is pre-production, §0.8 relaxes the "smallest possible change"
> rule below in favour of getting the design right.

- Focus strictly on the assigned task. Do not improve, refactor, or clean up code that is not directly related to it.
- Do not delete comments or modify code outside the task scope, even if it looks wrong.
- Make every change as small as possible. Avoid introducing unintended side effects.
- Note any problems spotted outside scope for a follow-up task rather than fixing them silently.

### 0.6 Quality Standards

- Find root causes. No temporary fixes or workarounds. Apply the solution a senior developer would.
- For non-trivial changes, pause and ask: *"Is there a more elegant way?"* before implementing.
- If a fix feels hacky, step back: *"Knowing everything I know now, what is the correct solution?"*
- Skip this reflection for simple, obvious fixes — do not over-engineer.

### 0.7 Self-Improvement (Lessons)

Lessons follow a two-stage lifecycle: **capture** in `tasks/lessons.md`, then **promotion** to `CLAUDE.md §24` by the human when validated.

**Capturing a lesson** — after any correction from the user, record it in `tasks/lessons.md`:
```
| # | Mistake | Rule |
|---|---|---|
| 1 | Short description of what went wrong | Concrete rule that prevents it |
```
End the response with:
> ⚠️ New lesson recorded in `tasks/lessons.md` — review for promotion to `CLAUDE.md` when convenient.

**Applying a lesson** — when a past lesson is directly applicable, note it:
> ✅ Lesson applied: *[lesson text]* (`tasks/lessons.md` #N).

A reused lesson is a signal it is stable and ready for promotion.

**Session start** — review `tasks/lessons.md` and `§24 Lessons Learned` for rules relevant to the current task.

This repository has its own `tasks/lessons.md`, started on 2026-10-20 with the 4.9 lessons that apply here. The full
4.9 history (#1–#110) stays in `C:\Work\TIG_Nop_4_9\tasks\lessons.md`; read it when porting a 4.9 plugin.

### 0.8 Project Stage — pre-production (added 2026-08-14)

**The 5.00 codebase is not in production and has no users.** The 4.1 system is the one serving customers,
and it is untouched by this work. Nothing here can break a live store.

That changes the trade-off. **Getting the design right outweighs avoiding breakage.** In order of priority:

1. **Correct** — the behaviour matches what the business needs
2. **Readable** — a developer meeting the code cold can follow it
3. **Maintainable** — a change lands in one place, not five
4. **Performant** — this will run across many stores against a large database
5. *Then* — not breaking things

**What this permits**, without asking first:

- Redesigning a component rather than patching it, when the existing shape is the reason it keeps breaking
- Deleting dead code, dead properties, dead CSS, and abandoned abstractions on sight
- Changing method signatures, moving logic between layers, renaming for clarity
- Fixing latent bugs found while working nearby, rather than logging them for later
- Reshaping a data access path for performance even when the current one "works" at demo scale

**What it does not permit** — these are unchanged and not up for negotiation:

- **§2 Cardinal Rule.** Never modify nopCommerce core. This is about keeping upgrades a clean drop-in, not
  about avoiding breakage, so pre-production status is irrelevant to it.
- **§0.1 Code Touch Policy** and **§0.2 Approval Gates.** Still ask, still get approval, still no commits.
- **§0.3 Complexity Tiers.** A redesign is Tier 2 and still needs a spec and clarifying questions upfront.
- Silent scope creep. Wide changes are fine; *unannounced* wide changes are not. Say what is being
  changed and why before doing it.

**Revisit this section when the 5.00 system goes live.** At that point §0.5 resumes as written and this
section should be deleted rather than softened — a half-applied version of either rule is worse than
committing to one.

### 0.9 Public Repository (added 2026-10-20)

This repository is pushed to a **public** GitHub repository. Anything committed is readable by anyone, and stays in
the history even after it is removed.

- **Never put real tenant IDs, client secrets, certificates, PFX files, passwords, connection strings or customer
  data in a tracked file.** Test values and seeds use placeholders (`00000000-0000-0000-0000-000000000000`); real
  values are typed in locally and never committed.
- Files that hold real values stay in `.gitignore` — e.g. `SAMLSSOConfig_*.json` and the `miniorange.saml/` vendor
  copy in the SSO plugin, `App_Data/appsettings*.json`, `App_Data/DataProtectionKeys/`.
- Before handing over git commands (§0.2), scan the staged files for secrets and say what was checked.

---

## 1. Project Identity

| Property | Value |
|---|---|
| **Product** | nopCommerce **5.00** customized for CW (rebranded from Total Image Group / TIG) |
| **Runtime** | .NET **10.0** (`Directory.Build.props`) |
| **Database** | SQL Server (`appsettings.json` → `DataProvider: "sqlserver"`); local database `NOP_CW_5_0` |
| **Solution file** | `NopCommerce.sln` at repo root |
| **Repository** | Public GitHub repository — see §0.9 |
| **Ported plugins** | `Nop.Plugin.ExternalAuth.SSO` (the rest are being ported from 4.9 — see §3.2) |
| **Port source** | The 4.90 repository at `C:\Work\TIG_Nop_4_9` (§3.1) |

---

## 2. Cardinal Rule

**Never modify the original nopCommerce code. All customisations live in plugins.**

This applies to every project shipped by nopCommerce:
- `Libraries/Nop.Core`, `Libraries/Nop.Data`, `Libraries/Nop.Services`
- `Presentation/Nop.Web`, `Presentation/Nop.Web.Framework`
- `Tests/Nop.Tests`
- Any `Plugins/Nop.Plugin.*` that ships with nopCommerce out of the box

**`Nop.Plugin.ExternalAuth.SSO` is CW-owned**, even though its name follows nop's `ExternalAuth.*` pattern rather than
`CW.*`: every CW rule applies to it, and the ban on editing nop plugins does not. It keeps its name, its SystemName
`ExternalAuth.SSO` and its `Plugins.ExternalAuth.SSO.*` locale keys — a rename would change all three for no gain
(decided 2026-10-20).

If a customisation seems to require editing one of those projects, stop and re-plan. The correct path is almost always one of:

1. Add or extend a plugin under `Plugins/Nop.Plugin.CW.*`.
2. Override the relevant service — either inside the consuming plugin (feature-local) or in `Nop.Plugin.CW.Core` (shared). See §10.
3. Add a Razor view in the plugin and let the plugin's `ViewLocationExpander` shadow the core view.
4. Add an event consumer (`IConsumer<TEvent>`) instead of patching a service.

Upgrades to nopCommerce must remain a clean drop-in. If a core file has any TIG/CW diff, the upgrade path is broken.

---

## 3. Solution Layout

```
CW_NOP_5/
├── Libraries/              Nop.Core, Nop.Data, Nop.Services              (DO NOT EDIT)
├── Presentation/           Nop.Web, Nop.Web.Framework                    (DO NOT EDIT)
├── Plugins/                nop OOTB plugins (DO NOT EDIT)
│                           + Nop.Plugin.ExternalAuth.SSO (CW-owned — see §2)
│                               └── References/   the plugin's documents (Deprecated/ for superseded ones)
│                               └── miniorange.saml/  old vendor copy, gitignored, never built — reference only
├── CWLibraries/
│   └── CW.Common           Shared utilities for CW plugins (no domain logic — §11)
├── Tests/Nop.Tests         NUnit tests (DO NOT EDIT — see §22 Note A)
├── Build/                  ClearPluginAssemblies.proj
├── Directory.Build.props   Central .NET 10.0 + version metadata
├── tasks/lessons.md        Captured lessons (§0.7)
└── NopCommerce.sln
```

There is no `NOP41/` and no `CWPlans/` folder in this repository — see §3.1.

### 3.1 Researching legacy code

| What | Where | Use it for |
|---|---|---|
| **4.90 source** (the port source) | `C:\Work\TIG_Nop_4_9` | Every CW plugin not yet ported, its plans (`CWPlans/`) and its lessons (`tasks/lessons.md`) |
| **4.1 source** | `C:\Work\TIG_Nop_4_9\NOP41` | Checking original behaviour when a port is suspected of having dropped something (see the 4.9 `CLAUDE.md` §3.1) |

Both are **reference material**: read them, never edit them, never build them here, and never copy a folder across
wholesale. Port deliberately, into the 5.00 patterns (§10.5). `NOP41` is **not** copied into this repository.

> **Name collision.** The 4.9 repository has `Plugins/Nop.Plugin.TIG.Exchanges` (build artifacts only) as well as
> `NOP41/Nop.Plugin.TIG.Exchanges` (the 4.1 source). Scope legacy searches to `NOP41/` explicitly.

> **Rename status (carried from 4.9).** The brand moved from **TIG** to **CW** (Cityweb): `CW.Core`, `CW.Freight`,
> `CW.ThemeCore`, `CW.Theme.Standard` and the `CW.Standard` / `CW.StandardTest` themes use `CW` throughout (namespaces,
> SystemNames, table names, cache keys, permission keys, routes, locale keys, CSS custom properties). Only
> `TIG.NAB.XMLPayment` is still on the `TIG` prefix. Ported plugins arrive already renamed.

### 3.2 Port status

| Plugin | Purpose | Status in 5.00 | Reference doc |
|---|---|---|---|
| `Nop.Plugin.ExternalAuth.SSO` | Single sign-on with Entra ID (OIDC / SAML 2.0) | **Ported** 2026-10-19; reviewed and extended (plugin version `…_DATA_0.7`). Sign-in not yet tested against a real IdP | `Plugins/Nop.Plugin.ExternalAuth.SSO/References/SSO_Plugin_Reference.md` |
| `Nop.Plugin.CW.Core` | Base CW infrastructure, service overrides, exchanges, subsidies, packnames, admin menu root | Not started — §13–§21 describe its 4.9 code | 4.9 `CLAUDE.md` and `CWPlans/` |
| `Nop.Plugin.CW.Freight` | Shipping / freight | Not started | 4.9 repo |
| `Nop.Plugin.CW.ThemeCore` | Theme framework (accent colours, custom CSS) | Not started | 4.9 `CWPlans/CW_Theme_Build_Plans.md` |
| `Nop.Plugin.CW.Theme.Standard` | Settings for the `CW.Standard` theme | Not started | 4.9 repo |
| `Nop.Plugin.TIG.NAB.XMLPayment` | NAB payment gateway | Not started | 4.9 repo |
| `Nop.Plugin.Integration.Odoo` | Odoo ERP integration | Not started | 4.9 repo |

Update this table as each plugin lands. Coupling rules between them are in §10.2 and apply unchanged.

> Plugins `Misc.WebApi.Frontend`, `Misc.RFQ`, `Misc.Dynamics365`, `Misc.Brevo` are present in source but **not installed** — do not target them for new work.

---

## 4. Tech Stack (Locked Decisions)

| Concern | Choice |
|---|---|
| Web framework | ASP.NET Core MVC + Razor (no Blazor / SPA) |
| ORM / data access | **`IRepository<T>` exclusively** — no EF Core, no Dapper, no custom `DbContext` |
| Migrations | **FluentMigrator**, in each plugin's own `Data/Migrations/` folder |
| Validation (admin models) | **FluentValidation**, nop standard pattern |
| Object mapping | **Mapster** (nop 5.00 replaced AutoMapper) — profiles inherit `Nop.Core.Infrastructure.Mapper.BaseMapperProfile` (an `IOrderedMapperProfile`) and call its `CreateMap<,>()`, never the Mapster API directly. Mapping by hand in a factory is fine for a few fields (the SSO plugin has no profile) |
| Dependency injection | Register via `INopStartup.ConfigureServices(IServiceCollection)`. Constructor injection only — no `EngineContext.Current.Resolve<T>()` in new code. |
| Caching | `IStaticCacheManager` (distributed cache supported) |
| Frontend (admin) | jQuery 3.7 + AdminLTE 3.2 + **Bootstrap 4.6** |
| Asset bundling | WebOptimizer (`.scripts` / `.styles` suffixes) |
| Localization | XML resources + Globalize/CLDR |
| Test framework | NUnit 4.4 + FluentAssertions 7.2 + Moq 4.20 + SQLite (in `Tests/Nop.Tests`) |

---

## 5. Layered Structure

Every feature follows this layered structure. Do not skip layers or create cross-layer dependencies.

| Layer | Location | Responsibility |
|---|---|---|
| **Domain** | `Domain/` | Plain entity classes inheriting `BaseEntity`. No logic, no dependencies. |
| **Data** | `Data/Mapping/`, `Data/Migrations/` | Schema definition (FluentMigrator builders) and migrations. |
| **Service** | `Services/` | Business logic. Depends on `IRepository<T>` and other services. Always async. |
| **Model** | `Models/` (or `Areas/Admin/Models/`) | View models. Inherit `BaseNopEntityModel` or `BaseSearchModel`. |
| **Controller** | `Controllers/` (or `Areas/Admin/Controllers/`) | Thin — delegates to services, maps to/from models. |
| **View** | `Views/` | Razor pages. Use NopCommerce tag helpers and Bootstrap 4.6 layout. |
| **Business Logic** *(optional)* | `BusinessLogic/` | Orchestrator classes that coordinate multiple services for one workflow. Only for genuine cross-service coordination — see §6.13. |

### 5.1 Layer Dependency Rules

| Layer | May reference |
|---|---|
| `Domain/` | Nothing else from this plugin. Entities, enums and value objects only. |
| `Data/` | `Domain/` |
| `Services/`, `BusinessLogic/` | `Domain/`, `Data/`, other services. **Never `Models/` or `Areas/Admin/Models/`.** |
| `Models/` (public) | `Domain/` types only where genuinely needed |
| `Factories/` (public) | `Models/`, `Services/`. **Never `Areas/Admin/Models/`.** |
| `Areas/Admin/Factories/` | `Areas/Admin/Models/`, `Services/`, and public `Models/` if a screen genuinely reuses one |
| Controllers / Views | Their own area's factories and models |

A service that needs data *from* a form takes an **explicit input record it owns**, never a view model.
Input records live beside their consumer — e.g. `BusinessLogic/Requests/ReturnRequestStatusChangeData`,
mapped from the model by the controller at the boundary.

---

## 6. NopCommerce 5.00 Key Patterns

> Examples that name a CW.Core class (`CWNameCompatibility`, `ExchangeService`, `ExchangesLogic`, …) point at 4.9 code
> until CW.Core is ported; the patterns themselves hold for 5.00. 5.00 differences are listed in §10.5.

### 6.1 Base Classes & Interfaces

| Type | Purpose |
|---|---|
| `BaseEntity` | All domain entities inherit this — provides `int Id` |
| `ISoftDeletedEntity` | Adds `bool Deleted` — use `GetAll...Async` with `showDeleted: false` |
| `ISettings` | Plugin settings (serialised to `Setting` table by store) |
| `ISlugSupported` | Entity has SEO slug support |
| `INameCompatibility` | Maps `Type → table name` for LinqToDB (used in `CWNameCompatibility`) |
| `INopStartup` | Plugin service registration entry point |
| `IRouteProvider` | Register custom routes; `Priority` controls order (higher = earlier) |
| `IWidgetPlugin` | Plugin renders widget zones |
| `BasePlugin` | Plugin install/uninstall/update lifecycle |
| `BaseAdminController` | Base for admin area controllers in CW.Core and plugins that reference it |
| `BasePluginController` | Base for standalone plugin admin controllers |

### 6.2 Data Access

- **Repository pattern:** `IRepository<T>` (from `Nop.Data`) — inject and use `InsertAsync`, `UpdateAsync`, `DeleteAsync`, `GetByIdAsync`, `GetAllAsync`, `GetAllPagedAsync`.
- **LinqToDB queries:** For complex queries, inject `IRepository<T>` and call `.Table` to get an `IQueryable<T>`. Use LinqToDB LINQ extensions.
- **Views/stored procedures:** Accessed via `IRepository<ViewEntity>` where the entity class name maps to a view via `CWNameCompatibility`. The `[NotColumn]` attribute (LinqToDB) marks computed-only properties.
- **Migrations:** FluentMigrator. Each migration class lives in `Data/Migrations/`. Version numbers are managed in `Data/MigrationVersions.cs`.
- **Table naming:** `CW.Core`'s tables use `CWNameCompatibility` (implements `INameCompatibility`). Register in your builder by inheriting `NopEntityBuilder<T>`.

### 6.3 Caching

- NopCommerce uses `IStaticCacheManager` (in-memory) and `ILocker`.
- Cache keys are `CacheKey` objects with typed parameters.
- Cache invalidation is done through `ICacheEventConsumer<T>` — implement one per entity to clear relevant keys when the entity changes.
- CW cache key prefixes: `CW.packname.*`, `CW.subsidy.*`, `CW.departments.*`, `CW.branch.*`, `CW.costcenter.*`, etc. (see §16).

### 6.4 Services Override Pattern

To replace a NopCommerce service, create a class that inherits the NOP service class and overrides methods. Register it in `NopStartup.ConfigureServices` **after** the base registrar (`Order = DependencyRegistrarConstants.OrderAfterBaseRegistrar`):

```csharp
services.AddScoped<ICustomerService, CustomerOverrideService>();
```

Existing examples in CW.Core: `CustomerOverrideService`, `ShoppingCartOverrideService`, `AclOverrideService`, `OrderTotalCalculationServiceOverride`, `MessageTokenProviderOverride`.

### 6.5 Model Factory Pattern (Admin)

Admin area follows: **Controller → ModelFactory → Model → View**

- Model factories implement `I<Name>ModelFactory`.
- `SearchModel` extends `BaseSearchModel` (has paging).
- `ListModel<TModel>` extends `BasePagedListModel<TModel>`.
- Controllers call `[HttpPost] ActionResult List(SearchModel)` returning `Json(model)` consumed by a DataTables grid.

### 6.6 Permissions

- Permissions declared in classes implementing `IPermissionConfigManager`.
- Applied to controller actions via `[CheckPermission("permission.key")]` attribute.
- CW permissions defined in `CWPermissionConfigManager`:
  - `CW.CORE.Admin.AccessBranch`
  - `CW.CORE.Admin.AccessMultiStore`
  - `CW.CORE.Admin.AccessMultiStoreSettings`
  - `CW.CORE.Admin.AccessDefaultStoreSettings`

### 6.7 Events & Notifications

- Publish events via `IEventPublisher.PublishAsync(event)`.
- Consume events by implementing `IConsumer<TEvent>`.
- Notification service: `INotificationService` — `.SuccessNotification()`, `.ErrorNotification()`, `.WarningNotification()`.

### 6.8 Localization

- `ILocalizationService` for translating resource strings.
- In views: `@T("Resource.Key")`.
- Strings installed/uninstalled in plugin lifecycle via `_localizationService.AddOrUpdateLocaleResourceAsync(...)`.

### 6.9 Activity Log

- `ICustomerActivityService.InsertActivityAsync("SystemKeyword", description, entity)`.
- Log types installed during plugin install (see `CWCorePlugin.InstallActivityLogTypesAsync`).

### 6.10 Settings (per-store)

- Implement `ISettings`. Load with `ISettingService.LoadSettingAsync<T>(storeId)`.
- Save with `ISettingService.SaveSettingAsync(settings, storeId)`.
- CW main settings class: `MultiStoreSettings`.

### 6.11 Widget Zones & View Components

- `IWidgetPlugin.GetWidgetZones()` returns zones where the plugin renders.
- Rendering is done via `ViewComponent` classes.
- `CWCorePlugin` renders to zones: `PublicWidgetZones.HeaderLinksAfter`, and several `CWWidgetsZones.*` constants defined in `Constants/` folder.

### 6.12 View Location Expander

CW.Core uses a custom `ViewLocationExpander` and `ViewPathsRegistrar` so other plugins can register their own view paths through a central registry (`ViewsPathsRegistry`). This allows multiple plugins to override/extend views without conflicts.

### 6.13 Business Logic / Orchestrator Classes

A `Service` (§5) owns one entity/domain and its data-access rules. A **Business Logic** class is different: it exists only to coordinate *multiple* services to execute one workflow (e.g. impersonation, return-request status transitions) that doesn't belong to any single entity's service.

**When to use one:**
- The workflow genuinely calls into 3+ services/domains to do one job.
- Skip it if the "logic" is a single call or a trivial condition — put that directly in the controller, or on the one service it actually belongs to. A class that only wraps one dependency and one `if` is not a business logic class.

**Rules:**
- Lives in the plugin's `BusinessLogic/` folder.
- **Must have an interface**, following the same `IFooLogic` / `FooLogic` convention as every other service — no exceptions. This is what makes it mockable under NUnit + Moq (§4), consistent with the rest of the codebase.
- Registered in `NopStartup.cs` as `services.AddScoped<IFooLogic, FooLogic>()`, same as any other service — never as a bare concrete type.
- Consumed only by **controllers** (or other services), never `@inject`ed directly into a Razor view. Views must go through the Controller → ModelFactory → Model → View pattern (§6.5); injecting business logic into a view bypasses that pattern.

Existing examples in CW.Core: `ICustomersLogic`/`CustomersLogic` (impersonation), `IExchangesLogic`/`ExchangesLogic` (return/exchange workflow orchestration).

---

## 7. Code Conventions

- **File-scoped namespaces**: `namespace Nop.Plugin.CW.MyPlugin.Services;` (no braces).
- **Async-first**: every public service method returns `Task` / `Task<T>` and uses the `Async` suffix.
- **Constructor injection only**; fields are `protected readonly` and `_camelCase`.
- **`ImplicitUsings` enabled** — do not add `using System;`, `using System.Collections.Generic;`, etc. unless needed for disambiguation.
- **`record` for view models**: `public record MyModel : BaseNopEntityModel`.
- Naming: `IFooService` / `FooService`, `IFooModelFactory` / `FooModelFactory`, `FooOverrideService` for overrides.
- `#region` sectioning for classes with more than ~3 members, in this order: **`Fields` → `Ctor` → `Utilities` → public method region(s)**. **Visibility is the primary axis, topic the secondary one:** all protected/private helpers stay inside the single `Utilities` region, and when that region grows large it is subdivided with **nested** regions by topic rather than promoted to top level (see `ExchangesLogic`). Public methods are grouped into named top-level regions by domain slice (see `ExchangeService`). Avoid a `Miscellaneous` region — if members have no obvious home, they usually belong in an existing region. Protected/private helpers come *before* the methods that use them (see `ExchangeService`, `ReturnCaseService`). Larger services may split the public methods into several named regions (`#region Cases`, `#region Lines`, ...) rather than one `#region Methods`.
- Full XML doc comments on public APIs (matches nop core style).
- Nullable reference types: enabled in `CW.Common`; new CW plugins should opt in (`<Nullable>enable</Nullable>`).
- **Locale resource keys** follow the pattern `Plugins.CW.{PluginName}.Fields.{PropertyName}`. Exception:
  `ExternalAuth.SSO` keeps `Plugins.ExternalAuth.SSO.*` (§2); its strings live in `Infrastructure/SsoLocaleResources.cs`.
- **Table names** follow a per-plugin prefix pattern `{PLUGIN_ABBREVIATION}_{EntityName}` (e.g., `CORE_EX_CreditNote`, `CORE_SUB_Subsidy` for `CW.Core`). This changed during the TIG→CW rename: previously every plugin shared one company-wide `TIG_` prefix; now each plugin uses its own short abbreviation (`CW.Core` uses `CORE_`) so table names won't need to change again if the company brand changes. Plugins not yet renamed (e.g. `TIG.NAB.XMLPayment`) still use the old `TIG_{ABBREVIATION}_` convention until they're migrated. Now-renamed `CW.Freight` uses `CW_FR_*` (e.g. `CW_FR_ShippingMethod`), migrated in place from the old `TIG_FR_*` names by a rename-table migration. `ExternalAuth.SSO` uses `SSO_*`
  (`SSO_AuthEvent`, `SSO_GroupRoleMapping`).
- **No Bootstrap 3** in views — use Bootstrap 4.6 (`card`, `float-left`, `float-right`, `col-md-*`).

---

## 8. Data Layer

### 8.1 Repository

```csharp
public class MyService : IMyService
{
    protected readonly IRepository<MyEntity> _repository;

    public MyService(IRepository<MyEntity> repository) => _repository = repository;

    public async Task<IList<MyEntity>> GetAllAsync()
        => await _repository.GetAllAsync(q => q.OrderBy(e => e.DisplayOrder));
}
```

### 8.2 Entity Builder

```csharp
public class MyEntityBuilder : NopEntityBuilder<MyEntity>
{
    public override void MapEntity(CreateTableExpressionBuilder table)
    {
        table
            .WithColumn(nameof(MyEntity.Name)).AsString(400).NotNullable()
            .WithColumn(nameof(MyEntity.DisplayOrder)).AsInt32().NotNullable();
    }
}
```

### 8.3 Migrations

**Schema migration (initial install)** — use `MigrationProcessType.Installation`:

```csharp
[NopMigration(MigrationVersions.InitialSchema, "CW.MyPlugin base schema", MigrationProcessType.Installation)]
public class SchemaMigration : AutoReversingMigration
{
    public override void Up()
    {
        // nop 5.00: CreateTableIfNotExists replaces Create.TableFor<T>() and checks existence itself.
        this.CreateTableIfNotExists<MyEntity>();
    }
}
```

**Update migration (add column)** — use `MigrationProcessType.NoMatter`. **Use `Migration` as base class, NOT `MigrationBase`** (which does not exist in this codebase).

```csharp
[NopMigration(MigrationVersions.AddSomeColumn, "CW.MyPlugin add SomeColumn", MigrationProcessType.NoMatter)]
public class AddSomeColumnMigration : Migration
{
    public override void Up()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(MyEntity));
        if (!Schema.Table(table).Column(nameof(MyEntity.SomeColumn)).Exists())
            Alter.Table(table).AddColumn(nameof(MyEntity.SomeColumn))
                 .AsBoolean().NotNullable().WithDefaultValue(false);
    }

    public override void Down()
    {
        var table = NameCompatibilityManager.GetTableName(typeof(MyEntity));
        if (Schema.Table(table).Column(nameof(MyEntity.SomeColumn)).Exists())
            Delete.Column(nameof(MyEntity.SomeColumn)).FromTable(table);
    }
}
```

### 8.4 Migration Version Constants

Centralise migration timestamps per plugin in `Data/MigrationVersions.cs`:

```csharp
public static class MigrationVersions
{
    public const string InitialSchema = "2026/01/01 00:00:00";
    public const string AddSomeColumn = "2026/06/01 00:00:00";
}
```

Accepted formats: `yyyy/MM/dd HH:mm:ss`, `yyyy-MM-dd HH:mm:ss`, `yyyy.MM.dd HH:mm:ss`.

### 8.5 Table Name Mapping

```csharp
public class MyPluginNameCompatibility : INameCompatibility
{
    public Dictionary<Type, string> TableNames => new()
    {
        { typeof(MyEntity), "XX_MyEntity" }, // XX = this plugin's own abbreviation, e.g. CORE_ for CW.Core
    };
    public Dictionary<(Type, string), string> ColumnName => new();
}
```

### 8.6 Plugin Lifecycle

```csharp
public class MyPlugin : BasePlugin
{
    public override async Task InstallAsync()
    {
        await _settingService.SaveSettingAsync(new MySettings { /* defaults */ });
        await _localizationService.AddOrUpdateLocaleResourceAsync(new Dictionary<string, string>
        {
            ["Plugins.CW.MyPlugin.Fields.SomeSetting"] = "Some setting",
        });
        await base.InstallAsync();
    }

    public override async Task UninstallAsync()
    {
        await _settingService.DeleteSettingAsync<MySettings>();
        await _localizationService.DeleteLocaleResourcesAsync("Plugins.CW.MyPlugin");
        await base.UninstallAsync();
    }
}
```

---

## 9. Admin UI Conventions

### 9.1 Controllers — Base Class Choice

Two valid base classes; choice depends on the plugin's relationship to CW.Core:

- **`BaseAdminController`** — for CW.Core itself, and for plugins that have a reference to CW.Core. Already applies `[Area(AreaNames.ADMIN)]`, `[AuthorizeAdmin]`, `[AutoValidateAntiforgeryToken]`, `[ValidateIpAddress]`, `[ValidateVendor]`, `[SaveSelectedTab]`, `[NotNullValidationMessage]`. **Do not re-apply these attributes** when inheriting it.
- **`BasePluginController`** — for standalone plugins (no reference to CW.Core). Each controller must declare the admin attributes explicitly:

```csharp
[AuthorizeAdmin]
[Area(AreaNames.ADMIN)]
[AutoValidateAntiforgeryToken]
public class MyController : BasePluginController
{
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Configure() { ... }

    [HttpPost]
    [CheckPermission(StandardPermission.Configuration.MANAGE_PLUGINS)]
    public async Task<IActionResult> Configure(MyModel model) { ... }
}
```

A standalone plugin with several admin controllers puts these attributes on **one abstract base** of its own instead
of repeating them — `ExternalAuth.SSO`'s `SsoAdminControllerBase` (`[AuthorizeAdmin]`, `[Area]`,
`[AutoValidateAntiforgeryToken]`). `[CheckPermission]` cannot go there: it is method-only, so it stays on each action.

Other rules:
- **Permission checks via `[CheckPermission(...)]` attribute on actions.** Do not call `_permissionService.AuthorizeAsync(...)` inline in new code. (Some existing plugins use the inline pattern — leave them alone.)
- Use `_notificationService.SuccessNotification(...)` / `_notificationService.ErrorNotification(...)` for user feedback.
- Return explicit view paths: `View("~/Plugins/CW.<Feature>/Views/Configure.cshtml", model)`.

### 9.2 Models

```csharp
// List/search page model
public record ConfigurationModel : BaseSearchModel
{
    [NopResourceDisplayName("Plugins.CW.MyPlugin.Fields.SomeSetting")]
    public bool SomeSetting { get; set; }
}

// Entity edit model
public record MyEntityModel : BaseNopEntityModel
{
    [NopResourceDisplayName("Plugins.CW.MyPlugin.Fields.Name")]
    public string Name { get; set; }
}

// Grid list model
public record MyEntityListModel : BasePagedListModel<MyEntityModel> { }
```

Call `model.SetGridPageSize()` in the controller before passing a `BaseSearchModel` to a view.

### 9.3 Grid (DataTables)

Use `DataTablesModel` + `PrepareToGridAsync` — never Kendo UI:

```csharp
[HttpPost]
public async Task<IActionResult> EntityList(ConfigurationModel searchModel)
{
    var entities = await _service.GetAllAsync();
    var paged = entities.ToPagedList(searchModel);

    var gridModel = await new MyEntityListModel().PrepareToGridAsync(searchModel, paged, () =>
    {
        return paged.SelectAwait(async e => new MyEntityModel { Id = e.Id, Name = e.Name });
    });

    return Json(gridModel);
}
```

```cshtml
@await Html.PartialAsync("Table", new DataTablesModel
{
    Name = "my-grid",
    UrlRead = new DataUrl("EntityList", "MyController", null),
    Length = Model.PageSize,
    LengthMenu = Model.AvailablePageSizes,
    ColumnCollection = new List<ColumnProperty>
    {
        new ColumnProperty(nameof(MyEntityModel.Name)) { Title = T("...").Text },
        new ColumnProperty(nameof(MyEntityModel.Id))
        {
            Title = T("Admin.Common.Edit").Text,
            ClassName = NopColumnClassDefaults.Button,
            Render = new RenderButtonEdit(new DataUrl("Edit", "MyController", null))
        }
    }
})
```

### 9.4 View Layout and Markup

- Configure pages: `Layout = "_ConfigurePlugin";`
- Edit/Create/List pages: `Layout = "_AdminLayout";`
- Use **Bootstrap 4.6** classes consistent with AdminLTE 3.2: `card`, `card-body`, `cards-group`, `float-left`, `float-right`, `col-md-*`.
- Do **not** use Bootstrap 3 classes: `panel`, `pull-left`, `pull-right`.
- Use NopCommerce tag helpers: `<nop-label>`, `<nop-editor>`, `<nop-select>`, `<nop-textarea>`, `<nop-delete-confirmation>`.
- Set the active menu item: `NopHtml.SetActiveMenuItemSystemName("...")`.

Standard view skeleton:

```cshtml
@model MyPlugin.Models.MyEntityModel
@{
    Layout = "_AdminLayout";
    NopHtml.SetActiveMenuItemSystemName("...");
}

<form asp-controller="MyController" asp-action="Edit" method="post">
    <div class="content-header clearfix">
        <h1 class="float-left">
            Edit — @Model.Name
            <small><a asp-controller="MyController" asp-action="Configure">back to list</a></small>
        </h1>
        <div class="float-right">
            <button type="submit" name="save" class="btn btn-primary">
                <i class="far fa-save"></i> @T("Admin.Common.Save")
            </button>
            <button type="button" id="entity-delete" class="btn btn-danger">
                <i class="fas fa-trash-alt"></i> @T("Admin.Common.Delete")
            </button>
        </div>
    </div>

    <section class="content">
        <div class="container-fluid">
            <div class="form-horizontal">
                @await Html.PartialAsync("~/Plugins/CW.MyPlugin/Views/_CreateOrUpdate.cshtml", Model)
            </div>
        </div>
    </section>
</form>

<nop-delete-confirmation asp-model-id="@Model.Id"
                         asp-button-id="entity-delete"
                         asp-action="@Url.Action("Delete", "MyController")" />
```

### 9.5 `_ViewImports.cshtml`

```cshtml
@inherits Nop.Web.Framework.Mvc.Razor.NopRazorPage<TModel>
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@addTagHelper *, Nop.Web.Framework

@using Microsoft.AspNetCore.Mvc.ViewFeatures
@using Nop.Web.Extensions
@using Nop.Web.Framework
@using Nop.Web.Framework.Extensions
@using Nop.Web.Framework.UI
@using System.Text.Encodings.Web
```

### 9.6 View Location Registration

```csharp
services.Configure<RazorViewEngineOptions>(options =>
{
    options.ViewLocationExpanders.Add(new ViewLocationExpander());
});
```

```csharp
public class ViewLocationExpander : IViewLocationExpander
{
    private const string PLUGIN_FOLDER = "CW.MyPlugin";

    public void PopulateValues(ViewLocationExpanderContext context) { }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        var pluginLocations = new[]
        {
            $"~/Plugins/{PLUGIN_FOLDER}/Views/{{0}}.cshtml",
            $"~/Plugins/{PLUGIN_FOLDER}/Views/{{1}}/{{0}}.cshtml",
        };
        return pluginLocations.Concat(viewLocations);
    }
}
```

### 9.7 Admin Menu Integration

Secondary plugins attach their menu items via `AdminMenuCreatedEvent` — no compile-time dependency on CW.Core required:

```csharp
public class EventConsumer : IConsumer<AdminMenuCreatedEvent>
{
    public async Task HandleEventAsync(AdminMenuCreatedEvent eventMessage)
    {
        var coreMenu = eventMessage.RootMenuItem.GetItemBySystemName("CW.Core");
        if (coreMenu is null)
            return;

        var myMenu = new AdminMenuItem
        {
            SystemName = "CW.MyPlugin",
            Title = await _localizationService.GetResourceAsync("CW.Admin.Menu.MyPlugin"),
            IconClass = "fa fa-...",
            ChildNodes = new List<AdminMenuItem> { /* ... */ }
        };

        coreMenu.InsertAfter("CW.Core.BranchesAdminMenu", myMenu);
    }
}
```

### 9.8 Admin UI Libraries Reference

**CSS / UI**

| Library | Source | Purpose |
|---|---|---|
| AdminLTE 3.2 | `lib_npm/admin-lte/` | Admin template/layout (sidebar, navbar, cards) |
| Bootstrap 4.6 | `lib_npm/bootstrap/` | Grid, components |
| FontAwesome | `lib_npm/@fortawesome/fontawesome-free/` | Icons (`fa fa-*`) |
| jQuery UI | `lib_npm/jquery-ui-dist/` | Datepicker, dialogs, sortable |
| DataTables + BS4 | `lib_npm/datatables.net-bs4/` | All admin grids |
| Select2 | `lib_npm/admin-lte/plugins/select2/` | Standard NOP dropdowns |
| Bootstrap TouchSpin | `lib_npm/bootstrap-touchspin/` | Numeric spinners |
| OverlayScrollbars | `lib_npm/overlayscrollbars/` | Custom scrollbars |

**JavaScript**

| Library | Purpose |
|---|---|
| jQuery 3.7 | Core |
| jQuery Migrate | Backward compat |
| jQuery Validation + Unobtrusive | Form validation |
| Globalize + CLDR | Number/date/currency formatting per locale |
| Moment.js | Date formatting in DataTables |
| Typeahead.js | Autocomplete |
| DataTables Buttons | Export buttons |

**NOP Admin JS Globals**: `admin.common.js`, `admin.navigation.js`, `admin.search.js`, `rootAppPath`, `culture`.

**CW-Specific**: `lib/admin.store.navigation.js` — CW multi-store navigation; `lib/tom-select/tom-select.complete.min.js` — **Tom Select** (used in CW views instead of Select2 for enhanced dropdowns).

### Where plugin assets live

nop 5.00 plugins keep their own assets under `Content/` — see `Widgets.Swiper` (`Content/js/`),
`Integration.Odoo` (`Content/js/`), `Payments.NABGateway` (`Content/`). There is **no `Scripts/`
convention** in modern nop plugins; that was the 4.1 era.

For CW plugins:

| Kind | Location |
|---|---|
| Third-party / vendored libraries | `lib/` (e.g. `lib/tom-select/`) |
| **CW's own CSS and JS — public store** | **`Content/{Feature}/`** — keep a feature's stylesheet and script together (e.g. `Content/Exchanges/exchanges-wizard.css` + `exchanges-gather.js`) |
| **CW's own CSS and JS — admin area** | **`Content/Admin/{Feature}/`** (e.g. `Content/Admin/Exchanges/return-request.css`, `Content/Admin/MultiStore/admin.store.navigation.js`) |

Do not add CW-authored scripts to `lib/`. Some existing ones (`public.packname.checkout.js`,
`subsidy-catalog.js`) predate this rule and should move to their feature folder when next touched.

**Admin assets do not go under `Areas/Admin/`.** `Areas/` is an MVC routing construct — it governs
controller routing (`[Area(AreaNames.ADMIN)]`) and Razor view discovery, and nothing else. Static files
are served by the static-file middleware keyed on URL path, so areas play no part: the path passed to
`AddCssFileParts` is a literal URL, and wherever the file physically sits *is* the URL. Filing admin CSS
under `Areas/Admin/` would produce browser-facing URLs advertising a server-side concept
(`~/Plugins/CW.Core/Areas/Admin/Content/...`) and would break the rule that everything under
`Areas/Admin/Views/` is Razor.

The dividing line: **`Areas/Admin/` is for what the framework routes or compiles; `Content/` is for what
the browser fetches.** Audience is a folder *inside* `Content/`, never outside it. Note the asymmetry —
public assets are not under `Content/Public/`, because public is the majority case and admin is the
marked exception. This is deliberate; do not "fix" it.

**Every CSS/JS file needs its own csproj entry or it will not deploy** — there is no glob, so a missing
entry is a silent 404 with no build warning:

```xml
	  <None Update="Content\Admin\Exchanges\return-request.css">
	    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
	  </None>
```

#### Where to register an asset

Register it as **narrowly as the markup that needs it**. A stylesheet registered globally is downloaded
on every page of both storefront and admin, and becomes a mystery to whoever later greps for why it
loads.

| Situation | Register in | Why |
|---|---|---|
| A view or partial rendered **with a layout** | The view/partial itself, in its `@{ }` block | Razor renders the whole body — partials and view components included — *before* the layout executes, so `NopHtml` registrations from anywhere in the body still reach `<head>`, and only when that view actually runs. See `Views/PackNameCheckout/_OrderSummary.cshtml`. |
| A partial returned by an AJAX action (`return PartialView(...)`) | The **host page** that loads it | An AJAX fragment has no layout, so a registration inside it goes nowhere. |
| A core view CW does not own, or an asset gated on a feature flag / route | `PageRenderingEvent` consumer in `Services/EventConsumer.cs` | Last resort. Always gate it — check `AreaNames.ADMIN` and `eventMessage.GetRouteName()` so an admin-only stylesheet is not pushed onto every storefront page. |
| Anything | **Never an inline `<style>` or `<script>` block in a partial** | Re-emitted on every render, invisible to bundling, and impossible to cache. |

**Adding scripts/CSS in admin views:**
```csharp
@{
    NopHtml.AddScriptParts(ResourceLocation.Footer, Url.Content("~/path/to/script.js"));
    NopHtml.AddCssFileParts("~/path/to/style.css");
}
```

**Adding scripts in public store plugin views:**
```csharp
@{
    NopHtml.AppendScriptParts(ResourceLocation.Footer, Url.Content("~/Plugins/CW.Core/path/to/script.js"));
    NopHtml.AppendCssFileParts("~/Plugins/CW.Core/path/to/style.css");
}
```

#### `Add*` vs `Append*` — the naming is misleading

**`Append` prepends.** In `NopHtmlHelper`, `AddCssFileParts` does `_cssParts.Add(...)` (end of the list)
while `AppendCssFileParts` does `_cssParts.Insert(0, ...)` (start of the list). `AddScriptParts` /
`AppendScriptParts` behave the same way.

The choice is about **cascade order only** — it does not depend on whether the caller is a view, a partial
or a view component:

| Use | When |
|---|---|
| `Add*` | The file must come **after** what is already registered — i.e. it needs to win ties on equal specificity. Default for admin, where CW CSS is layered on top of AdminLTE. |
| `Append*` | The file is a base the theme is allowed to override. Default for the storefront, so a theme's stylesheet still wins. |

Follow the area default above unless a specificity fight forces the other. If you do deviate, say why in a
comment — a lone `Add*` on the storefront looks like a typo otherwise.

### 9.9 DataTables Grid (JS Pattern)

```javascript
var dataTable = $('#grid-name').DataTable({
    serverSide: true,
    processing: true,
    ajax: {
        url: '@Url.Action("List", "Controller")',
        type: 'POST',
        data: addAntiForgeryToken
    },
    columns: [ ... ]
});
```

NOP provides `addAntiForgeryToken` globally.

### 9.10 Common Admin UI Patterns

**Tabs:**
```html
<ul class="nav nav-tabs">
    <li class="nav-item"><a class="nav-link active" data-toggle="tab" href="#tab-info">Info</a></li>
</ul>
<div class="tab-content">
    <div class="tab-pane active" id="tab-info">...</div>
</div>
```

**Cards:**
```html
<div class="card">
    <div class="card-body">...</div>
</div>
```

**Tom Select (CW enhanced dropdowns):**
```javascript
new TomSelect('#element-id', {
    valueField: 'id',
    labelField: 'text',
    searchField: 'text',
    load: function(query, callback) {
        fetch('/Branch/SearchBranches?storeId=X&term=' + query)
            .then(r => r.json())
            .then(data => callback(data.results));
    }
});
```

**NOP Standard Popup:**
```javascript
openPopup('#popup-id');
$('#popup-id').dialog('close');
```

### 9.11 Bundling and Minification — Three Traps

Admin JS and CSS are bundled and minified by **WebOptimizer**
(`App_Data/appsettings.json` → `WebOptimizer.EnableJavaScriptBundling` / `EnableCssBundling`, wired at
`ServiceCollectionExtensions.cs:426`). Each of the following produced a silent failure that looked
like an application bug.

**1. A `.js` / `.css` edit needs `Presentation/Nop.Web/wwwroot/bundles` emptied.**
`EnableDiskCache: true` points `CacheDirectory` at that folder, so the minified bundle survives a
rebuild **and** a full restart. `.cshtml` and `.cs` are fine on hot reload — which is why view
changes appear instantly in the same session where JS changes appear to do nothing.
Diagnosis, one line in the browser console: compare the module's exports against the source file. If
the file has the export and the console does not, it is the cache. Do not debug the code.

**2. Use `var`, not `let`, for a closure variable read by a hoisted inner function.**
NUglify inverts an early-`return` guard into a wrapping `if` block and moves `let` declarations
inside it, while `function` declarations stay hoisted at function scope — so the inner function binds
to an unrelated outer variable. Symptom: a silent no-op with no console error, reproducible **only**
in the minified bundle. `var` is function-scoped whatever the minifier does with the declaration.

**3. A minified bundle is not your source.** When live behaviour contradicts the file, read the
function back (`SomeModule.someFn.toString()`) before theorising. That also catches a debugging probe
left in the page from an earlier session.

See lessons #73, #74, #77.

### 9.12 Delegated Handlers on Long-Lived Containers

A partial that binds **delegated** handlers to an element it does not own must `.off('.ns')` first
and namespace every handler:

```javascript
var $panel = $('#some-panel').off('.mine');
$panel.on('click.mine', '.btn-x', ...);
```

A container created once and then refilled with `$panel.html(...)` outlives every render of the
partial inside it, so handler sets accumulate — one confirm dialog becomes four. Handlers bound to
elements **inside** the replaced markup are safe: those are new elements each render. Say which case
applies in a comment, because the two look identical.

`e.stopPropagation()` does not fix this and hides the diagnosis — duplicates sit on the **same**
element, and `stopPropagation` stops bubbling to ancestors, not siblings on the same node.
(MultiStore: `#pnlists-panel`, `#cc-panel`, `#branch-addresses-panel` — see
`CWPlans/MultiStore_Management_Reference.md` §8b. Lesson #75.)

### 9.13 FluentValidation Runs Before the Controller Body

`BaseNopValidator<T>` runs at **model binding**, so a controller cannot derive a value that its
validator requires. A rule like `RuleFor(x => x.StoreId).NotEqual(0)` rejects a post that omits
`StoreId` before the action can take the store from the posted branch.

This is invisible when the view has no span for that property and no validation summary: the request
returns **200** with the validation-failure partial, and the UI reports nothing. If an AJAX save
"succeeds" and nothing changes, check the validator before the controller.


### 9.14 No Razor Inside a `<script>` — Use a Config Div

Razor **HTML-encodes** `@T(...)` and `@Model` output on its way into a JavaScript string literal. An
apostrophe arrives as `&#x27;` and renders as mojibake; a quote terminates the literal. Attribute
values encode and decode symmetrically, so the same text survives intact.

Server values therefore reach a script through a hidden div, never through interpolation:

```html
<div class="thing-config"
     data-store-id="@Model.StoreId"
     data-delete-url="@Url.Action("DeleteThing", "Controller")"
     style="display: none;"></div>
```

```javascript
var $cfg = $panel.find('.thing-config');
var storeId = $cfg.data('storeId');
```

The same rule retires hard-coded action URLs: `@Url.Action(...)` on the div, never
`'/Admin/Area/Action'` in the script. Renaming an action then breaks the build instead of breaking a
button silently.

Two consequences worth knowing. A `<script>` block inside a `.cshtml` is **never bundled**, so it is
never minified and never checked; moving the code out is what subjects it to the minifier — see
§9.11. And localised text must come from a resource bundle serialised once (`Html.Raw` of a
dictionary, read back through a `res(key, fallback)` helper), not from `@T()` at each call site.

**A partial rendered on a page that does not load the shared module** (e.g. a view component that also
appears on nop's own customer page) cannot use that module's render namespace or resource bundle. Give it
its own namespace, declared once however many times the view renders —
`window.CW = window.CW || {}; CW.Thing = CW.Thing || (function () { ... })();` — with its labels on the
config div, and delegate its handlers from its own container with `.off('.ns')` first.

### 9.15 One Script Block Per Partial, and It Is an IIFE

A partial gets at most one `<script>`, and its whole body is `(function () { ... })()`. Nothing it
defines is reachable from outside.

A function that must be global to serve an `onclick="doThing()"` three hundred lines above is the
smell this removes: bind the handler instead, delegated from the partial's own container. Grid
buttons carry `data-id` and nothing else, with the render function living in a shared namespace —
`RenderCustom` emits its argument as a raw JS expression, so `new RenderCustom("App.render.edit")`
works. Any other field the handler needs comes from
`$(grid).DataTable().row($(this).closest('tr')).data()`.

Interpolating a record's own text into an `onclick` is both a quoting bug and a stored-value
injection vector: a name containing `"` closes the attribute and spills the rest of the button into
the DOM as attributes.

Errors go to `display_nop_error({ error: ... })`, which also handles a ModelState error array, not to
`alert()`.

### 9.16 A Ready Callback Never Runs Before the Script That Follows It

nop's `Table` partial initialises its DataTable inside `$(function () { … })`. jQuery 3 runs ready callbacks
**asynchronously** even when the document is already ready, so in HTML injected with `.html()` the inline script that
follows the table runs *before* the table exists as a DataTable. Calling `updateTable('#grid')` (or any
`$('#grid').DataTable()`) from that script creates an empty DataTable and the real initialisation is then refused
("Cannot reinitialise DataTable"). Touch a grid only from a handler or a callback; a load-time toggle guards the call
with `$.fn.dataTable.isDataTable(sel)`. Found on `_Branches.PackNameListDetail.cshtml` (2026-10-08).

---

## 10. Override Strategy & Cross-Plugin Coupling

When functionality in nop core needs different behaviour for CW, **do not edit the core service**. The override goes in a plugin.

### 10.1 Where the Override Lives

**Option A — inside the consuming plugin** (preferred when feature-local):
- The override is conceptually part of one feature and no other CW plugin needs it.
- Implement in `Plugins/Nop.Plugin.CW.<Feature>/Services/<Name>OverrideService.cs`.
- Register in that plugin's `NopStartup` with `Order` that runs after both nop default and `CW.Core`.

**Option B — inside `CW.Core`** (preferred when shared across plugins):
- The behaviour is foundational or two or more CW plugins need it.
- Define the interface in `CW.Core`, implement in `CW.Core/Services/`, register in `CW.Core/Infrastructure/NopStartup.cs`.

Decision rule: *"If a brand-new CW plugin were added tomorrow, would it want this override automatically?"* Yes → Option B. No → Option A. Unsure → start with A; promoting later is safe, demoting is disruptive.

Existing Option B examples: `BranchService`, `MultiStoreService`, `CustomerOverrideService`, `AclOverrideService`, `ShoppingCartOverrideService`, `PackNameService`, `CustomerOverrideModelFactory`.

### 10.2 Cross-Plugin Coupling Patterns

Choosing how a secondary plugin consumes a CW.Core service is an **architectural decision — always ask before implementing.**

**Pattern A — Pure runtime DI (no compile-time link):**
```csharp
var branchService = _serviceProvider.GetService(
    Type.GetType("Nop.Plugin.CW.Core.Services.Branches.IBranchService, Nop.Plugin.CW.Core"));
```
Most decoupled, but reflection-heavy and brittle to renames.

**Pattern B — Shared interface in `CW.Common`:** Contract lives in `CW.Common`. CW.Core implements it; secondary plugin consumes by interface. No reflection, no project reference between plugins. Good for stable, reusable contracts.

**Pattern C — DLL reference with `<Private>false</Private>`:** Secondary plugin references the built CW.Core DLL. No `<ProjectReference>`, no build-order coupling.
```xml
<Reference Include="Nop.Plugin.CW.Core">
  <HintPath>..\..\Presentation\Nop.Web\Plugins\CW.Core\Nop.Plugin.CW.Core.dll</HintPath>
  <Private>false</Private>
  <ExcludeAssets>runtime</ExcludeAssets>
</Reference>
```

**Pattern D — NopCommerce events:** Use `IEventPublisher` + `IConsumer<TEvent>` for "X happened" semantics rather than "give me data."

**Forbidden:** A `<ProjectReference>` from a secondary CW **peer feature plugin** to `Nop.Plugin.CW.Core.csproj` (or another peer plugin). This couples build order and tangles the plugin loader.

**Exception — framework packages (added 2026-07-24):** a plugin that is a *dependent extension* of a CW **framework package** (e.g. a per-theme plugin `Nop.Plugin.CW.Theme.<Name>` that inherits base classes from `Nop.Plugin.CW.ThemeCore`) **may** use a `<ProjectReference>` to that framework's `.csproj`, provided it sets `<Private>false</Private>` + `<ExcludeAssets>runtime</ExcludeAssets>`. Rationale: the two objections above don't apply — (a) build-order coupling is *intended* (the extension genuinely depends on the framework), and (b) the flags keep the framework DLL out of the extension's output, so there's no duplicate assembly / loader tangle, while the compile-time reference (base-class inheritance) and an automatic build-order edge are retained. This applies **only** to a declared framework→extension relationship, not to peer plugins. See the ThemeCore example in `CWPlans/CW_Theme_Build_Plans.md` (Phase 2c).

### 10.3 Copied nop method bodies — **re-diff these on every nopCommerce upgrade**

> **Applies once CW.Core is ported.** The table lists the 4.9 copies; each one is re-diffed against the 5.00 base
> method as part of that port. The SSO plugin copies no nop method bodies.

Overriding a nop service normally means calling `base` and adjusting. In three places that was impossible
because nop builds the thing **inline with no hook**, so the method body was copied and one part changed.
A nop upgrade can change the copied body underneath us with **no compile error and no test failure** —
the override simply goes on doing what the old version did.

**On every nopCommerce upgrade, diff the base method against our copy and re-apply the change:**

| Our file | Copied from | What we changed |
|---|---|---|
| `CW.Core/Services/Shipping/ShipmentOverrideService.GetAllShipmentsAsync` | `Nop.Services/Shipping/ShipmentService.cs` (~113 lines) | the four location filters join `vw_CORE_PN_ShipmentAddress` instead of `o.ShippingAddressId` |
| `CW.Core/Services/Common/PdfOverrideService.PrintOrderToPdfAsync` | `Nop.Services/Common/PdfService.cs` (~60 lines) | builds a `CWInvoiceDocument`, and sets `ShippingAddress` + `ShippingDestinations` |
| `CW.Core/Services/Common/PdfOverrideService.PrintPackagingSlipToPdfAsync` | `Nop.Services/Common/PdfService.cs` | builds a `CWShipmentDocument` (prints each product's attribute lines — and so the For line — under its name, which nop's `ShipmentDocument` does not); `Address` comes from the shipment's destination via a stand-in `Order` |
| `CW.Core/Services/Messages/MessageTokenProviderOverride.WriteTotalsAsync` | `Nop.Services/Messages/MessageTokenProvider.cs` (~200 lines) | for a **packname** order with a settlement row (`CORE_PN_OrderSettlement`): nop's subtotal-discount and payment-fee rows are skipped; after the tax rows `WriteSettlementTotalsAsync` writes "Total before subsidy and credit", subsidy for goods, subsidy for shipping and store credit, then `AppendTotalsRow` writes the subtotal discount and the fee; the order discount and total rows take the CW labels ("Order discount", "Payable balance"). A **plain** order keeps nop's layout, with a store-credit row (when it has a settlement row) after the reward points. Before the total, `WriteExchangeFeesTotalsAsync` (fees come after every deduction) — seven places differ from nop's body |
| `CW.Core/Services/Orders/OrderReportOverrideService.ProfitReportAsync` | `Nop.Services/Orders/OrderReportService.cs` (the order filter, ~90 lines) | the filtered orders are joined to their settlement rows (`CORE_PN_OrderSettlement`) and the subsidy (goods + shipping) is added back onto nop's profit; store credit is not (it is customer money the store already holds) |

Everything else in the CW plugins overrides by calling `base` and post-processing, or by overriding a
`protected virtual` hook, and needs no such check. (The last two rows were found on 2026-10-01 while opening
phase 6; both had been in place since the exchanges rework.) Background and rationale:
`CWPlans/Exchanges_Rebuild_Record.md` §4

### 10.4 Themes and view resolution — **read this before enabling a theme**

> **Applies once CW.Core is ported** — the collision set below is CW.Core's 4.9 storefront views. The SSO plugin
> ships no storefront view that a theme could shadow except its login button (`PublicInfo.cshtml`).

`CW.Core/Infrastructure/ViewLocationExpander.cs` **prepends** the plugin's view paths to the Razor search
list, ahead of everything — including any theme's `~/Themes/{theme}/Views/…`.

**Consequence, and it is silent.** Where a theme customises a view the plugin also ships, the plugin wins
and the theme's version **never renders**. No error, no warning, no log line. Today only `DefaultClean` is
present so nothing collides; the moment a real theme is enabled this becomes live.

**The collision set — the storefront views CW.Core forks from nop:**

| View | Why the plugin forks it |
|---|---|
| `Views/Order/Details.cshtml` | packname sections, the shipments **Delivered to** column (phase 9) |
| `Views/ReturnRequest/ReturnRequest.cshtml` | the returns wizard entry |
| `Views/ReturnRequest/CustomerReturnRequests.cshtml` | the customer's returns list |
| `Views/Shared/Components/OrderTotals/Default.cshtml` | subsidy and credit-note lines in the totals |
| `Views/Shared/Components/FlyoutShoppingCart/Default.cshtml` | packname-aware flyout cart |

The last two are the ones a theme is most likely to have restyled.

**When enabling or changing a theme, do this:**

1. Confirm which theme is active per store and per environment —
   `SELECT Name, Value, StoreId FROM Setting WHERE Name LIKE 'storeinformationsettings.defaultstoretheme'`.
   Also check `storeinformationsettings.allowcustomertoselecttheme`: if it is on, customers can land on a
   theme nobody tested and the answer is not single-valued.
2. For each active theme, check whether any of the five files above exists under `~/Themes/{theme}/Views/`.
3. Every hit is a decision, not a bug: either the plugin's fork absorbs that theme's styling, or that view
   moves out of the plugin. Do not "fix" it by reordering the expander — putting themes first would break
   every packname screen.
4. Re-run this check after any phase that forks a new storefront view. Keep the table above current; it is
   the whole point of this section.

Admin views are unaffected — themes do not apply to the admin area.

### 10.5 Porting a plugin from 4.90 to 5.00

Found porting `ExternalAuth.SSO` (2026-10-19). Check every one of these in each plugin brought across; most fail
**silently** — a clean build proves little.

| # | Change | What to do | How it fails if missed |
|---|---|---|---|
| 1 | `net9.0` → `net10.0`; `plugin.json` `SupportedVersions` `"4.90"` → `"5.00"` | Edit the `.csproj` and `plugin.json` | Plugin not loaded |
| 2 | `IWebHelper` moved from `Nop.Core` to `Nop.Services.Helpers` | `using Nop.Services.Helpers;` — in C# **and** in views (`@inject`, `_ViewImports`) | C#: build error. Views: Razor error on first request |
| 3 | AutoMapper replaced by Mapster | Rewrite profiles on `BaseMapperProfile` (§4); `AutoMapperConfiguration` → `MapperConfiguration` | Build error |
| 4 | `Create.TableFor<T>()` removed | `this.CreateTableIfNotExists<T>()` (§8.3) | Build error |
| 5 | **Plugin views compile at runtime** (`AddRazorRuntimeCompilation`) | Grep every `.cshtml` for moved or renamed types, then load each page | Build passes, page dies on first request |
| 6 | **nop admin JSON is PascalCase** (`Json(...)` from admin controllers, DataTables rows) | Read `data.Success`, not `data.success`, in plugin JS | `undefined` everywhere, no error — buttons silently do nothing |
| 7 | Host package versions changed (e.g. `Microsoft.IdentityModel.*` 8.16.0 in `Nop.Web` 5.00) | Pin plugin packages to the host's versions, and take third-party packages at a version whose floor is at or below them | Builds, then a `FileLoadException` / `MissingMethodException` at runtime |
| 8 | Plugin updates run only when the version changes | Bump `plugin.json` `Version` (`…_DATA_x.y`) for any change `UpdateAsync` or an `Update` migration must apply | Update code never runs |
| 9 | Copied nop method bodies (§10.3) | Re-diff each against the 5.00 base method | Old behaviour, no error |

After a port: build, start the site, open every admin and storefront page the plugin owns, and run its flows. Record
what the port changed in the plugin's reference doc (`References/`).

---

## 11. Project Reference Rules

| Plugin | References |
|---|---|
| `ExternalAuth.SSO` | `Nop.Web.Framework` only — it needs no `Nop.Web` type, as nop's own `ExternalAuth.*` plugins. Switch to `Nop.Web` if it ever does |
| `CW.Core` *(4.9; when ported)* | `Nop.Web`, `Nop.Web.Framework`, `CW.Common` |
| `CW.Freight` *(4.9; when ported)* | `Nop.Web` |
| Any new secondary plugin | `Nop.Web`, optionally `CW.Common`. Coupling to `CW.Core` per §10.2 — ask first. |

`Nop.Web` transitively brings in `Nop.Core`, `Nop.Data`, `Nop.Services`, and `Nop.Web.Framework` — do not add those as separate references.

`CW.Common` is a lightweight library for utilities with no plugin-specific logic (`PluginHelper`, `FormatHelper`, `HttpContextHelper`). It must remain free of domain logic.

---

## 12. Plugin Scaffold

### Folder Structure

```
Plugins/Nop.Plugin.CW.<Feature>/
├── Nop.Plugin.CW.<Feature>.csproj
├── plugin.json
├── logo.jpg
├── <Feature>Plugin.cs                           : BasePlugin
├── Infrastructure/
│   ├── NopStartup.cs                            : INopStartup (DI registration + Order)
│   ├── ViewLocationExpander.cs                  (if plugin ships Razor views)
│   └── RouteProvider.cs                         (if plugin ships routes)
├── Domain/                                      Entity classes : BaseEntity
├── Data/
│   ├── Mapping/                                 NopEntityBuilder<T> for each entity
│   ├── Migrations/                              FluentMigrator [NopMigration(...)]
│   ├── MigrationVersions.cs                     Centralised migration timestamp constants
│   └── <Plugin>NameCompatibility.cs             INameCompatibility
├── Services/                                    IXxxService + XxxService
├── Mapping/Builders/                            IOrderedMapperProfile entries
├── Validators/                                  AbstractValidator<TModel>
├── Models/
├── Areas/Admin/
│   ├── Controllers/
│   ├── Factories/
│   ├── Models/
│   └── Validators/
└── Views/
    ├── _ViewImports.cshtml
    ├── Configure.cshtml
    └── <Feature>/...
```

### `.csproj` Template

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <OutputPath>$(SolutionDir)\Presentation\Nop.Web\Plugins\CW.<Feature></OutputPath>
    <OutDir>$(OutputPath)</OutDir>
    <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>
  </PropertyGroup>
  <ItemGroup>
    <Content Include="logo.jpg"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>
    <Content Include="plugin.json"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>
    <Content Include="Views\Configure.cshtml"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="$(SolutionDir)\Presentation\Nop.Web\Nop.Web.csproj" />
    <ClearPluginAssemblies Include="$(SolutionDir)\Build\ClearPluginAssemblies.proj" />
  </ItemGroup>
  <Target Name="NopTarget" AfterTargets="Build">
    <MSBuild Projects="@(ClearPluginAssemblies)" Properties="PluginPath=$(OutDir)" Targets="NopClear" />
  </Target>
</Project>
```

### `plugin.json` Template

```json
{
  "Group": "CW",
  "FriendlyName": "CW <Feature>",
  "SystemName": "CW.<Feature>",
  "Version": "1.0",
  "SupportedVersions": [ "5.00" ],
  "Author": "Total Image Group",
  "DisplayOrder": 10,
  "FileName": "Nop.Plugin.CW.<Feature>.dll",
  "Description": "..."
}
```

### Required Wiring Checklist

- [ ] `plugin.json` present, `SupportedVersions: ["5.00"]`, `SystemName` unique
- [ ] `INopStartup` registers all services as `AddScoped` with a sensible `Order` (after `CW.Core` if depending on it)
- [ ] FluentMigrator migrations under `Data/Migrations/` with timestamps from `MigrationVersions.cs`
- [ ] `INameCompatibility` registered for every entity (`{PLUGIN_ABBREVIATION}_{EntityName}` — see §7)
- [ ] Admin controller inherits the right base (see §9.1)
- [ ] Admin forms have FluentValidation validators
- [ ] Mapster profile (`BaseMapperProfile`) if the plugin maps more than a few fields
- [ ] Plugin views reachable through `ViewLocationExpander` registered in `NopStartup`
- [ ] No edits to anything outside `Plugins/Nop.Plugin.CW.*` or `CWLibraries/CW.Common`

---

## 13. Nop.Plugin.CW.Core — Full Reference *(4.9 code — not yet in this repository)*

> **§13–§21 describe CW.Core as it stands in the 4.9 repository** (`C:\Work\TIG_Nop_4_9`). None of it exists here
> yet. They are kept so the port has its map: when a part of CW.Core lands in 5.00, check its section against the
> 5.00 code, correct it, and remove it from this banner's scope. Until then, verify anything taken from §13–§21
> against the 4.9 source before relying on it.

### 13.1 Plugin Metadata

| Field | Value |
|---|---|
| SystemName | `CW.Core` |
| Group | `CW` |
| FriendlyName | `CW Core Plugin` |
| Supported NOP Version | `4.90` |
| DLL | `Nop.Plugin.CW.Core.dll` |

### 13.2 Key Domains

| Domain | Entity | Service Interface |
|---|---|---|
| Branches | `Branch`, `BranchAddressMapping` | `IBranchService` |
| MultiStores | `Department`, `CostCenter` | `IMultiStoreService` |
| PackNames | `PackName`, `PackNameList` | `IPackNameService` |
| Customers | `CustomerBranch` | `ICustomerOverrideService` |

### 13.3 Folder Structure

```
Nop.Plugin.CW.Core/
├── Areas/Admin/
│   ├── Components/        — Admin ViewComponents (panels injected into NOP pages)
│   ├── Controllers/       — Admin controllers
│   ├── Factories/         — Admin model factories
│   ├── Models/            — Admin view models (grouped by feature)
│   ├── Validators/        — FluentValidation validators
│   └── Views/             — Admin Razor views
├── BusinessLogic/         — Orchestrator classes coordinating multiple services (see §6.13)
├── Data/
│   ├── Mapping/           — LinqToDB entity builders (NopEntityBuilder<T>)
│   ├── Migrations/        — FluentMigrator migration classes
│   ├── MigrationVersions.cs
│   └── CWNameCompatibility.cs  — Entity → table name map
├── Domain/                — Domain entities grouped by feature
├── Extensions/            — Extension methods, shared CSS
├── Factories/             — Public store model factories
├── Filters/               — Action filters
├── Helpers/               — Business helpers
├── Infrastructure/        — DI startup, mapper profiles, view expander
├── Models/                — Public store view models
├── Services/              — Service layer grouped by feature
├── Views/                 — Public store Razor views
├── lib/                   — Bundled JS libs (tom-select, admin.store.navigation.js)
├── CWCoreDefaults.cs     — Constants (route names, menu system names, SP names)
├── CWCorePlugin.cs       — Plugin lifecycle
└── RouteProvider.cs       — Custom route registrations
```

### 13.4 Service Registrations (NopStartup)

All registered as **Scoped** unless noted:

| Interface | Implementation |
|---|---|
| `ICustomerService` | `CustomerOverrideService` |
| `IShoppingCartService` | `ShoppingCartOverrideService` |
| `IAclService` | `AclOverrideService` |
| `IOrderTotalCalculationService` | `OrderTotalCalculationServiceOverride` |
| `IMessageTokenProvider` | `MessageTokenProviderOverride` |
| `IBranchService` | `BranchService` |
| `IMultiStoreService` | `MultiStoreService` |
| `IPackNameContext` | `PackNameContext` |
| `IPackNameService` | `PackNameService` |
| `ICWDatabaseService` | `CWDatabaseService` |
| `ICreditNotesService` | `CreditNotesService` |
| `IExchangeService` | `ExchangeService` |
| `ISubsidyService` | `SubsidyService` |
| `ISubsidyOrderingService` | `SubsidyOrderingService` |
| `ISubsidyCreditService` | `SubsidyCreditService` |
| `ISubsidyReportingService` | `SubsidyReportingService` |
| `IPackNameShippingMethodService` | `PackNameShippingMethodService` — the CW.Freight methods of a store, for the store and branch pickers, and the filter that keeps only offered names on every save (no reference to CW.Freight) |
| `IPackNameProvisioningService` | `PackNameProvisioningService` — a new branch's default list (`UseBranchesAsPackNameList`) and a packname for a customer an admin links to a branch (`UseStaffAsPackName`), PackName-gated; also what the two Generate buttons run. Called explicitly, not an event consumer |
| `IPackNameLandingService` | `PackNameLandingService` — where a customer lands after sign-in, packname selection or impersonation (subsidy catalogue, then bulk ordering) |
| `IPackNameWorkflowMessageService` | `PackNameWorkflowMessageService` — the welcome e-mail of a manager-created customer (`Customer.Subsidy.CustomerRegistrationImport`, nop's set-password link, `%PackName.*%` / `%Subsidy.SubsidyName%` tokens) |
| `ICustomerSubsidiesModelFactory` | `CustomerSubsidiesModelFactory` — the My Subsidies page |
| `ICWReturnRequestService` | `CWReturnRequestService` |
| `IExchangeOrderService` | `ExchangeOrderService` |
| `ICWPdfService` | `CWPdfService` |
| `IBackgroundTaskQueue` | `BackgroundTaskQueue` (Singleton) |
| `ICustomersLogic` | `CustomersLogic` |
| `IExchangesLogic` | `ExchangesLogic` |
| Admin factories | `CustomerOverrideModelFactory`, `BranchModelFactory`, `MultiStoreModelFactory`, `PackNameModelFactory`, `SubsidyModelFactory`, `CWSettingModelFactory`, `CWAddressModelFactory`, `CreditNotesModelFactory`, `ReturnRequestOverrideModelFactory` |
| Public factories | `PackNameModelFactory`, `ShoppingCartOverrideModelFactory`, `ExchangesModelFactory`, `ReturnRequestItemModelFactory`, `ReturnRequestOverrideModelFactory` |

### 13.5 Route Overrides

| Route Name | Pattern | Controller → Action |
|---|---|---|
| `CW.Plugin.Admin.Customer.List` | `admin/customer/list` | `CWCustomer.List` |
| `CW.Plugin.Admin.Customer.Create` | `admin/customer/create` | `CWCustomer.Create` |
| `CW.Plugin.Admin.Customer.Edit` | `admin/customer/edit/{id}` | `CWCustomer.Edit` |
| `CW.Plugin.Admin.MultiStore.Edit` | `admin/multistore/edit/{id}` | `MultiStore.Edit` |
| `Plugin.CW.Core.CreditsForMe` | `Credits/CreditsForMe` | `CreditNotes.CreditsForMe` |
| `Plugin.CW.Core.CustomerSubsidies` | `customer/subsidies` | `CustomerSubsidies.Index` (My Subsidies, follows the selected packname) |
| `Plugin.CW.Core.SubsidyCatalog` | `SubsidyCatalog` | `SubsidyCatalog.Index` — the landing route name (`CWCoreDefaults.SubsidyCatalogRouteName`) |
| `PackNameCheckoutCompleted` | `packname/checkout/completed/{orderId:int?}` | `PackNameCheckout.Completed` — nop's `checkout/completed` re-routes here for a packname order (`PackNameCheckoutRedirectFilter`) |

### 13.6 Admin Controllers

| Controller | Key Responsibilities |
|---|---|
| `BranchController` | The standalone *CW Admin › Branches* page (`Branch/Index` → `List`): branch list, create / edit / delete, branch addresses. **Kept by the manager (as in 4.1)** and held to the same rules as Manage Store's `SaveBranch` — shipping picker and filter, provisioning on create, parent same-store and loop checks, store never written on edit, the children delete rule (`CWPlans/MultiStore_Management_Reference.md` §8f) |
| `MultiStoreController` | MultiStore/store management, departments, cost centers, packName lists, packNames |
| `SubsidyController` | CRUD for product groups, subsidies, subsidy-packName assignments, variations, topups |
| `CWCustomerController` | Overrides NOP customer admin — adds PackName tab, CreditNotes tab |
| `ExchangesController` | Return/exchange request management, fee rules, reason/action/status rules |
| `CreditNotesController` | Credit note CRUD and management |
| `CreditNotesReportsController` | Reporting for credit notes |
| `ReturnsController` | Return request overrides |
| `CWOrderController` | Admin shipment list with packname destinations; `OrderBadges` (POST, `Orders.OrdersView`) — the E / R flags of one page of an admin order grid (`IExchangeService.GetOrderReturnFlagsAsync`; nothing for a vendor; at most 500 ids) |

### 13.7 Admin View Components

| Component | Where injected |
|---|---|
| `PackNameExtensionPanelsViewComponent` | Customer edit page — extra panels |
| `PackNameSubsidiesViewComponent` | Customer edit page — subsidies tab |
| `AclDepartmentsViewComponent` | Category/Product ACL — department mappings |
| `ExchangeSettingsComponent` | Exchange settings page |
| `FeeConceptViewComponent` | Fee concept UI |
| `FeeSettingsComponent` | Fee settings page |
| `OrderReturnRequestsViewComponent` | Order detail page — return requests |
| `OrderListBadgesViewComponent` | `OrderListButtons` (Sales → Orders) and `CustomerDetailsButtons` (customer edit page, Orders tab) — renders the `.cw-order-badges-config` div and registers `Content/Admin/Orders/admin.order.badges.js`, which adds the **E** / **R** badges to the order-number cell after each draw of `#orders-grid` / `#order-grid`; nothing for a vendor or without `Orders.OrdersView` |

---

## 14. Domain Entities & Database Tables *(4.9 — see §13 banner)*

### 14.1 Table Naming Convention

As of the CW rename, `CW.Core`'s tables were migrated from the old company-wide `TIG_` prefix to a plugin-specific `CORE_` prefix (see §7 and `CWNameCompatibility`):

- `CORE_*` — Branch / CustomerBranch / Department entities (no sub-abbreviation)
- `CORE_PN_*` — PackName, CostCenter, DepartmentManagerMapping, PackName order/cart/shipping entities
- `CORE_SUB_*` — Subsidy entities
- `CORE_EX_*` — Exchanges / CreditNote entities
- `vw_CORE_*` — SQL Views (read-only, accessed via LinqToDB repositories)

Historical note: these tables were previously named `TIG_CO_*`, `TIG_SUB_*`, `TIG_EX_*`, and `vw_TIG_*`. A rename migration (`RenameCWTablesVersion`, see `Data/Migrations/`) renames them in place on any environment that still has the old names, preserving existing data. Plugins not yet renamed still use the old `TIG_{ABBREVIATION}_` convention.

### 14.2 PackName / Branch Entities

#### `Branch` → `CORE_Branch`
```
Id, ParentBranchId (nullable — hierarchy), BranchCode, Name, CardCode,
BranchNumber, StoreId (required), PaymentTerms, DomainRegistrationFilter,
ShippingMethod, Active, CreatedOnUtc, UpdatedOnUtc, Deleted
```
Hierarchical via `ParentBranchId`. Has many addresses via `BranchAddressMapping`. `ShippingMethod` is a comma list of
CW.Freight method names that overrides the store's `MultiStoreSettings.ShippingMethod` for the branch's packnames (empty
= the store's). `DomainRegistrationFilter` is unused — no page shows or writes it since store self-registration was
deleted (phase 8 Part 1); the column stays.

#### `BranchAddressMapping` → `CORE_BranchAddresses`
```
Id, BranchId, AddressId (FK to NOP Address table)
```

#### `CustomerBranch` → `CORE_CustomerBranch`
```
Id (= NOP CustomerId), BranchId (nullable), JimCardCode, LastModifiedDateUtc
```
1:1 with NOP `Customer`. Stores which branch the customer admin belongs to.

#### `PackNameList` → `CORE_PN_PackNameList`
```
Id, Name, StoreId, BranchId (nullable), Active, IsDefault,
AllManagers, ParentManagers, CreatedOnUtc, Deleted
```
- `IsDefault`: the list used for automatic PackName assignment.
- `AllManagers/ParentManagers = false` + manual `PackNameListManagerMapping` = specific owners only.

#### `PackNameListManagerMapping` → `CORE_PN_PackNameListManagers`
```
Id, PackNameListId, PackNameId
```

#### `PackName` → `CORE_PN_PackName`
```
Id, Name, Colour, Gender, PrintName, Group, Active,
PackNameListId (required), CustomerId (nullable — links to NOP Customer),
DepartmentId (nullable), CostCenterId (nullable),
Manager (bool), EmployeeNumber, POEntry, Comments, Deleted
```
- `CustomerId != null` → PackName is also a Customer → can order.
- `Manager = true` → must also have `CustomerId` → can order on behalf of others.
- `IsCustomer` (computed, `[NotColumn]`): `CustomerId.HasValue && CustomerId > 0`.

#### `PackNameAddressMapping` → `CORE_PN_PackNameAddresses`
#### `PackNameCartItem` → `CORE_PN_PackNameCartItem`
#### `PackNameOrderItem` → `CORE_PN_PackNameOrderItem`
#### View: `PackNameShoppingCartItem` → `vw_CORE_PN_PackNameShoppingCartItem`

### 14.3 MultiStore Entities (Department / CostCenter)

#### `Department` → `CORE_Department`
```
Id, StoreId, Name, CustomerRoleId (nullable — NOP CustomerRole for ACL), Active, Deleted
```
Flat (no hierarchy). Store-scoped. Managers assigned via `DepartmentManagerMapping`.

#### `DepartmentManagerMapping` → `CORE_PN_DepartmentManagers`
```
Id, PackNameId (the manager), DepartmentId
```

#### `CostCenter` → `CORE_PN_CostCenter`
```
Id, StoreId, BranchId (nullable), Name, Active, Deleted
```
`BranchId = null` → store-level cost center. `BranchId != null` → restricted to that branch + children.

#### `CostCenterAddressMapping` → `CORE_PN_CostCenterAddresses`

### 14.4 Subsidy Entities

#### `ProductGroup` → `CORE_SUB_ProductGroup`
```
Id, StoreId, Name, Description, Icon (icon class name), Colour, DisplayOrder, CreatedOnUtc, Active, Deleted
```

#### `ProductGroupProduct` → `CORE_SUB_ProductGroupProduct`
```
Id, ProductGroupId, ProductId (FK to NOP Product)
```

#### `Subsidy` → `CORE_SUB_Subsidy`
```
Id, StoreId, Name, Description, Credit (global credit amount),
DurationStart, DurationEnd, AnnualRecurrence,
OrderOutsidePeriod, OnlyAllocatedProducts, IncludeManagers,
RestrictToAllocatedQty, Active, CreatedOnUtc, Deleted
```

#### `SubsidyProductGroup` → `CORE_SUB_SubsidyProductGroup`
```
Id, SubsidyId, ProductGroupId, Quantity (total qty allowed),
Credit (credit for this group), DisplayOrder, CreatorId, CreatedOnUtc, Deleted
```

#### `SubsidyPackName` → `CORE_SUB_SubsidyPackName`
```
Id, PackNameId, SubsidyId, CreatorId, CreatedOnUtc, Active, Deleted
```

#### `SubsidyVariation` → `CORE_SUB_SubsidyVariation`
```
Id, SubsidyPackNameId, Credit (override — nullable), DurationStart (nullable), DurationEnd (nullable), CreatedOnUtc, Deleted
```

#### `SubsidyTopup` → `CORE_SUB_SubsidyTopup`
```
Id, SubsidyPackNameId, Credit (additional credit), CreatedOnUtc
```

#### `ProductGroupTopup` → `CORE_SUB_ProductGroupTopup`
```
Id, SubsidyPackNameId, ProductGroupId, Quantity (extra qty), Credit (extra credit), CreatedOnUtc
```

Also: `SubsidyOrderItem`, `SubsidyOrderShipping`, `SubsidyTopupHistory`, `SubsidyVariationHistory`, `ProductGroupTopupHistory`.

#### `SubsidyRestoreItem` → `CORE_SUB_SubsidyRestoreItem`
```
Id, OrderId, OrderItemId, SubsidyId, ProductGroupId?, SubsidyOrderItemId?,
ReturnCaseLineId, ReturnRequestId?, ReturnedQuantity,
Quantity, QuantityTopup, Credit, CreditTopup, ProductGroupCredit, ProductGroupCreditTopup,
CreatedByCustomerId?, CreatedOnUtc
```
What an approved return gave back, bucket for bucket. **Insert-only, never a mutation of the consumption
it reverses**, and amounts are stored **positive** — the availability views negate them. `OrderId` and
`OrderItemId` are the **original** order's, which is what keeps a restoration inside the same
cancelled-order and ordering-period window as the consumption. See `CWPlans/Exchanges_Rebuild_Record.md` §4 and §8.

#### `OrderSettlement` → `CORE_PN_OrderSettlement` (`Domain/Orders`)
```
Id, OrderId (unique, FK to Order, cascade delete), SettleableAmount, SubsidyStockAmount,
SubsidyShippingAmount, CreditNoteAmount, CreatedOnUtc
```
One row per order that subsidy or store credit paid for, written once by `SettleOrderAsync` from the
`SettlementPlan` it applied (`OrderSettlementHelper.FromPlan`; nothing is written when nothing was covered).
All money incl. tax. `SettleableAmount` is what the pipeline was offered — goods at their line prices plus
shipping (packname carts; a plain cart offers nop's own total, §14.5). **Every reader of an order's
subsidy / store credit reads this row** (`ICWOrderService.GetOrderSettlementAsync`, cached): customer order
page, admin order cards, admin packname card, e-mail, invoice PDF, profit report. Never derive it again from
the ledgers. Orders placed before phase 6 have no row and show nop's totals only. Not updated on cancel —
the credit returns through the views; the row records the placement.

#### SQL Views (read-only)

| View Class | DB View | Purpose |
|---|---|---|
| `SubsidyPackNameCredit` | `vw_CORE_SUB_SubsidyPackNameCredit` | Total available credit for a PackName/Subsidy |
| `ProductGroupPackNameCredit` | `vw_CORE_SUB_ProductGroupPackNameCredit` | Credit per product group per PackName |
| `SubsidyPackNameCanOrder` | `vw_CORE_SUB_SubsidyPackNameCanOrder` | Whether a PackName can currently order |
| `SubsidyInOrderingPeriod` | `vw_CORE_SUB_SubsidyInOrderingPeriod` | Subsidies in ordering period |
| `SubsidyVariationInOrderingPeriod` | `vw_CORE_SUB_SubsidyVariationInOrderingPeriod` | Variations in ordering period |
| `StoreSubsidyVariationSetting` | `vw_CORE_SUB_StoreSubsidyVariationSetting` | Store-level variation settings |

The per-order total view (`vw_CORE_SUB_OrderSubsidyTotal`, `OrderSubsidyTotal`) was removed by phase 6
(2026-10-02); the settlement row above replaces it.

### 14.5 Subsidy Credit Consumption Order

When a PackName orders, credits are consumed in this sequence:
1. Product-group `Quantity` (`SubsidyProductGroup.Quantity`)
2. Product-group topup `Quantity` (`ProductGroupTopup.Quantity`)
3. Product-group `Credit` (`SubsidyProductGroup.Credit`)
4. Product-group topup `Credit` (`ProductGroupTopup.Credit`)
5. Global Subsidy `Credit` (`Subsidy.Credit`)
6. Subsidy topup `Credit` (`SubsidyTopup.Credit`)

**Subsidy is consumed before credit notes** (business decision 15, 2026-09-10): `SubsidySettlementSource`
is settlement priority 1 and `CreditNoteSettlementSource` priority 2. Return fees are added after both.

**Consumption is netted by restorations — in the availability views only.**
`vw_CORE_SUB_SubsidyPackNameCredit` and `vw_CORE_SUB_ProductGroupPackNameCredit` union the consumption
rows with `CORE_SUB_SubsidyRestoreItem` negated, via the shared `CWDatabaseService.SubsidyNetConsumptionSource`
fragment. What an order consumed is recorded on the order itself — its settlement row
(`CORE_PN_OrderSettlement`) — which a later return does not change.

**What subsidy and store credit settle (phase 6, decisions 5 and 6).** A packname cart offers the pipeline
its goods at the line prices (incl. tax, before the subtotal discount) plus shipping (incl. tax). Subsidy,
then credit notes, settle that; what is left is the balance due. The customer's share is then: balance due −
the subtotal discount **actually applied** (never more than the balance due) + payment-method fee − order
discount − gift cards − reward points; exchange fees are added last. The order keeps nop's gross columns
(`OrderSubtotal*`, `OrderShipping*`, `OrderTax`); `OrderTotal` is what the customer pays;
`OrderSubTotalDiscount*` holds the discount actually applied. A **plain** cart keeps nop's order: its credit
notes settle nop's total, after discounts and gift cards. Detail: `CWPlans/subsidy-system.md` §7.

### 14.6 Exchanges & Credit Note Entities

#### `CWCreditNote` → `CORE_EX_CreditNote`
```
Id, CustomerId, OrderId, CreatedOnUtc, Reference (long, nullable),
Name, Amount, Remain (remaining balance), ReturnReason, StoreId, ReturnRequestId (nullable)
```

#### `CWCreditNoteHistory` → `CORE_EX_CreditNoteHistory`

#### `CWFeeRule` → `CORE_EX_FeeRule`
```
Id, StoreId, FeeTypeId (FeeType enum), Amount, OperatorId (FeeRuleOperator enum), UnitsQuantity
```
`FeeType`: `ReShippingFee = 1`, `RepackagingFee = 2`, `AdditionalFee = 10`

#### `CWReturnReasonRule` → `CORE_EX_ReturnReasonRule`
```
Id, ReturnReasonId, StoreId, AllowStoreCredit, AllowExchange, AllowRepackagingFee, AllowShippingFee, BackInStock, IsDisabled
```

#### `CWReturnActionRule` → `CORE_EX_ReturnActionRule`
```
Id, ReturnActionId, StoreId, AllowStoreCredit, AllowExchange, AllowRepackagingFee, AllowShippingFee, BackInStock, IsDisabled
```

#### `CWReturnStatusRule` → `CORE_EX_ReturnStatusRule`
```
Id, ReturnStatusId, StoreId, PerformStoreCredit, PerformExchange, BackInStock, Authorised
```

#### `CWReasonActionRelationship` → `CORE_EX_ReasonActionRelationship`
Links return reasons to valid actions.

#### `CWReturnRequestItem` → `CORE_EX_ReturnRequestItem`
```
Id, ReturnRequestId (FK to NOP ReturnRequest), ExchangeModeId (ExchangeReturnMode),
ExchangeTypeId (ExchangeType), StaffShipQty, StaffRepackQty,
ReturnRequestGroup (Guid), NeedsCompletion, IsCompleted,
ShoppingCartItemId, OrderItemId, ReturnedOrderItemId,
AdditionalFeeName, AdditionalFeeAmount, virtual ReplacementItem
```

#### `CWReturnReplacementItem` → `CORE_EX_ReturnReplacementItem`
#### `CWImpersonationToken` → `CORE_EX_ImpersonationToken`

#### Enums
```csharp
ExchangeReturnMode { Exchange = 1, CreditNote = 2 }
ExchangeType { SameStyle = 1, DifferentStyle = 2, Exact = 3 }
FeeType { ReShippingFee = 1, RepackagingFee = 2, AdditionalFee = 10 }
FeeRuleOperator { ... }   // see Domain/Exchanges/FeeRuleOperator.cs
```

---

## 15. Customer Attributes, Roles & Settings *(4.9 — see §13 banner)*

### 15.1 CW Custom Customer Generic Attributes (`CWCustomerAttributeNames`)

The class is `Domain/Customers/ExtendedCustomerAttributeNames`; stored in NOP's `GenericAttribute` table:
- `ImpersonationOriginStore` — store ID when impersonating
- `CW_ImpersonationToken` (`ImpersonationToken`) — impersonation token value

A manager-created customer has **no** CW attribute: the packname link is `PackName.CustomerId`, the password is a
random one nobody sees and the welcome e-mail carries nop's own password-recovery token
(`NopCustomerDefaults.PasswordRecoveryTokenAttribute`, valid `CustomerSettings.PasswordRecoveryLinkDaysValid` days);
the former `ForceToChangePassword`, `StoreId`, `BranchId`, `EmployeeId` and `PinnedStores` names were dead and were
removed (phase 7, 2026-10-08). nop signs the customer in when the password is set through that link.

### 15.2 CW Customer Roles

Department-linked customer roles use `DEP_` prefix (`CWCustomerRoleNames.DepartmentPrefix`).

**Two role sets in the packname context** (phase 5 Part 1, 2026-10-01). In the public store, while the signed-in
customer has a packname selected, `ICustomerService.GetCustomerRoleIdsAsync` returns what the catalogue **sees**:
the packname's account roles (or Registered + `DefaultPackNameCustomerRoleIds` for a packname without an account)
plus `Department.CustomerRoleId`, followed by the signed-in roles as **negative ids** (cache-key material only —
`Helpers/CustomerRoleSetHelper`). Everything nop keys or filters ACL on follows it. What the customer **pays and may
do** follows the signed-in customer: `GetCustomerRolesAsync` is not overridden, and code that filters on role ids
for paying (tier prices, plugin role limits) uses `ICustomerOverrideService.GetSignedInCustomerRoleIdsAsync`.
Never look roles up by those ids. To check something against a packname other than the selected one, open
`PackNameCatalogScope.Begin(packName)` **in the calling method** (lessons #89).

**The allocation gate** (phase 5 Part 2, 2026-10-01). When every active subsidy of a packname has
`OnlyAllocatedProducts`, it sees only its allocated products (with their grouped-product relatives) and the
categories / manufacturers holding them; a closed ordering period shows nothing. One service answers it,
`IPackNameCatalogGate`, and `AclOverrideService` applies it in `AuthorizeAsync<TEntity>` and `ApplyAcl` — so every
ACL check and ACL query filter follows it. Do not filter on allocation anywhere else. The answer is carried into
nop's cache keys by a restriction marker appended to the role set. For an exchange replacement, open
`PackNameCatalogScope.Begin(packName, exchangeType)`: Exact / Same Style are unrestricted, Different Style follows
the allocation, and a closed period never blocks either.

### 15.3 MultiStoreSettings (per-store)

`Domain/Settings/MultiStoreSettings.cs`; edited on Manage Store → *Store Settings* (per store, with *override for this
store*) and *CW Admin → Default settings* (store 0). Rewritten from the class at phase 8 Part 1 (2026-10-11).

**Feature switches** — `PackNameEnable`, `SubsidyEnable`, `CreditNotesEnable`, `ExchangesEnable`, `BulkOrderEnable`:
see `CWPlans/Feature_Toggles_Reference.md`. The `BulkOrder*` group is described in the bulk-ordering docs.

| Setting | Purpose |
|---|---|
| `UseStaffAsPackName` | A customer an admin links to a branch gets a packname in the branch's default list (created if missing); the *Generate PackNames* button does it now for customers without one (`IPackNameProvisioningService`) |
| `UseBranchesAsPackNameList` | A new branch gets its default packname list; the *Generate PackName Lists* button does it now for branches without one |
| `PackNameListNameFrom` | How those lists are named: `0` branch code, `1` branch name |
| `EmployeeNumberEnabled` / `PrintNameEnabled` / `POEntryEnabled` / `CommentsEnabled` / `GenderEnabled` | Show that packname field |
| `PrintNameProductAttributeId` | The product attribute the print name pre-fills (`PrintNamePrefillEventConsumer`) |
| `ShowDepartmentPackNamesByBranch` | Department managers see each branch's list with only the department's packnames instead of one department list |
| `AllowManagersToAddPackNames` | Store managers can add packnames from the storefront |
| `AllowToAddCustomerToPackName` | Link an existing customer to a packname |
| `AllowSubsidySelection` | Subsidy checkboxes on the manager's add / edit packname form (Subsidy feature on) |
| `AllowEditSubsidyTopup` | Managers can edit top-ups |
| `AllowToAddByExcel` / `AllowSubsidyCountByExcel` | Excel import of packnames and the number of subsidy columns — **X-01, phase 8 Part 2; not wired yet** |
| `LimitAssignedBranches` | Limit managers to adding packnames to the branches they are assigned to |
| `PackNamesOwnGlobalValue` / `PackNamesOwnStartAndFinishDates` | Packnames may carry their own subsidy credit value / dates |
| `ShippingMethod` | Comma list of CW.Freight method names packnames of the store may use, the first the default; empty = every method. A branch's own list overrides it. Saves keep only names the plugin offers |
| `AllowHomeDelivery` | Packname / customer addresses are offered at the checkout besides the branch addresses |
| `AllowCustomersToAddAddress` | Packnames can add an address at the checkout (`PackNameCanAddAddress`) |
| `DoNotPreSelectShippingAddress` | No address pre-selected at the checkout; switching it on clears the selected addresses |
| `IncludeShippingOnSubsidy` | Subsidy credit also pays shipping (off by default — decision 4) |
| `SubsidySendUserAfterLogin` | After sign-in, packname selection and impersonation the customer lands on the subsidy catalogue when the selected packname has active product groups (phase 7, decision H); read before `BulkOrderSendUserAfterLogin` by `IPackNameLandingService` |
| `DefaultPackNameCustomerRoleIds` | Customer roles added to a customer a manager registers from the storefront *Add packname* form (phase 7, decision E — not when an admin links an existing customer); a packname without an account sees the catalogue with them (§15.2) |
| `AllowCloseWithoutPrintingReturnNote` | Exchanges: the customer may dismiss the return confirmation without printing the return note |
| `OwnPlacementPaymentMethods` | Not on any screen — payment methods that place the order in their own pages (default `Payments.PayPalCommerce`); see `MultiStore_Management_Reference.md` §6.1 |

Gone: `UseBranchAddress` (never existed in 4.9 — P-12, not needed), `GenderFiltering` (phase 5 Part 2),
`RegistrationScreen` / `RegistrationScreenPageName` (store self-registration deleted, phase 8 Part 1).

---

## 16. Caching Reference *(4.9 — see §13 banner)*

### PackName caches (`PackNameServicesDefaults`)
- `CW.packname.bycustomer.{customerId}`
- `CW.packname.activepacknames.bylistid.{listId}`
- `CW.packname.cartcounts.bycustomer.{customerId}`
- `CW.packname.activepacknames.fordepartments.bylistid.{listId}-{departmentId}`
- `CW.packname.activepackname.bymanagerid.{managerId}`
- `CW.packname.addresses.{packNameId}`
- `CW.packname.addresses.{packNameId}-{addressId}`
- `CW.departments.bymanager.{managerId}`
- `CW.packnames.bymanager.{managerId}`
- `CW.packname.owned.bymanager.{managerId}-{packNameId}`
- `CW.packnamelists.bydepartment.ids.{departmentId}`

### Subsidy caches (`SubsidyServicesDefaults`)
- `CW.subsidy.active.subsidies.bystore.{storeId}`
- `CW.subsidy.packnames.bysubsidy.{subsidyId}`
- `CW.subsidy.subsidies.bypackname.{packNameId}`
- `CW.subsidy.inordering.subsidies.bypackname.{packNameId}`
- `CW.subsidy.orderingblocked.bypacknameid.{packNameId}`
- `CW.subsidy.hasactiveproductgroups.bypacknameid.{packNameId}`
- `CW.subsidy.subsidyvariation.bysubsidypacknameid.{id}`
- `CW.subsidy.productgroups.icons`
- `CW.subsidy.credit.bypacknameid.{packNameId}`
- `CW.subsidy.products.byproductgroup.{productGroupId}`
- `CW.subsidy.productgroups.bysubsidy.{subsidyId}`
- Visibility caches for categories/manufacturers by packName

### Branch / MultiStore caches (`BranchServicesDefaults`, `MultiStoreServicesDefaults`)
- `CW.branch.bystore.{storeId}`
- `CW.branch.addresses.{branchId}`, `CW.branch.addresses.{branchId}-{addressId}`
- `CW.branch.breadcrumb.{branchId}`
- `CW.branch.staffcount.{branchId}`
- `CW.manager.storeId.{customerId}`
- `CW.shippingmethods.bybranch.{branchId}`
- `CW.costcenter.addresses.{costCenterId}`, `CW.costcenter.addresses.{costCenterId}-{addressId}`
- `CW.costcenter.selectitems.{storeId}`
- `CW.packnamelistids.bymanager.{managerId}`
- `CW.packnamelists.bymanager.{managerId}`

---

## 17. Stored Procedures *(4.9 — see §13 banner)*

| SP Name | Used In |
|---|---|
| `packname_get_listids_by_manager` | `CWCoreDefaults.GetPackNameListIdsByManagerSPName` |
| `subsidy_get_history` | `CWCoreDefaults.GetSubsidyHistorySPName` |

Called via `ICWDatabaseService` (wraps raw SP calls).

---

## 18. Permissions Reference *(4.9 — see §13 banner)*

| Constant | Key String | Default Role |
|---|---|---|
| `ADMIN_ACCESS_BRANCH` | `CW.CORE.Admin.AccessBranch` | Administrators |
| `ADMIN_ACCESS_MULTISTORE` | `CW.CORE.Admin.AccessMultiStore` | Administrators |
| `ADMIN_ACCESS_MULTISTORE_SETTINGS` | `CW.CORE.Admin.AccessMultiStoreSettings` | Administrators |
| `ADMIN_ACCESS_DEFAULT_STORE_SETTINGS` | `CW.CORE.Admin.AccessDefaultStoreSettings` | Administrators |

---

## 19. Public Store — Navigation & Widgets *(4.9 — see §13 banner)*

### Customer Area Navigation

| Tab | Route | CSS Class | Tab Id |
|---|---|---|---|
| My Store Credit (`Plugins.CW.Core.CustomerCreditsForMe`) | `Plugin.CW.Core.CreditsForMe` — `Credits/CreditsForMe` | `credits-forme` | 21 (`CWCoreDefaults.CustomerCreditsForMeMenuTab`) |
| Subsidies (`Plugins.CW.Core.CustomerSubsidies`) | `Plugin.CW.Core.CustomerSubsidies` — `customer/subsidies` | `customer-subsidies` | 22 (`CWCoreDefaults.CustomerSubsidiesMenuTab`) |

Each page passes its own tab id to `CustomerNavigationViewComponent`; nop highlights an item only when
`SelectedTab == item.Tab`, so `-1` means "never highlighted". Both pages use a page-scoped stylesheet under
`Content/MyAccount/` (no `<style>` in the view) and the same card layout.

### Widget Zones

Registered in `CWCorePlugin` (`GetWidgetZonesAsync` / `_widgetComponents`):

- `PublicWidgetZones.HeaderLinksBefore` → `BulkOrderLinkViewComponent`
- `PublicWidgetZones.HeaderLinksAfter` → `PackNameSelectorViewComponent`
- `PublicWidgetZones.ContentBefore` → `PackNameInfoViewComponent`
- `PublicWidgetZones.ProductDetailsOverviewBottom` / `ProductDetailsBottom` / `ContentAfter` → `BulkOrderProductPageViewComponent` (draws in the first the theme renders)
- `AdminWidgetZones.OrderDetailsBlock` → `PackNameOrderAdminDetailsViewComponent`
- `AdminWidgetZones.ProductDetailsBlock` → `BulkOrderProductPageAdminViewComponent`
- `AdminWidgetZones.OrderShipmentAddButtons` / `OrderShipmentDetailsButtons` → `CWShipmentDestinationViewComponent`
- `AdminWidgetZones.OrderListButtons` / `CustomerDetailsButtons` → `OrderListBadgesViewComponent` (E / R badges; nop also renders `CustomerDetailsButtons` on *Create customer*, where the script finds no grid and does nothing)
- `CWWidgetsZones.WIDGET_CREDITNOTES_FOR_CART` → `CreditNotesWidgetsViewComponent`
- `CWWidgetsZones.WIDGET_CREDITNOTES_ORDER_CUSTOMER` → `CreditNotesWidgetsViewComponent`
- `CWWidgetsZones.WIDGET_CREDITNOTES_ORDER_ADMIN` → `CreditNotesWidgetsViewComponent`
- `CWWidgetsZones.WIDGET_CREDITNOTES_ADD_FORM` → `CreditNotesWidgetsViewComponent` (invoked by name from CW views)
- `CWWidgetsZones.WIDGET_CREDITNOTES_CUSTOMER_INFO` → `CreditNotesWidgetsViewComponent` (invoked by name from CW views)
- `CWWidgetsZones.WIDGET_EXCHANGES_FEES_FOR_CART` → `ExchangesWidgetsViewComponent`
- `CWWidgetsZones.WIDGET_EXCHANGES_FEES_FOR_ORDER` → `ExchangesWidgetsViewComponent`
- `CWWidgetsZones.WIDGET_EXCHANGES_FEES_FOR_ORDER_ADMIN` → `ExchangesWidgetsViewComponent`

---

## 20. Activity Log Types *(4.9 — see §13 banner)*

Branch: `AddNewBranch`, `EditBranch`, `DeleteBranch`
Department: `AddNewDepartment`, `EditDepartment`, `DeleteDepartment`
CostCenter: `AddNewCostCenter`, `EditCostCenter`, `DeleteCostCenter`
PackNameList: `AddNewPackNameList`, `EditPackNameList`, `DeletePackNameList`
(plus PackName, Subsidy, ProductGroup, Exchange/Return actions)

---

## 21. Extension Patterns & Background Jobs *(4.9 — see §13 banner)*

### IPackNamePanelProvider

CW.Core defines `IPackNamePanelProvider` so other plugins can inject extra panels into the PackName admin UI without modifying CW.Core. `SubsidyPackNamePanelProvider` is the built-in implementation.

### Background Jobs

`IBackgroundTaskQueue` / `QueuedHostedService` (registered as `IHostedService`) — for fire-and-forget async operations (e.g., sending messages after order processing without blocking the request).

---

## 22. Notes for Future Revision

These are intentionally deferred and should be resolved before they block real work:

- **Note A — Tests for CW plugins.** No tests currently ship with CW plugins (the SSO plugin has none either; its
  sign-in flows have only been exercised through the DEBUG mock IdP). Decide whether new features add NUnit tests inside `Tests/Nop.Tests` (which conflicts with the Cardinal Rule), or whether a parallel `Tests/Cw.Tests` project should be added. Pending decision.
- **Note B — Default theme. CLOSED 2026-08-27.** Only `DefaultClean` is deployed today, so nothing collides and there is no decision outstanding. The underlying hazard is real but is now written down where it will be found: **§10.4** records that `ViewLocationExpander` puts plugin views ahead of theme views, lists the five storefront views CW.Core forks, and gives the four-step check to run when a theme is enabled. `Voyage` and `Pavilion` remain `.gitignore`d; if either is enabled, run §10.4 step 2 first. (Theme plugins are not ported to 5.00 yet.)
- **Note C — SSO sign-in against a real IdP.** OIDC and SAML sign-in, sign-out and `id_token_hint` have not run
  against a real identity provider (Entra or Keycloak). Until they have, treat both flows as unverified; the open
  checks are listed in the SSO plugin's `References/` docs.

---

## 23. Quick Reference: Do / Don't

**Do**
- Add features as new plugins under `Plugins/Nop.Plugin.CW.*`
- Override nop services via the consuming plugin (feature-local) or `CW.Core` (shared) — see §10
- Use `IRepository<T>`, FluentMigrator, FluentValidation, Mapster `BaseMapperProfile`
- Inherit `BaseAdminController` (plugin references CW.Core) or `BasePluginController` (standalone) — see §9.1
- Use `[CheckPermission(...)]` on admin actions
- Keep migrations in the plugin's own `Data/Migrations/` with timestamps in `MigrationVersions.cs`
- Use table-name convention `{PLUGIN_ABBREVIATION}_{EntityName}` via `INameCompatibility` (e.g. `CORE_` for `CW.Core`)
- Use locale resource key convention `Plugins.CW.{PluginName}.Fields.{PropertyName}`
- Hook the admin menu via `AdminMenuCreatedEvent` against the `CW.Core` system-name anchor
- Ask before introducing any cross-plugin coupling — pattern choice in §10.2 is case-by-case
- Give business logic / orchestrator classes an interface (`IFooLogic`/`FooLogic`), same as any other service — see §6.13
- Run the §10.5 checklist on every plugin ported from 4.90, then open each page it owns — views compile at runtime
- Bump `plugin.json` `Version` whenever `UpdateAsync` or an `Update` migration must run
- Keep plugin documents in the plugin's `References/` folder; move superseded ones to `References/Deprecated/` — see §0.4
- Hand over git commands; leave committing and pushing to the user — see §0.2

**Don't**
- Commit a real tenant ID, secret, certificate or connection string — the repository is public — see §0.9
- Read camelCase properties from nop admin JSON in plugin JS — it is PascalCase — see §10.5
- Edit anything under `Libraries/Nop.*`, `Presentation/Nop.*`, `Tests/Nop.Tests`, or any OOTB `Nop.Plugin.*`
- Use a different ORM, validation library, or mapper
- Use `EngineContext.Current.Resolve<T>()` in new code
- Call Autofac APIs directly — register via `IServiceCollection` in `INopStartup`
- Use Bootstrap 3 idioms (`panel`, `pull-left`, `pull-right`)
- Use Kendo UI grids — use `DataTablesModel` + `PrepareToGridAsync`
- Expect a `.js`/`.css` edit to take effect without emptying `Presentation/Nop.Web/wwwroot/bundles` — see §9.11
- Use `let` for a closure variable read by a hoisted inner function in bundled admin JS — see §9.11
- Bind delegated handlers to a long-lived panel without `.off('.ns')` and a namespace — see §9.12
- Delegate from `document` when a nearer stable ancestor exists; it outlives every reload — see §9.12
- Put `@T(...)`, `@Model` or a hard-coded action URL inside a `<script>` — use a config div — see §9.14
- Interpolate a record's own text into an `onclick` — bind the handler and read the row — see §9.15
- Combine a `!important` display utility (`d-flex`, `d-none`) with a scripted show/hide on one element
- Add a `<ProjectReference>` from a secondary **peer** plugin to `Nop.Plugin.CW.Core.csproj` (peers only — a framework→extension package may, with `<Private>false>`/`<ExcludeAssets>runtime>`; see §10.2 exception)
- Put plugin migrations into `Nop.Data/Migrations/`
- Skip `INopStartup.Order` — silent ordering bugs are painful to diagnose
- Target the uninstalled plugins (`Misc.WebApi.Frontend`, `Misc.RFQ`, `Misc.Dynamics365`, `Misc.Brevo`) for new work

---

## 24. Lessons Learned

Entries promoted from `tasks/lessons.md` — validated rules for this codebase.

| # | Mistake | Rule |
|---|---|---|
| 1 | Used `MigrationBase` as base class for update migrations | Use `Migration` (FluentMigrator) for update migrations and `AutoReversingMigration` for schema ones — never `MigrationBase` directly. |

---

*Merged from CLAUDE.md + AGENTS.md + AGENTS_M1.md — 2026-06-08. Adapted for nopCommerce 5.00 — 2026-10-20.*
