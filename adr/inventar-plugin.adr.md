# ADR: Inventar Plugin

## Purpose

Persistent architectural decisions for the inventar-plugin iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-14 - Command Sequencing: Core Before Plugin

### Decision

Commands 11 (nav contributions), 12 (generic plugin pages), and 13 (Inventar plugin) are implemented in strict order in a single run. Commands 11 and 12 are `generic-core` changes that require explicit ADR 0001 GR-002 user approval before any core file is edited. Command 13 is `plugin-only` and is unblocked only after both core commands are complete and all tests pass.

### Rationale

ADR 0001 mandates that generic-core changes be approved before implementation. Commands 11 and 12 each extend the plugin contracts assembly, the plugin runtime record, the view model, host services, and shared UI surfaces — all of which qualify as core code under ADR 0001. Implementing Command 13 before the host infrastructure exists would require either stubbing or later refactoring. The sequential dependency is explicit and enforced at the design level.

---

## 2026-06-14 - IPluginNavEntryService: Reads NavEntries from RegisteredPluginRuntime

### Decision

`IPluginNavEntryService.GetVisibleNavEntriesAsync` reads `NavEntries` directly from the `RegisteredPluginRuntime` record (populated at load time by `PluginLoader.PluginContributionCollector`). It does not call `module.RegisterContributions(sink)` at query time. The service iterates `IPluginRegistryReader.GetRegisteredPlugins()`, collects all `NavEntries` from every registered runtime, applies the claims-only permission filter, and returns the sorted flat list.

### Rationale

Nav entries are stable per-load: they are declared in `RegisterContributions` which is called once during plugin activation. Re-collecting at every page render would be wasteful. The data is already materialized in `RegisteredPluginRuntime.NavEntries` after the loader runs the collector. This contrasts with admin panel providers and page providers, which are re-collected at call time because the host needs to invoke the provider's async methods (which may be non-idempotent) on demand. Nav entries contain only static metadata (label, icon, route, permission key, sort order) — no async provider invocation is needed.

---

## 2026-06-14 - _Layout.cshtml: @inject IPluginNavEntryService for Plugin Nav Entries

### Decision

`Views/Shared/_Layout.cshtml` gains `@inject IPluginNavEntryService PluginNavEntryService`. Inside the navbar `<ul>`, after the static navigation links and before the admin dropdown, each visible nav entry is rendered as a `<li class="nav-item"><a class="nav-link" href="@entry.Route"><i class="bi @entry.Icon"></i> @entry.Label</a></li>`. The service call is `await PluginNavEntryService.GetVisibleNavEntriesAsync(User)`.

### Rationale

The layout already performs an async `UserManager.GetUserAsync` call and uses `@inject UserManager<ApplicationUser>`. Adding a second `@inject` follows an established pattern. The nav-entry list is claims-gated at the service level, so no additional view logic is needed. The Bootstrap Icons `<i class="bi @entry.Icon">` pattern is used throughout the existing admin UI. Rendering entries after static links and before the admin dropdown keeps plugin nav visually separated from core nav while remaining inside the collapsed navbar for mobile.

---

## 2026-06-14 - IPluginPageService: Re-collect Pattern, Inner ContributionSink with 4 Explicit Empty Bodies

### Decision

`PluginPageService` uses the same re-collect-at-call-time pattern as `PluginAdminCommandService`. Its inner `ContributionSink` explicitly implements all four abstract `IPluginContributionSink` methods (`AddRoute`, `AddService`, `AddMemberProvider`, `AddBackgroundJob`) as empty bodies, and overrides `AddPageProvider` to collect. The four core methods cannot be omitted because the interface defines them without default bodies (confirmed in research Q4).

### Rationale

Research Q4 confirmed that `PluginAdminCommandService.ContributionSink` implements all four core methods explicitly as empty bodies. The interface does not provide default implementations for them. Any `IPluginContributionSink` implementation must provide all four, making the pattern mandatory. `PluginPageService` follows this exactly.

---

## 2026-06-14 - PluginPageCommandRequest as Named Input Model for API Controller

### Decision

A named record `PluginPageCommandRequest(string ModuleId, string PageKey, string CommandKey, string? EntityKey, IReadOnlyDictionary<string, string> Arguments)` is the body type for `POST /api/plugin/page-commands`. It lives in the controller file (or a co-located models file) rather than in `Services/Abstractions`, since it is a transport-layer type with no host-service semantic meaning.

### Rationale

Placing transport models near their controller avoids polluting the service abstractions layer with HTTP-specific binding concerns. The existing `PluginAdminCommandRequest` (in Contracts) is a different pattern — it is passed through to the plugin provider. `PluginPageCommandRequest` is host-only and never crosses the host-plugin boundary.

---

## 2026-06-14 - Views/PluginPage Use Tuple View Models

### Decision

`Views/PluginPage/Index.cshtml` is typed `@model (PluginPageDefinition Definition, IReadOnlyList<PluginDataRow> Rows, string ModuleId, string PageKey)`. `Views/PluginPage/Detail.cshtml` is typed `@model (PluginPageDefinition Definition, PluginDataRow Row, string ModuleId, string PageKey)`. No dedicated view model record is created.

### Rationale

The data shapes are simple, controller-local projections. Creating named view model records for one-off controller → view transfers would add classes with no reuse value. The existing codebase uses typed view models only where the model is shared or complex (e.g., `PluginAdminStatusViewModel`). Tuple view models are idiomatic C# for lightweight projections.

---

## 2026-06-14 - Inventar Permission Keys: inventar.items.read and inventar.items.manage

### Decision

The Inventar plugin declares two permission keys: `inventar.items.read` (list and detail access) and `inventar.items.manage` (create, edit, delete). The `ListPermission` on `PluginPageDefinition` is `"inventar.items.read"`. Each mutating command sets `RequiredPermission = "inventar.items.manage"`.

### Rationale

Research Q5 confirmed that no `general-extension` plugin permission key convention exists beyond the flat dot-separated pattern. The existing pattern (`members.read`, `members.manage`, `admin.access`, `selfservice.access`) is feature-scoped, not module-ID-scoped. `inventar.items.read` and `inventar.items.manage` follow this pattern exactly: the prefix is the feature area (`inventar`), the suffix is the capability scope (`items.read`, `items.manage`). This is more readable than a module-ID-prefixed form (`clubgear.plugin.inventory.items.read`) and consistent with all existing permission keys.

---

## 2026-06-14 - Inventar Plugin: Dynamic Member Picker via GetListAsync in GetPageDefinitionAsync

### Decision

`InventarPageProvider.GetPageDefinitionAsync` calls `host.Members.GetListAsync(search: null, ct)` to obtain the full member list and maps each `PluginMemberSummary` to a `PluginFieldSchemaOption(memberId.ToString(), member.FullName)`. This produces the `Options` list for the `VerantwortlichesMitgliedId` `Select` field in the Create and Edit command schemas. The member list is fetched fresh on every call to `GetPageDefinitionAsync` — no caching.

### Rationale

Research Q7 confirmed Inventar is the first plugin to populate `PluginFieldSchemaOption` values dynamically. The contract (`IPluginMemberReader.GetListAsync`) is already the approved façade for plugin-layer member access under ADR 0001 GR-003. The member list at typical club sizes (tens to hundreds of members) is small enough that fetching it on demand is not a performance concern. Caching would require invalidation logic tied to member lifecycle events, adding complexity not justified here.

---

## 2026-06-14 - Inventar Plugin: Table Name Confirmation

### Decision

The single Inventar data table is named `plugin_clubgear_plugin_inventory_inventory_items` as produced by `PluginSchemaNamePolicy.GetTableName("clubgear.plugin.inventory", "inventory_items")`. A unique index on `Nummer` is added in the same migration.

### Rationale

Research Q6 traced the `NormalizeIdentifier` logic and confirmed the resulting name is 49 characters, well within SQLite limits, and not a reserved word. The index on `Nummer` enforces uniqueness of inventory item numbers at the database level, consistent with CarInfo's `UNIQUE INDEX` on `(MemberId, LicensePlate)`.

---

## 2026-06-14 - Inventar Plugin: Single Migration, Two Source Files

### Decision

The plugin consists of four `.cgcs` source files: `InventarPluginModule.cgcs` (module + manifest), `InventarMigrations.cgcs` (one migration class `InventarSchemaMigration` with MigrationId `"001_inventar_schema"`), `InventarData.cgcs` (internal data service and records), and `InventarProviders.cgcs` (page provider and admin panel provider). The manifest file is `plugin.json`.

### Rationale

This mirrors the CarInfo file layout (`CarInfoPluginModule.cgcs`, `CarInfoSchemaMigration.cgcs`, `CarInfoData.cgcs`, `CarInfoProviders.cgcs`, `plugin.json`). A single migration is sufficient since Inventar is a new plugin with no prior schema state to upgrade. The `.cgcs` extension follows the established plugin source-file convention.

---

## 2026-06-14 - ContractVersion Remains 1.4.0

### Decision

`ContractVersion.Current` is not changed from `1.4.0` in this iteration.

### Rationale

Both new interface methods (`AddNavEntries`, `AddPageProvider`) use default no-op bodies on `IPluginContributionSink`, making them purely additive at the IL level. Existing compiled plugins dispatch to the default body without recompilation. The nav-contributions ADR (2026-06-13) and generic-pages ADR (2026-06-14) both established this precedent. A version bump is not required for runtime correctness and is deferred to a dedicated housekeeping commit if needed for documentation under GR-009.
