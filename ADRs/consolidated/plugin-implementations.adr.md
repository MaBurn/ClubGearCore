# ADR: Plugin Implementations

## Purpose

Persistent architectural decisions for concrete plugin implementations and reference plugins, including CarInfo, GPS tracker, Inventar, ServiceBook and dashboard/widget-driven Vereinszeitung behavior.

## Decision History Summary

- CarInfo decisions document member-profile integration against the platform extension contracts and capture where host support was planned versus merged.
- GPS tracker, inventory and service book decisions record each plugin's boundaries, storage expectations and host integration points without folding their product-specific choices into core ADRs.
- Dashboard widget and Vereinszeitung decisions remain implementation-scoped but reference the shared plugin platform decisions for extension points and UI contribution behavior.
- Cross-plugin decisions are kept here only when they describe concrete implementations; reusable platform contracts belong in `plugin-platform.adr.md`.

## Consolidation

This ADR consolidates the previous iteration-scoped ADR files listed below. The dated decision entries are preserved under source sections so the decision history remains append-only and traceable.

Sources:

- `carinfo-member-profile.adr.md`
- `gpstracker-plugin.adr.md`
- `inventar-plugin.adr.md`
- `servicebook-plugin.adr.md`
- `dashboard-widgets-vereinszeitung.adr.md`

---

## Source: CarInfo Member Profile

Former file: `carinfo-member-profile.adr.md`


## 2026-06-12 - Foundation Rebuild Chosen Over Incremental Patch

### Decision

Slice 1 of this iteration rebuilds the Member Extension Data Foundation (the four types
documented in `ADRs/consolidated/plugin-platform.adr.md` but never merged to main): `IMemberExtensionStore`,
`PluginInvocationContext`, `PluginPermissionKeys`, and `EnsureMemberExtTableAsync`. This is treated
as a Core change under ADR 0001 GR-002. The alternative — patching CarInfo without the foundation
types — was explicitly rejected by the user.

### Rationale

Without the foundation, CarInfo cannot implement self-service vehicle CRUD correctly (the
`IsSelfService` signal would be unavailable) and the permission key convention
(`PluginPermissionKeys.MemberWrite`) would have no canonical definition. Rebuilding the foundation
in Slice 1 makes CarInfo's Slice 2 changes clean, idiomatic, and reusable by future plugins.


## 2026-06-12 - Detail Card Body Returns HTML, Rendered via Html.Raw

### Decision

`CarInfoDetailCardProvider.GetCardsAsync` returns an HTML `<ul>` string in
`MemberDetailCardSlot.Body`. `_PluginSlots.cshtml` line 47 is changed from
`@card.Card.Body` to `@Html.Raw(card.Card.Body)` and the wrapping `<p>` becomes a `<div>`.
All vehicle-supplied strings are passed through `System.Net.WebUtility.HtmlEncode` before
interpolation into the HTML string.

### Rationale

The user chose an HTML list over a plain-text summary for the detail card. `MemberDetailCardSlot.Body`
is already a plain string — no contract change is required. `Html.Raw` unescapes the HTML the plugin
intentionally emits. XSS risk is mitigated by server-side HtmlEncode on every user-supplied value
(make, color, license plate) before it enters the markup string. The `<p>` to `<div>` wrapper
change prevents invalid nesting of `<ul>` inside `<p>`.


## 2026-06-12 - Delete-Then-Add Is the Edit Flow; No Dedicated carinfo.edit Action

### Decision

There is no `carinfo.edit` action slot. Members edit a vehicle by deleting it (via `carinfo.delete`)
and re-adding it (via `carinfo.add`). The detail card's HTML list provides the current license
plates; the action modal is the edit surface.

### Rationale

The schema-driven action modal path is fully implemented and already covers both add and delete. A
dedicated edit action would require either a pre-populated form (which the modal does not support
today) or a fetch-and-repopulate flow that would require new JS. The delete-then-add pattern is
sufficient for the current UX requirement and keeps the plugin surface minimal.


## 2026-06-12 - carinfo.add and carinfo.delete Use PluginPermissionKeys.MemberWrite, Not members.manage

### Decision

The `PermissionKey` for `carinfo.add` and `carinfo.delete` action slots is changed from
`"members.manage"` to `PluginPermissionKeys.MemberWrite("clubgear.plugin.carinfo")`
(= `"clubgear.plugin.carinfo.member.write"`). `carinfo.field.define` retains `"members.manage"`.
The manifest gains `"clubgear.plugin.carinfo.member.write"` in its `Permissions` array. The core
authorization layer grants `clubgear.plugin.carinfo.member.write` to users who hold
`selfservice.profile.edit`.

### Rationale

Research confirmed (Q4) that self-service users hold `selfservice.profile.edit` and `selfservice.access`
but not `members.manage`. Without a permission key that self-service users can satisfy, every
`carinfo.add`/`carinfo.delete` call from a member would result in `PluginPermissionDeniedException`.
The ADR-planned `PluginPermissionKeys` convention is the correct fix: it scopes write access to the
specific plugin rather than granting broad `members.manage`. Admin users who hold `members.manage`
must also receive `clubgear.plugin.carinfo.member.write` (via the same grant site) so that the
admin vehicle CRUD path continues to work.


## 2026-06-12 - CarInfoDataService Continues Using host.Persistence; host.MemberData Reserved for Future Use

### Decision

`CarInfoDataService` does not migrate vehicle or field-definition storage to `host.MemberData` in
this iteration. All SQL operations continue to use `host.Persistence` (the existing
`PluginDataStore`). `CarInfoSchemaMigration` calls `EnsureMemberExtTableAsync` to provision the
`plg_carinfo_member_ext` table, but no CarInfo code reads or writes to it yet.

### Rationale

The existing SQL schema for cars, field definitions, and field values is correct and working. A
migration of that data to the key-value `member_ext` table would be a lossy transformation (the
`member_ext` table stores string key-value pairs, not typed relational rows). The `MemberData` store
is designed for simple member-scoped preferences and feature flags, not for structured multi-row data
like a vehicle list. Provisioning the table now ensures the migration is idempotent for future
adopters without forcing an unnecessary CarInfo schema change.


## 2026-06-12 - TestPluginHostContext Gains NoOpMemberExtensionStore and Default Invocation

### Decision

`TestPluginHostContext` in `CarInfoPluginSlice3Tests` gains two new property implementations:
`MemberData = new NoOpMemberExtensionStore()` (all methods are no-ops or return null/empty) and
`Invocation = default` (IsSelfService: false). A new private class `NoOpMemberExtensionStore`
implementing `IMemberExtensionStore` is added inline in the test file.

### Rationale

Adding two required properties to `IPluginHostContext` is source-breaking for all existing
implementors. The test context is the only non-production implementor. The no-op and default values
preserve all existing test assertions while satisfying the new contract without introducing any test
logic complexity.


## 2026-06-17 - carinfo.delete Switches from licensePlate to carId as Primary Key

### Decision

The `carinfo.delete` action slot's argument schema changes from accepting `licensePlate` (text) to
accepting `carId` (integer string). `CarInfoDataService` gains `DeleteCarByIdAsync(host, memberId,
carId, ct)` which resolves the car by `Id WHERE MemberId = @memberId` (ownership guard). The legacy
`DeleteCarAsync(…, licensePlate)` method is retained but is no longer called from the self-service
action path. The edit-tab text is updated to prefix each car line with `[{Id}]` so the UI can
populate the `carId` argument without a separate lookup.

### Rationale

Research confirmed (Q3/Q7) that the database `cars.Id` is a stable AUTOINCREMENT key independent of
`LicensePlate`. The new `carinfo.update` action may change a license plate, after which the old
`carinfo.delete` (keyed on plate) would fail with "not-found". Using `carId` as the delete key
makes add-update-delete sequences atomic and plate-change safe. Surfacing the `Id` in the edit-tab
text is the lowest-friction mechanism because no new API endpoint or contract change is needed.


## 2026-06-17 - carinfo.update Action Added for Self-Service Vehicle Editing

### Decision

A new `MemberActionSlot` with key `carinfo.update` is added to `CarInfoActionProvider.GetActionsAsync`
with `PermissionKey = "clubgear.plugin.carinfo.member.write"`. Its argument schema includes `carId`
(required), `make`, `color`, `licensePlate`, and all active dynamic `field.*` keys. `CarInfoDataService`
gains `UpdateCarAsync(host, memberId, carId, make, color, licensePlate, extraFields, ct)` which:
(1) verifies car ownership via `Id WHERE MemberId`, (2) checks for a plate collision with another
car of the same member before changing the plate, (3) issues a single `UPDATE cars SET … WHERE Id`,
and (4) calls the existing `UpsertFieldValuesAsync` for dynamic fields. No database migration is
needed.

### Rationale

Research confirmed (Q2) that the contract layer supports any action key — no new interface is
required. Research confirmed (Q7) that UPDATE by `Id` with a prior duplicate-plate check is the
clean strategy given the `UNIQUE INDEX` on `(MemberId, LicensePlate)`. Delete-and-reinsert was
rejected because it would change the car's `Id`, breaking any future reference to that vehicle (e.g.
external audit logs or future foreign-key additions).


## 2026-06-17 - carinfo.field.define MemberActionSlot Removed

### Decision

The `MemberActionSlot` with key `carinfo.field.define` and its handler in `CarInfoActionProvider`
are removed. Field template management is handled exclusively through `CarInfoAdminPanelProvider`
via the `field.upsert` admin panel command.

### Rationale

Research confirmed (Q6) that `carinfo.field.define` is an exact duplicate of the admin panel
command — same method call, no additional guards or logic. Keeping a duplicate action slot with
`PermissionKey = "members.manage"` in the member action panel confuses the UI surface and pollutes
the self-service action list for admins. The admin panel is the correct and already-established
surface for field template management.


## 2026-06-17 - CarInfoMemberCarsPageProvider Added for Admin Per-Member Vehicle View

### Decision

A new `CarInfoMemberCarsPageProvider` implementing `IPluginPageProvider` is added to
`CarInfoProviders.cgcs`. It is registered in `CarInfoPluginModule.RegisterContributions` via
`sink.AddPageProvider(...)`. The plugin manifest gains `"page.generic"` in its `extensionPoints`
array. The page key is `"carinfo.member-cars"`. Admins navigate to:
`/plugin-pages/clubgear.plugin.carinfo/carinfo.member-cars?entityKey={memberId}`. The `entityKey`
parameter carries the member ID as a string. `GetRowsAsync` returns one row per car for that member
with `Id`, `LicensePlate`, `Make`, `Color`, `UpdatedAtUtc`, and dynamic field columns.
`ExecuteCommandAsync` handles `car.add`, `car.edit` (entityKey = carId), and `car.delete`
(entityKey = carId), all gated by `RequiredPermission = "members.manage"`.

### Rationale

Research confirmed (Q5) that `IPluginPageProvider` is the correct existing contract for a tabular
per-member car admin view. The Inventar plugin demonstrates the exact same pattern (entityKey =
inventory number). Using the generic page mechanism avoids introducing a new host contract or view,
keeps the admin car data editable through the same modal/command infrastructure that already exists,
and aligns with the established `AddPageProvider` extension point on `IPluginContributionSink`.


## 2026-06-12 - New CarInfoSelfServiceIntegrationTests Cover Permission Key and IsSelfService Signal

### Decision

A new test class `CarInfoSelfServiceIntegrationTests` is added to
`tests/ClubGear.ArchitectureTests/`. It uses in-memory SQLite (same pattern as
`CarInfoPluginSlice3Tests`) and covers three scenarios: (1) `GetActionsAsync` with a self-service
invocation context emits the `clubgear.plugin.carinfo.member.write` permission key on the add and
delete slots; (2) `ExecuteAsync` for `carinfo.add` with a valid self-service context succeeds; (3)
`ExecuteAsync` for `carinfo.delete` with an unknown license plate returns `"not-found"`.

### Rationale

Research confirmed (Q5) that no existing test exercises the self-service vehicle CRUD path with
real plugin code. The existing `SelfServicePluginActionApiTests` uses a fake slot service. The new
tests directly instantiate `CarInfoActionProvider` and `CarInfoDetailCardProvider` against an
in-memory database with a `TestPluginHostContext` where `IsSelfService = true`, providing the
missing coverage without requiring a full ASP.NET integration test.


## Source: GPS Tracker Plugin

Former file: `gpstracker-plugin.adr.md`


## 2026-06-12 - Single-Row-Per-Member Schema with UPSERT and Hard-Delete Revoke

### Decision

The GPS Tracker plugin uses a single-row-per-member table `{prefix}tracker` with a `UNIQUE (MemberId)` constraint. Assign is an `INSERT ... ON CONFLICT(MemberId) DO UPDATE` upsert that overwrites the existing tracker. Revoke is a hard `DELETE WHERE MemberId = @memberId`. The table columns are: `MemberId INTEGER NOT NULL`, `TrackerId TEXT NOT NULL`, `Provider TEXT NOT NULL`, `IsActive INTEGER NOT NULL DEFAULT 1`, `IssuedAt TEXT NOT NULL`, `ReturnedAt TEXT NULL`.

### Rationale

The domain invariant is one active tracker per member. The UPSERT pattern is already used by CarInfo for `field_definitions` (UNIQUE on `FieldKey`) and is idiomatic in this codebase. Hard delete on Revoke matches the CarInfo vehicle delete pattern, avoids leaking PII in soft-deleted rows, and keeps the query path simple (a present row means an active assignment).


## 2026-06-12 - Permission Key: Reuse members.manage for Assign and Revoke

### Decision

Both `gpstracker.assign` and `gpstracker.revoke` action slots use `PermissionKey = "members.manage"`. No GPS-specific permission key is introduced. The manifest declares `["members.read", "members.manage"]` in its `Permissions` array.

### Rationale

Research (Q4) confirmed that introducing a GPS-specific key (e.g. `"gpstracker.manage"`) would require provisioning it as a new core permission seed — a GR-002 core change. GPS Tracker is an admin-only plugin with no self-service path, so `"members.manage"` is the correct gate. Zero core changes are required.


## 2026-06-12 - Shared Plugin Test Helpers Extracted to Helpers/PluginTestHelpers.cs

### Decision

The five private nested test helper classes from `CarInfoPluginSlice3Tests.cs` (`SqlitePluginStore`, `TestPluginHostContext`, `TestMetadataFacade`, `NoOpMemberReader`, `NoOpMemberActions`) are extracted into a new internal file `tests/ClubGear.ArchitectureTests/Helpers/PluginTestHelpers.cs`. The `SqlitePluginStore` constructor is updated to accept a `tablePrefix` parameter instead of hardcoding `"plg_carinfo_"`. `CarInfoPluginSlice3Tests` is updated to pass `"plg_carinfo_"` as the prefix argument. GPS Tracker tests pass `"plg_gpstracker_"`.

### Rationale

Without extraction, the GPS Tracker test file would duplicate ~130 lines of SQLite boilerplate. The shared file establishes a reusable plugin-test fixture convention for all future plugins. The parameterised prefix allows each test suite to use its own short name while keeping assertions prefix-agnostic via `store.GetTableName(...)`.


## 2026-06-12 - Status Badge: success Tone for Active, secondary for No Tracker

### Decision

`GpsTrackerBadgeProvider.GetBadgesAsync` returns a single `MemberStatusBadgeSlot` always. When a tracker row exists and `IsActive = 1`: `Tone = "success"`, `Label = "Tracker aktiv"`. In all other cases (no row, or row with `IsActive = 0`): `Tone = "secondary"`, `Label = "Kein Tracker"`.

### Rationale

Research (Q3) confirmed `"success"` maps to `bg-success` and `"secondary"` maps to `bg-secondary` in `_PluginSlots.cshtml`. Both are valid Bootstrap-mapped tone strings. GPS Tracker is the first production plugin to populate the `member.badge` slot. Research (Q1) confirmed the slot service and Razor view handle an empty badge list safely; a single badge is always appropriate here because the tracker status is always defined (either assigned or not).


## 2026-06-12 - Detail Card Returns HTML ul via Html.Raw

### Decision

`GpsTrackerDetailCardProvider.GetCardsAsync` returns an HTML `<ul class="list-unstyled mb-0">` string when a tracker is assigned, and `<p class="text-muted">Kein Tracker zugewiesen.</p>` when none. All string values from the database (TrackerId, Provider) are passed through `System.Net.WebUtility.HtmlEncode` before interpolation. The card body is rendered in `_PluginSlots.cshtml` via `@Html.Raw(card.Card.Body)`.

### Rationale

The `Html.Raw` rendering path was established by the previous carinfo-member-profile iteration. No further Razor view changes are needed. XSS risk is mitigated by server-side HtmlEncode on all user-supplied strings. The `<ul>` pattern is consistent with the CarInfo detail card.


## 2026-06-12 - Edit Tab Is Plain Text; Mutations Are Action-Only

### Decision

`GpsTrackerEditTabProvider` returns a plain-text summary string (TrackerId, Provider, IssuedAt, ReturnedAt when set, or "Kein Tracker zugewiesen." when not assigned). It does not render HTML or expose any form fields.

### Rationale

The edit tab in this codebase is an informational surface; mutations are handled through the action modal system (Assign / Revoke action slots). Keeping the tab as plain text avoids introducing a second `Html.Raw` surface and is consistent with CarInfo's edit tab convention.


## 2026-06-12 - IPluginDataStore Used Throughout; IMemberExtensionStore Not Used

### Decision

All GPS Tracker data access goes through `host.Persistence` (`IPluginDataStore`). `IMemberExtensionStore` / `host.MemberData` is not used.

### Rationale

Research (Q2) confirmed that `IMemberExtensionStore` is not available in the runtime until the carinfo-member-profile Foundation Rebuild (Slice 1 of the previous iteration) is fully merged. Even if available, the key-value store is designed for simple member preferences, not for structured single-row records with typed fields. Using `IPluginDataStore` with raw SQL is the idiomatic approach in this codebase.


## 2026-06-12 - Plugin Category Set Only in plugin.json, Not in C# Constructor

### Decision

`GpsTrackerPluginModule.Manifest` does not call `with { Category = "member-profile" }` in C#. The `plugin.json` includes `"category": "member-profile"`.

### Rationale

Research (Q6) confirmed that `PluginManifestParser` reads the `"category"` field from `plugin.json` and applies it via a `with` expression at activation time. When loaded through the package pipeline (the production path), the in-memory manifest will have `Category = "member-profile"` without any C# constructor change. This matches the CarInfo convention.


## 2026-06-12 - Production Table Prefix Derived from Module ID; Test Prefix Is a Short Alias

### Decision

The production table name for the tracker is `plugin_clubgear_plugin_gpstracker_tracker` (derived by `PluginSchemaNamePolicy.GetTablePrefix("clubgear.plugin.gpstracker")` = `"plugin_clubgear_plugin_gpstracker_"`). Test code uses the shortcut prefix `"plg_gpstracker_"` as a constructor argument to `SqlitePluginStore`. All test assertions use `store.GetTableName("tracker")` rather than hard-coded names.

### Rationale

Research (Q7) confirmed the production derivation formula. The test shortcut is a deliberate pattern already used in CarInfo (`"plg_carinfo_"`). Using `GetTableName` in assertions decouples the assertion text from the prefix choice and prevents drift if the prefix is ever changed in tests.


## 2026-06-12 - GPS Tracker Plugin Is Plugin-Only; Zero Core Changes

### Decision

This iteration introduces no changes to `Contracts/Plugin/`, `Services/`, `Controllers/`, or `Views/`. The only repository-level side effects are: a new `plugins/GpsTracker/` project, a new `ProjectReference` in the test `.csproj`, a new shared test helper file, and a `dotnet sln add` command.

### Rationale

The task brief states this command is "primaer plugin-only" and that any Core change requires prior user approval per ADR 0001 GR-002. All required contract surface (`IPluginDataStore`, `IMemberDetailCardProvider`, `IMemberEditTabProvider`, `IMemberStatusBadgeProvider`, `IMemberActionProvider`, `IPluginMigration`) is already present in the current codebase. No new abstractions are needed.


## Source: Inventar Plugin

Former file: `inventar-plugin.adr.md`


## 2026-06-14 - Command Sequencing: Core Before Plugin

### Decision

Commands 11 (nav contributions), 12 (generic plugin pages), and 13 (Inventar plugin) are implemented in strict order in a single run. Commands 11 and 12 are `generic-core` changes that require explicit ADR 0001 GR-002 user approval before any core file is edited. Command 13 is `plugin-only` and is unblocked only after both core commands are complete and all tests pass.

### Rationale

ADR 0001 mandates that generic-core changes be approved before implementation. Commands 11 and 12 each extend the plugin contracts assembly, the plugin runtime record, the view model, host services, and shared UI surfaces — all of which qualify as core code under ADR 0001. Implementing Command 13 before the host infrastructure exists would require either stubbing or later refactoring. The sequential dependency is explicit and enforced at the design level.


## 2026-06-14 - IPluginNavEntryService: Reads NavEntries from RegisteredPluginRuntime

### Decision

`IPluginNavEntryService.GetVisibleNavEntriesAsync` reads `NavEntries` directly from the `RegisteredPluginRuntime` record (populated at load time by `PluginLoader.PluginContributionCollector`). It does not call `module.RegisterContributions(sink)` at query time. The service iterates `IPluginRegistryReader.GetRegisteredPlugins()`, collects all `NavEntries` from every registered runtime, applies the claims-only permission filter, and returns the sorted flat list.

### Rationale

Nav entries are stable per-load: they are declared in `RegisterContributions` which is called once during plugin activation. Re-collecting at every page render would be wasteful. The data is already materialized in `RegisteredPluginRuntime.NavEntries` after the loader runs the collector. This contrasts with admin panel providers and page providers, which are re-collected at call time because the host needs to invoke the provider's async methods (which may be non-idempotent) on demand. Nav entries contain only static metadata (label, icon, route, permission key, sort order) — no async provider invocation is needed.


## 2026-06-14 - _Layout.cshtml: @inject IPluginNavEntryService for Plugin Nav Entries

### Decision

`Views/Shared/_Layout.cshtml` gains `@inject IPluginNavEntryService PluginNavEntryService`. Inside the navbar `<ul>`, after the static navigation links and before the admin dropdown, each visible nav entry is rendered as a `<li class="nav-item"><a class="nav-link" href="@entry.Route"><i class="bi @entry.Icon"></i> @entry.Label</a></li>`. The service call is `await PluginNavEntryService.GetVisibleNavEntriesAsync(User)`.

### Rationale

The layout already performs an async `UserManager.GetUserAsync` call and uses `@inject UserManager<ApplicationUser>`. Adding a second `@inject` follows an established pattern. The nav-entry list is claims-gated at the service level, so no additional view logic is needed. The Bootstrap Icons `<i class="bi @entry.Icon">` pattern is used throughout the existing admin UI. Rendering entries after static links and before the admin dropdown keeps plugin nav visually separated from core nav while remaining inside the collapsed navbar for mobile.


## 2026-06-14 - IPluginPageService: Re-collect Pattern, Inner ContributionSink with 4 Explicit Empty Bodies

### Decision

`PluginPageService` uses the same re-collect-at-call-time pattern as `PluginAdminCommandService`. Its inner `ContributionSink` explicitly implements all four abstract `IPluginContributionSink` methods (`AddRoute`, `AddService`, `AddMemberProvider`, `AddBackgroundJob`) as empty bodies, and overrides `AddPageProvider` to collect. The four core methods cannot be omitted because the interface defines them without default bodies (confirmed in research Q4).

### Rationale

Research Q4 confirmed that `PluginAdminCommandService.ContributionSink` implements all four core methods explicitly as empty bodies. The interface does not provide default implementations for them. Any `IPluginContributionSink` implementation must provide all four, making the pattern mandatory. `PluginPageService` follows this exactly.


## 2026-06-14 - PluginPageCommandRequest as Named Input Model for API Controller

### Decision

A named record `PluginPageCommandRequest(string ModuleId, string PageKey, string CommandKey, string? EntityKey, IReadOnlyDictionary<string, string> Arguments)` is the body type for `POST /api/plugin/page-commands`. It lives in the controller file (or a co-located models file) rather than in `Services/Abstractions`, since it is a transport-layer type with no host-service semantic meaning.

### Rationale

Placing transport models near their controller avoids polluting the service abstractions layer with HTTP-specific binding concerns. The existing `PluginAdminCommandRequest` (in Contracts) is a different pattern — it is passed through to the plugin provider. `PluginPageCommandRequest` is host-only and never crosses the host-plugin boundary.


## 2026-06-14 - Views/PluginPage Use Tuple View Models

### Decision

`Views/PluginPage/Index.cshtml` is typed `@model (PluginPageDefinition Definition, IReadOnlyList<PluginDataRow> Rows, string ModuleId, string PageKey)`. `Views/PluginPage/Detail.cshtml` is typed `@model (PluginPageDefinition Definition, PluginDataRow Row, string ModuleId, string PageKey)`. No dedicated view model record is created.

### Rationale

The data shapes are simple, controller-local projections. Creating named view model records for one-off controller → view transfers would add classes with no reuse value. The existing codebase uses typed view models only where the model is shared or complex (e.g., `PluginAdminStatusViewModel`). Tuple view models are idiomatic C# for lightweight projections.


## 2026-06-14 - Inventar Permission Keys: inventar.items.read and inventar.items.manage

### Decision

The Inventar plugin declares two permission keys: `inventar.items.read` (list and detail access) and `inventar.items.manage` (create, edit, delete). The `ListPermission` on `PluginPageDefinition` is `"inventar.items.read"`. Each mutating command sets `RequiredPermission = "inventar.items.manage"`.

### Rationale

Research Q5 confirmed that no `general-extension` plugin permission key convention exists beyond the flat dot-separated pattern. The existing pattern (`members.read`, `members.manage`, `admin.access`, `selfservice.access`) is feature-scoped, not module-ID-scoped. `inventar.items.read` and `inventar.items.manage` follow this pattern exactly: the prefix is the feature area (`inventar`), the suffix is the capability scope (`items.read`, `items.manage`). This is more readable than a module-ID-prefixed form (`clubgear.plugin.inventory.items.read`) and consistent with all existing permission keys.


## 2026-06-14 - Inventar Plugin: Dynamic Member Picker via GetListAsync in GetPageDefinitionAsync

### Decision

`InventarPageProvider.GetPageDefinitionAsync` calls `host.Members.GetListAsync(search: null, ct)` to obtain the full member list and maps each `PluginMemberSummary` to a `PluginFieldSchemaOption(memberId.ToString(), member.FullName)`. This produces the `Options` list for the `VerantwortlichesMitgliedId` `Select` field in the Create and Edit command schemas. The member list is fetched fresh on every call to `GetPageDefinitionAsync` — no caching.

### Rationale

Research Q7 confirmed Inventar is the first plugin to populate `PluginFieldSchemaOption` values dynamically. The contract (`IPluginMemberReader.GetListAsync`) is already the approved façade for plugin-layer member access under ADR 0001 GR-003. The member list at typical club sizes (tens to hundreds of members) is small enough that fetching it on demand is not a performance concern. Caching would require invalidation logic tied to member lifecycle events, adding complexity not justified here.


## 2026-06-14 - Inventar Plugin: Table Name Confirmation

### Decision

The single Inventar data table is named `plugin_clubgear_plugin_inventory_inventory_items` as produced by `PluginSchemaNamePolicy.GetTableName("clubgear.plugin.inventory", "inventory_items")`. A unique index on `Nummer` is added in the same migration.

### Rationale

Research Q6 traced the `NormalizeIdentifier` logic and confirmed the resulting name is 49 characters, well within SQLite limits, and not a reserved word. The index on `Nummer` enforces uniqueness of inventory item numbers at the database level, consistent with CarInfo's `UNIQUE INDEX` on `(MemberId, LicensePlate)`.


## 2026-06-14 - Inventar Plugin: Single Migration, Two Source Files

### Decision

The plugin consists of four `.cgcs` source files: `InventarPluginModule.cgcs` (module + manifest), `InventarMigrations.cgcs` (one migration class `InventarSchemaMigration` with MigrationId `"001_inventar_schema"`), `InventarData.cgcs` (internal data service and records), and `InventarProviders.cgcs` (page provider and admin panel provider). The manifest file is `plugin.json`.

### Rationale

This mirrors the CarInfo file layout (`CarInfoPluginModule.cgcs`, `CarInfoSchemaMigration.cgcs`, `CarInfoData.cgcs`, `CarInfoProviders.cgcs`, `plugin.json`). A single migration is sufficient since Inventar is a new plugin with no prior schema state to upgrade. The `.cgcs` extension follows the established plugin source-file convention.


## 2026-06-14 - ContractVersion Remains 1.4.0

### Decision

`ContractVersion.Current` is not changed from `1.4.0` in this iteration.

### Rationale

Both new interface methods (`AddNavEntries`, `AddPageProvider`) use default no-op bodies on `IPluginContributionSink`, making them purely additive at the IL level. Existing compiled plugins dispatch to the default body without recompilation. The nav-contributions ADR (2026-06-13) and generic-pages ADR (2026-06-14) both established this precedent. A version bump is not required for runtime correctness and is deferred to a dedicated housekeeping commit if needed for documentation under GR-009.


## Source: Serviceheft Plugin

Former file: `servicebook-plugin.adr.md`


## 2026-06-18 - Plugin-only Umsetzung ohne neue Core-Aenderungen

### Decision

Das Serviceheft wird als eigenstaendiges Plugin `clubgear.plugin.servicebook` umgesetzt. Es
registriert `IMemberDetailCardProvider`, `IMemberEditTabProvider` und `IMemberActionProvider`.
Controller, Core-Services, Contracts und gemeinsame Views erhalten fuer diese Funktion keine
Serviceheft-spezifischen Aenderungen.

### Rationale

Die vorhandenen Member-Slots und Member-Actions reichen fuer Anzeige und Mutationen aus. Eine
eigene Core-Route `/User/Checkheft` oder ein Checkheft-Controller wuerde die Plugin-Grenze
durchbrechen und waere nach ADR 0001 GR-002 freigabepflichtig.


## 2026-06-18 - CarInfo-Integration ueber maschinenlesbare Dateninsel

### Decision

CarInfo ab Version 1.0.5 rendert im eigenen Edit-Tab ein HTML-encodiertes
`data-carinfo-vehicles`-Attribut. Es enthaelt `id`, `make`, `color`, `licensePlate` und
`extraValues` der Fahrzeuge des aktuellen Mitglieds. Das Serviceheft liest diese Daten
clientseitig und verwendet die stabile CarInfo-ID als Fahrzeugreferenz.

### Rationale

`IPluginDataStore` erlaubt absichtlich nur Tabellen mit dem Prefix des aufrufenden Plugins.
Direkte SQL-Zugriffe des Servicehefts auf CarInfo-Tabellen wuerden diese Isolation verletzen.
Ein generischer Cross-Plugin-Read-Contract existiert nicht. Die Dateninsel bleibt vollstaendig
in Plugin-Code, veraendert keinen Core-Vertrag und stellt nur Daten bereit, die CarInfo im selben
Mitgliedskontext ohnehin sichtbar rendert.


## 2026-06-18 - Serviceheft speichert Fahrzeug-Snapshots im eigenen Schema

### Decision

Jeder Serviceeintrag speichert neben `CarId` auch Fabrikat, Farbe und Kennzeichen als Snapshot.
Alle Serviceeintraege und Teile liegen in den plugin-eigenen Tabellen `service_records` und
`service_parts`. Jede Mutation wird durch `MemberId` auf das aktuelle Mitglied begrenzt.

### Rationale

Der Snapshot erhaelt die Wartungshistorie, wenn ein Fahrzeug spaeter in CarInfo geloescht oder
sein Kennzeichen geaendert wird. Die CarInfo-ID bleibt die primaere Zuordnung fuer aktive
Fahrzeuge; der Snapshot ist die historische Darstellung und keine zweite Fahrzeugquelle.


## 2026-06-18 - Plugin-eigenes UI nutzt bestehende Member-Action-API

### Decision

Der Checkheft-Tab rendert Fahrzeugkarten, Kostenuebersicht, Historie und ein Bootstrap-Modal als
HTML/JavaScript aus dem Plugin. Schreiboperationen laufen ueber die bestehenden Endpunkte
`/api/member/plugin-actions` und `/api/self-service/plugin-actions`. Der Action-Provider
veroeffentlicht `servicebook.record.add`, `servicebook.record.update` und
`servicebook.record.delete`, alle mit
`clubgear.plugin.servicebook.member.write`.

Die generisch vom Host gerenderten Action-Buttons werden durch Plugin-CSS fuer dieses Modul
ausgeblendet, weil die Aktionen kontextgebundene Fahrzeug- und Teilewerte benoetigen und deshalb
im Serviceheft-Modal ausgeloest werden.

### Rationale

Die angehaengte Referenzoberflaeche benoetigt verschachtelte Teilelisten und eine
fahrzeugbezogene Detailansicht. Das generische Einzelfeld-Modal kann diese Struktur nicht
abbilden. Die bestehende Action-API liefert dennoch Berechtigungspruefung, Mitgliedsbindung und
Plugin-Isolation, ohne neue Controller oder Core-Sonderlogik einzufuehren.


## 2026-06-18 - Self-Service rendert Plugin-Edit-Bereiche als eigenstaendige Karten

### Decision

`_PluginSlots.cshtml` erhaelt den generischen Modus `edit-cards`. Im Self-Service wird dieser
Modus verwendet und jeder `MemberEditTabSlot` als eigene Bootstrap-Karte mit dem Slot-Titel
gerendert. Der bisherige gemeinsame Kasten mit der Ueberschrift `Plugin-Erweiterungen` und die
globalen Action-Buttons entfallen im Self-Service. Die Mitgliederverwaltung verwendet weiterhin
den bestehenden `edit`-Modus mit Tabs.

### Rationale

Fahrzeuge und Serviceheft sind fachlich eigenstaendige Profilbereiche. Eine generische
Kartenabbildung gibt jedem Plugin seinen eigenen sichtbaren Bereich, ohne CarInfo- oder
Serviceheft-Namen im Core zu hinterlegen. Die Aktionen bleiben ueber die Buttons innerhalb des
jeweiligen Plugin-Inhalts erreichbar; das gemeinsame Action-Modal wird weiterhin einmal pro Seite
bereitgestellt.


## 2026-06-30 - Mitgliederverwaltung wechselt zu edit-cards-Modus (ServiceBook = CarInfo-Erweiterung)

### Decision

`Views/Members/Edit.cshtml` Zeile 18 wird von `ViewData["PluginSlotMode"] = "edit"` auf
`ViewData["PluginSlotMode"] = "edit-cards"` geaendert. Damit werden CarInfo und ServiceBook in
der Mitgliederverwaltung unter einem gemeinsamen Bootstrap-Card mit zwei inneren Tabs
("Fahrzeuge" und "Serviceheft") dargestellt. Beide Provider setzen bereits `GroupKey = "fahrzeuge"`
und `GroupTitle = "Fahrzeuge"`. Die frueherer Entscheidung vom 2026-06-18, die Mitgliederverwaltung
im `edit`-Modus zu belassen, wird mit dieser Aenderung aufgehoben.

### Rationale

ServiceBook ist inhaltlich eine Erweiterung von CarInfo: Serviceeintraege beziehen sich immer auf
ein Fahrzeug aus CarInfo. Die gemeinsame Karte macht diese Zugehoerigkeit in der Oberflaeche
sichtbar. Die technische Voraussetzung (GroupKey-Infrastruktur in `_PluginSlots.cshtml`) war
bereits vorhanden. Kein Core-Code erhaelt plugin-spezifische Kenntnisse; die Koordination
erfolgt ausschliesslich ueber den stabilen `GroupKey`-String "fahrzeuge".

Die Detailansicht (`Details.cshtml`) bleibt unveraendert im `details`-Modus ohne Gruppierung,
da keine `GroupKey`-Infrastruktur fuer `MemberDetailCardSlot` besteht und der Mehrwert gegenueber
dem Aufwand (neue Infrastruktur, neue Rendering-Branch) nicht gerechtfertigt ist.


## 2026-06-30 - Plugin-Abhaengigkeiten ueber DependenciesJson in PluginStatusRecord

### Decision

Fuer die Aktivierungspruefstufe wird ein generischer Abhaengigkeitsmechanismus eingefuehrt:

1. `PluginDependency` ist ein neuer `sealed record` in `Contracts/Plugin/` mit `ModuleId` (string)
   und `MinVersion` (Version). `TryParse` versteht das Format `"moduleId@>=x.y.z"`.
2. `PluginManifest` erhaelt eine nicht-positionale init-only Property
   `IReadOnlyList<PluginDependency> Dependencies` mit Default `Array.Empty<PluginDependency>()`.
3. `PluginManifestParser` liest das optionale `"dependencies"`-Array aus `plugin.json` und
   validiert jedes Element via `PluginDependency.TryParse`.
4. `PluginStatusRecord` erhaelt eine neue Spalte `DependenciesJson TEXT NOT NULL DEFAULT '[]'`,
   die via idempotenter SQLite-Migration (`202606300101_AddPluginDependencies`) angelegt wird.
5. `PluginInstallerService.PersistSuccessAsync` serialisiert `manifest.Dependencies` in diese
   Spalte beim Installieren/Upgraden.
6. `PluginLifecycleService.ActivateAsync` liest `existing.DependenciesJson` vor dem Assembly-Load
   und prueft fuer jede Abhaengigkeit: (a) Ist das Plugin im Runtime-Registry?
   (b) Ist die Laufzeitversion >= MinVersion? Bei Fehlschlag wird sofort `"dependency-not-met"`
   zurueckgegeben, ohne die DB zu schreiben.
7. `ServiceBook/plugin.json` deklariert `"dependencies": ["clubgear.plugin.carinfo@>=1.0.5"]`.

### Rationale

Die Aktivierungssperre muss vor dem Assembly-Load greifen, weil `module.Manifest.Dependencies`
erst nach `LoadAsync` zugaenglich ist. Eine DB-Spalte fuer `DependenciesJson` ist daher
erforderlich. Als autoritative Pruefquelle wird `_runtimeRegistry.GetByModuleId` verwendet (nicht
`_statusStore.GetByKey`), weil ein Eintrag mit `IsActive=true` in der DB nicht garantiert, dass
das Plugin tatsaechlich im Laufzeitspeicher vorhanden ist (z.B. nach einem Ladefehlschlag beim
Start). Die Minimalversion 1.0.5 entspricht der einzigen vorhandenen Quellevidenz im Codebase
(JS-Warnung in `ServiceBookProviders.cgcs`). `LoadActivatedAsync` wird nicht veraendert; die
Abhaengigkeitssperre gilt ausschliesslich fuer explizite Admin-Aktivierungsaufrufe.
Binary-Kompatibilitaet bleibt durch die nicht-positionale init-only Property erhalten;
bestehende Plugin-Assemblies (CarInfo, Inventar) benoetigen keine Neukompilierung.


## Source: Dashboard Widgets and Vereinszeitung Plugin

Former file: `dashboard-widgets-vereinszeitung.adr.md`


## 2026-06-16 - IDashboardWidgetProvider Placed in Contracts Assembly with IPluginHostContext Parameter

### Decision

A new interface `IDashboardWidgetProvider` is added to `Contracts/Plugin/IDashboardWidgetProvider.cs`. Its single method signature is `Task<IReadOnlyList<DashboardWidgetSlot>> GetWidgetsAsync(IPluginHostContext hostContext, CancellationToken cancellationToken = default)`. The associated slot record `DashboardWidgetSlot(string Key, string Title, string Body, string? PermissionKey, int SortOrder = 0)` is co-located in the same file.

### Rationale

Every existing provider interface (`IMemberDetailCardProvider`, `IAdminFunctionPanelProvider`, `IPluginPageProvider`) takes `IPluginHostContext` as a parameter regardless of whether the provider performs simple reads or complex operations. Consistency with all existing provider interfaces is the deciding factor. `DashboardWidgetSlot` mirrors `MemberDetailCardSlot` structurally (Key, Title, Body, Order) and adds `PermissionKey` as confirmed in the task requirements. Both types belong in the contracts assembly to give plugin authors access without depending on host internals.


## 2026-06-16 - PluginWidgetProviderContribution as 10th Positional Parameter of RegisteredPluginRuntime

### Decision

`RegisteredPluginRuntime` gains a 10th positional parameter `IReadOnlyList<PluginWidgetProviderContribution> WidgetProviders`. `PluginWidgetProviderContribution(string ProviderType, int Order = 0)` is added to `PluginContributions.cs`. `IPluginContributionSink` gains `void AddWidgetProvider(PluginWidgetProviderContribution contribution) { }` with a default no-op body. `PluginLoader.PluginContributionCollector` adds a backing list and overrides the method. All 6 `RegisteredPluginRuntime` construction sites are updated in the same commit.

### Rationale

Widget contributions are structural metadata declared at load time via `RegisterContributions`, analogous to nav entries (`NavEntries` as 9th positional parameter, added in the plugin-nav-contributions iteration). Storing them in the runtime record avoids re-running `RegisterContributions` on every dashboard request. The default no-op body on `IPluginContributionSink` means existing compiled plugins dispatch to the default without recompilation. The blast radius of 6 construction sites is identical to the NavEntries addition and is accepted.


## 2026-06-16 - PluginDashboardWidgetService: Reads from RegisteredPluginRuntime.WidgetProviders, Invokes via IPluginRuntimeAdapter

### Decision

`PluginDashboardWidgetService` (in `Services/Core/`) implements `IPluginDashboardWidgetService` (in `Services/Abstractions/`). It injects `IPluginRegistryReader`, `IPluginRuntimeAdapter`, and `ILogger<PluginDashboardWidgetService>`. `GetWidgetsAsync` iterates `GetRegisteredPlugins()`, reads `runtime.WidgetProviders` from the pre-loaded runtime record, instantiates each `IDashboardWidgetProvider` via `CreateMemberProvider<IDashboardWidgetProvider>`, invokes via `InvokeAsync`, and applies a per-slot `bridge.HasPermissionAsync(slot.PermissionKey)` guard for slots with a non-null `PermissionKey`. Registered as `services.AddScoped<IPluginDashboardWidgetService, PluginDashboardWidgetService>()`.

### Rationale

Widget content (Title, Body) requires async provider invocation — it cannot be pre-computed at load time. Reading `WidgetProviders` from the runtime record (rather than re-collecting via a local `ContributionSink`) avoids calling `RegisterContributions` on every home page render, following the same reasoning as `PluginNavEntryService` for nav entries. Permission filtering at the per-slot level (not at the contribution level) is necessary because a single provider may return multiple slots with different permission requirements. `HasPermissionAsync` is used (not claims-only) to support DB-backed role-permission grants, consistent with how `PluginPageService` checks `ListPermission`.


## 2026-06-16 - DashboardWidget Extension Point Constant

### Decision

`PluginExtensionPoints.DashboardWidget = "dashboard.widget"` is added as a new `public const string` field, and `"dashboard.widget"` is added to `KnownValuesInternal`. `ContractVersion.Current` is not bumped.

### Rationale

`PluginManifestParser.ValidateExtensionPoints` calls `PluginExtensionPoints.IsKnown` for every declared extension point. Without this entry, any plugin declaring `"dashboard.widget"` (including the Vereinszeitung plugin) would be rejected at install time. Adding a constant and a HashSet entry follows the identical pattern established in plugin-nav-contributions (`NavMain`) and plugin-generic-pages (`PageGeneric`). No `ContractVersion` bump is required per the precedent set in those iterations.


## 2026-06-16 - HomeController.Index Becomes Async with IPluginDashboardWidgetService Injection

### Decision

`HomeController.Index()` is refactored from `IActionResult` to `async Task<IActionResult>`. `IPluginDashboardWidgetService` is added as a constructor dependency. The method calls `await _dashboardWidgetService.GetWidgetsAsync(User, ct)` and returns `View(new HomeIndexViewModel { Widgets = widgets })`. `HomeIndexViewModel` is a new `sealed class` in `Models/Home/` with a single `IReadOnlyList<DashboardWidgetView> Widgets` property defaulting to `Array.Empty<DashboardWidgetView>()`.

### Rationale

`HomeController` is the host's primary landing page controller. Dashboard widgets are a user-facing aggregation requiring async DB/runtime calls through the plugin adapter. Making `Index` async is the correct pattern; the controller already uses `CancellationToken` via its base class. `DashboardWidgetView` (in `Services/Abstractions/`) wraps the contracts-layer `DashboardWidgetSlot` with host metadata (`ModuleId`, `PluginDisplayName`, `SortOrder`), following the identical view-record pattern used for `MemberPluginDetailCardView` and `MemberPluginStatusBadgeView`.


## 2026-06-16 - PluginAdminStatusViewModel Gains 26th Positional Parameter WidgetProviderCount

### Decision

`PluginAdminStatusViewModel` gains a 26th positional parameter `int WidgetProviderCount` after `NavEntryCount`. The construction site in `PluginAdminQueryService` passes `runtime?.WidgetProviders.Count ?? 0`. The 4 test construction sites pass `0`.

### Rationale

All existing runtime contribution counts (Routes, Services, MemberProviders, BackgroundJobs, NavEntries) are surfaced in the admin status view model as scalars. Widget provider count follows the same pattern for observability. Consistency with the existing scalar-count pattern is the sole deciding factor.


## 2026-06-16 - Vereinszeitung Plugin: Two Migrations, Three Providers, plugin.json Declares Three Extension Points

### Decision

The Vereinszeitung plugin lives in `plugins/Vereinszeitung/` and consists of four `.cgcs` source files plus `plugin.json`. It declares permissions `["vereinszeitung.read", "vereinszeitung.manage"]` and extension points `["dashboard.widget", "page.generic", "nav.main"]`. Two migrations create `plg_vereinszeitung_ausgaben` and `plg_vereinszeitung_artikel`. Three contributions are registered: `VereinszeItungWidgetProvider` (for `dashboard.widget`), `VereinszeItungPageProvider` (for `page.generic`), and a nav entry pointing to `/plugin/clubgear.plugin.vereinszeitung/ausgaben` with `RequiredPermission = "vereinszeitung.manage"`.

### Rationale

`IPluginPageProvider` is the correct admin-management pattern for Ausgaben CRUD (established in plugin-generic-pages). `IDashboardWidgetProvider` delivers the self-service Ausgaben view to the home dashboard. Nav entry follows the Inventar pattern (`sink.AddNavEntries`) for the admin link. Two separate migrations are used because `plg_vereinszeitung_artikel` has a foreign key to `plg_vereinszeitung_ausgaben`; the parent table must exist before the child table is created. `vereinszeitung.read` gates the widget (members need it to see published issues); `vereinszeitung.manage` gates the page provider and nav entry (admins need it for CRUD).


## 2026-06-16 - PluginDashboardWidgetService Permission Guard: bridge.HasPermissionAsync per Slot

### Decision

For each `DashboardWidgetSlot` returned by a provider, if `slot.PermissionKey` is non-null, `PluginDashboardWidgetService` calls `bridge.HasPermissionAsync(slot.PermissionKey, ct)`. Slots that fail the check are excluded from the result. Slots with a null `PermissionKey` are included unconditionally. `CreateBridge` is called once per runtime (reused across all slots from that runtime's providers).

### Rationale

Dashboard widgets may carry different permission requirements than the page-level check. A provider might return multiple slots with distinct keys. Per-slot checking is the only way to handle this correctly. Reusing the bridge per runtime avoids creating multiple bridge instances for the same module within a single request. This mirrors the slot-level permission pattern used for member actions in `MemberPluginSlotService`.


## 2026-06-16 - Architecture Tests: PluginBoundaryTests Extended, HomeDashboardRenderTests Added

### Decision

`PluginBoundaryTests.ContractsAssembly_ShouldExpose_HostContext_AndMemberReadModels` gains two new assertions: `Assert.Same(typeof(IPluginModule).Assembly, typeof(IDashboardWidgetProvider).Assembly)` and `Assert.Same(typeof(IPluginModule).Assembly, typeof(DashboardWidgetSlot).Assembly)`. A new file `HomeDashboardRenderTests.cs` is added with tests verifying that `Views/Home/Index.cshtml` contains `id="dashboardWidgets"` and `Model.Widgets`. A new `VereinszeItungPluginSliceTests.cs` covers manifest, contribution registration, widget and CRUD round-trips, and disk manifest validity.

### Rationale

Boundary tests enforce that widget contract types do not escape the contracts assembly — a mandatory guard under ADR 0001. `HomeDashboardRenderTests` follows the `MemberIndexSectionsRenderTests` pattern for view-structure regression tests. Vereinszeitung slice tests follow `InventarPluginSliceTests` in structure and use an in-memory SQLite fixture, which is the established pattern for plugin data-layer tests.
