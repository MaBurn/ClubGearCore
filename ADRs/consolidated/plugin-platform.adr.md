# ADR: Plugin Platform

## Purpose

Persistent architectural decisions for the generic ClubGear plugin platform: categories, extension points, permissions, admin lifecycle, member extension data, navigation, generic pages, member filters, tab grouping, self-service dashboard hooks and technical provider contracts.

## Decision History Summary

- Plugin discovery and classification decisions define categories, extension point metadata and admin visibility as platform concepts instead of per-plugin UI conventions.
- Permission and lifecycle decisions place authorization, deletion risk, admin operations and role checks in host-managed services where possible, with plugins contributing metadata and handlers.
- Member-facing extension decisions cover extension data, member profile navigation, plugin tabs, grouped tabs, generic pages, filters and self-service dashboard integration as separate but related host extension surfaces.
- Technical provider decisions define opt-in contracts for notification channels, audit sinks, background jobs and identity provider integration while keeping plugins isolated behind stable interfaces.

## Consolidation

This ADR consolidates the previous iteration-scoped ADR files listed below. The dated decision entries are preserved under source sections so the decision history remains append-only and traceable.

Sources:

- `plugin-categories.adr.md`
- `extension-point-catalog.adr.md`
- `plugin-permissions-admin.adr.md`
- `plugin-admin-delete-detail-risk.adr.md`
- `member-extension-data.adr.md`
- `plugin-nav-contributions.adr.md`
- `plugin-generic-pages.adr.md`
- `plugin-member-filter.adr.md`
- `plugin-tab-grouping.adr.md`
- `selfservice-plugin-dashboard.adr.md`
- `notification-channel-provider.adr.md`
- `audit-sink-plugins.adr.md`
- `background-job-runner.adr.md`
- `idp-plugin-interface.adr.md`

---

## Source: Plugin Categories

Former file: `plugin-categories.adr.md`


## 2026-06-09 - Category Property Added as Init-Only, Not Positional

### Decision

`PluginManifest.Category` is added as a non-positional `init`-only property with a default value of `"General"`, not as a new parameter in the positional primary constructor.

### Rationale

`PluginManifest` is a `sealed record` in the Contracts package consumed by external plugin assemblies. Adding a new required positional parameter would be a breaking binary change for all external assemblies that call `new PluginManifest(...)` with positional arguments. An init-only property with a default leaves the primary constructor signature unchanged, making the addition backward-compatible with pre-compiled plugins.


## 2026-06-09 - Category Default Assigned at Parse Time, Not Installer or Query Layer

### Decision

`PluginManifestParser` is the sole layer that assigns the `"General"` default when `"category"` is absent from `plugin.json`. No fallback logic is duplicated in `PluginInstallerService` or `PluginAdminQueryService`.

### Rationale

This follows the established precedent set by `author` and `license` optional fields, which are both assigned their defaults (`"Unknown"` and `"Unspecified"`) in `PluginManifestParser` via `ReadOptionalString(...) ?? <default>`. Centralising the default in the parser means that anywhere a `PluginManifest` is produced the property is already populated with a valid, non-null value.


## 2026-06-09 - Category Persisted in DB Column, Not Derived at Runtime

### Decision

A new `Category TEXT NOT NULL DEFAULT 'General'` column is added to the `PluginStatusRecords` table via an additive SQLite migration. `PluginStatusRecord` gains the corresponding `Category` property, and `PluginInstallerService.PersistSuccessAsync` maps `manifest.Category` to it.

### Rationale

For installed-but-not-loaded plugins, the runtime module is unavailable, so `runtimeModule?.Manifest.Category` cannot supply the value. Without DB persistence, the Admin UI would show `"General"` for all such plugins regardless of what their manifest declared at install time. Persisting the column ensures the declared category is faithfully displayed at query time for all plugin states. The established pattern for additive schema changes in this codebase is an idempotent `ALTER TABLE ... ADD COLUMN ... DEFAULT` migration, which is exactly what `AddPluginPackagePath_202605310151` does.


## 2026-06-09 - Category Surfaced as Badge in Plugin Column, Not New Table Column

### Decision

The category is rendered as a small Bootstrap `badge bg-info` inside the existing "Plugin" `<td>` cell in `_PluginStatusTable.cshtml`, alongside the existing `DisplayName`, `ModuleId`, and `Author·License` lines. No new `<th>` column is added and no row-grouping section headers are introduced.

### Rationale

The table already has seven columns. The existing "Plugin" cell already displays three stacked pieces of metadata. Adding a badge there requires no structural changes to the table, no view-model grouping, and no changes to the `<thead>`. A grouped/section-header layout would require breaking the flat `@foreach` and pre-grouping the model; that is a larger view change not justified by the current requirements. The badge approach is consistent with how the Status column renders multiple states and can be extended to grouping in a future iteration if needed.


## 2026-06-09 - ContractVersion Bumped to 1.3.0 (Minor)

### Decision

`ContractVersion.Current` advances from `1.2.0` to `1.3.0`.

### Rationale

`PluginManifest` is part of the public Contracts package. Adding a new observable property (`Category`) to the record is a backward-compatible additive change. Under semantic versioning, additive changes to a public API surface warrant a minor version bump. A patch bump (`1.2.1`) would be inappropriate because the API surface changes. No existing plugin's `requiredCoreVersion` (e.g. `">=1.0.0"`) is broken by this increment; the compatibility validator still passes them.


## Source: Extension Point Catalog

Former file: `extension-point-catalog.adr.md`


## 2026-06-10 - Extension-Point Constants Placed in Contracts Assembly as Public

### Decision

A new public static class `PluginExtensionPoints` is added to `Contracts/Plugin/PluginExtensionPoints.cs` in the `ClubGear.Plugin.Contracts` namespace. All seven known extension-point string values are defined as `public const string` fields. A `public static IReadOnlySet<string> All` property and a `public static bool IsKnown(string)` method expose the catalog for consumers. `ContractVersion.Current` advances from `1.3.0` to `1.4.0`.

### Rationale

The task describes extension-points as "stable contract values." Plugin authors already reference `Contracts/Plugin` for every other stable API surface (`IPluginModule`, `IPluginContributionSink`, `PluginManifest`, etc.). Placing the constants there gives plugin authors a typed reference to the known values, eliminating scattered inline string literals. An `internal` placement or a `Services/Plugins`-only class would deny plugin authors access to the constants, defeating the purpose of a published catalog. The versioning cost (minor bump to `1.4.0`) is appropriate under the established ADR policy: adding a new public type to the Contracts assembly is an additive API surface change. The pattern mirrors `PermissionKeys.cs` (const fields + private HashSet + public read-only accessor + query method) for consistency within the codebase.


## 2026-06-10 - Extension-Point Validation Added to PluginManifestParser, Not Runtime Loader

### Decision

Allowlist validation of the `extensionPoints` array is performed inside `PluginManifestParser.Parse()`, immediately after `ReadStringArray`. Each value is checked against `PluginExtensionPoints.IsKnown`. An unrecognized value appends an error message and causes the parse result to be invalid. The runtime contribution sink (`PluginLoader`, `PluginContributionCollector`) is not changed.

### Rationale

The manifest parser is already the single choke-point for all manifest contract enforcement (required fields, version format, `requiredCoreVersion` syntax). Putting allowlist validation there ensures that a plugin with an unrecognized extension-point string is rejected at install time, before any assembly loading or DB writes occur. Enforcing the same check at the runtime loader would require adding a cross-reference between contribution types and extension-point strings, which would be a larger, more invasive change not justified by current requirements. The parser validation is sufficient to make the catalog a binding contract.


## 2026-06-10 - Category Allowlist Validation Explicitly Deferred

### Decision

The `category` field in `plugin.json` continues to accept any non-empty string without validation. No allowlist check is added in this iteration.

### Rationale

The task scope is the extension-point catalog. The existing test `ZipInstall_PersistsCategory_FromManifest_OnFailure` deliberately uses an arbitrary category value (`"vehicle-data"`) to verify that an incompatible-version install still persists the declared category. Adding category allowlist validation now would break that test and would require either updating the test fixture to a known category or adding `"vehicle-data"` to a category catalog, both of which are out of scope. Category validation is a natural follow-on iteration.


## 2026-06-10 - CarInfo Manifest Updated to Declare member.badge

### Decision

`plugins/CarInfo/plugin.json` and `plugins/CarInfo/CarInfoPluginModule.cgcs` are updated to add `"member.badge"` to the `extensionPoints` array. The `selfservice.profile` declaration is retained.

### Rationale

CarInfo registers a `PluginMemberSlotKind.StatusBadge` provider via `AddMemberProvider` but its manifest omitted `"member.badge"`. Once parser validation is active this would cause CarInfo's own manifest to fail. The fix aligns the manifest declaration with the actual contribution. `selfservice.profile` maps to `PluginExtensionPoints.SelfServiceProfile` (a recognized constant declared for future use) and is valid to retain as a forward-looking declaration of intent.


## 2026-06-10 - Plugin Authoring Guide Extended with Extension-Point Catalog Table

### Decision

A new Section 4a is inserted into `docs/architecture/plugin-authoring-guide.md` between the existing Section 4 (Manifest-Datei) and Section 5 (Erlaubte Host-Interaktion). It contains a table mapping each extension-point string to its category, provider interface or contribution method, and the `PluginExtensionPoints` constant name. The section also documents that unknown values are rejected at install time and that `selfservice.profile` is reserved.

### Rationale

The guide currently mentions extension-point strings only as opaque examples in two code snippets. No prose or table explains what each string means, which interface it corresponds to, or that an allowlist exists. Plugin authors cannot write a compliant manifest without this information. The new section closes that gap and makes the guide the authoritative reference for the catalog, consistent with how the guide already documents permissions, contribution types, and packaging requirements.


## Source: Plugin Permissions Admin

Former file: `plugin-permissions-admin.adr.md`


## 2026-06-11 - CorePermissionDefinitionProvider Derives Description and Category from PluginStatusRecord

### Decision

`CorePermissionDefinitionProvider.GetPermissions()` is changed to produce a
`PermissionDefinition` with `Description = "{record.DisplayName}: {permissionKey}"`
and `Category = record.Category` for each plugin-declared permission key.
The `IPermissionDefinitionProvider` interface and the `PermissionDefinition`
record are not modified.

### Rationale

`PluginStatusRecord` already carries `DisplayName` and `Category` (the latter
added in the prior plugin-categories iteration via migration
`202606090101_AddPluginCategory`). Deriving richer metadata at the provider
level — rather than changing `PluginManifest.Permissions` from `IReadOnlyList<string>`
to a richer object type — avoids a breaking change to the public Contracts
package and keeps the enrichment confined to core infrastructure. The
`PermissionDefinition` record's existing three fields (`Key`, `Description`,
`Category`) are sufficient to carry the enriched data without any interface
change.


## 2026-06-11 - PermissionSeedTask Changed from Insert-Only to Upsert

### Decision

`PermissionSeedTask.SeedAsync` is updated to load existing `AppPermission` rows
as a keyed dictionary and, in addition to inserting missing rows, update the
`Description` and `Category` columns on rows where the definition differs from
what is stored. A single `SaveChangesAsync` call covers both inserts and updates.

### Rationale

The prior insert-only strategy was correct when descriptions were always generic
(all plugin permissions shared the same template). Now that descriptions and
categories carry plugin-specific data that can change on upgrade or reinstall,
the seed task must synchronize them. This is the least-invasive path: no new
table, no new seed task, no migration. The upsert logic mirrors the pattern used
by `DbPluginStatusStore.UpsertAsync`.


## 2026-06-11 - PluginUninstallService Cleans Up AppPermission and AppRolePermission Rows

### Decision

`PluginUninstallService.UninstallAsync` is extended to delete the uninstalled
plugin's `AppPermission` rows and all associated `AppRolePermission` rows within
the same `ApplicationDbContext` save call as the status-record deletion. The
permission keys to delete are read from `record.PermissionsJson` before the
status record is removed. Core permission keys (checked via
`PermissionKeys.IsCorePermission`) are excluded from deletion as a safety guard.

### Rationale

Research confirmed that `PluginUninstallService` does not currently delete
permission rows (Q3 finding). Leaving these rows causes the role-permissions
admin view to display permissions for plugins that no longer exist, breaking the
view's correctness. Cleaning them up atomically with the status record is safe
because `PermissionKeys.IsCorePermission` prevents accidental removal of core
permissions, and the `AppRolePermission` rows being deleted have no other
consumers that would be harmed by their removal (role membership is unaffected;
`DatabasePermissionService` simply won't find a matching row for a key that no
longer has a live plugin).


## 2026-06-11 - Role-Permission UI as Standalone Controller, Not Tab in Admin/Functions

### Decision

A new standalone `RolePermissionsController` at route `Admin/RolePermissions`
is created rather than adding a new tab inside `Admin/Functions`. The view lives
at `Views/Admin/RolePermissions/Index.cshtml`.

### Rationale

The `Admin/Functions` view is already a multi-tab page with seven tabs and has
no existing tab infrastructure for write operations on role assignments. Adding
an eighth tab with grant/revoke forms would require changes to
`AdminFunctionsViewModel`, the controller action, and the view's tab list and
pane rendering — all for functionality that is logically unrelated to the
existing config, maintenance, logging, and plugin-panel tabs. A standalone
controller follows the established pattern of `PluginAdminController` (dedicated
page for plugin lifecycle operations) and keeps concerns separated. The
navigation link added to `_Layout.cshtml` makes the page discoverable without
embedding it inside an unrelated tab container.


## 2026-06-11 - Admin Wildcard Guard in Revoke Action

### Decision

`RolePermissionsController.Revoke` enforces a hard guard that prevents revoking
the `*` (Wildcard) permission from the `ClubGear.Admin` role. Any revoke attempt
for this specific combination is rejected with an error feedback and no DB write.

### Rationale

`ClubGear.Admin` is the only role that grants access to the admin UI itself via
the `Wildcard` permission. If the Wildcard were revoked from Admin through the
new UI, the currently signed-in administrator would immediately lose access to
all admin pages including `RolePermissions/Index`, with no self-service recovery
path. The guard mirrors the principle of protecting the bootstrap admin account
used elsewhere in the codebase (`DatabasePermissionService.HasPermissionAsync`
checks for the `master-admin` system claim as a superuser bypass).


## 2026-06-11 - No New DB Migration Required for This Iteration

### Decision

This iteration adds no new database columns or tables. All changes operate on
existing `AppPermissions` and `AppRolePermissions` tables.

### Rationale

The `AppPermission` model already has `Key`, `Description`, and `Category`
columns. The `AppRolePermission` model already has `RoleName` and `PermissionKey`.
The richer metadata from `PluginStatusRecord` is projected into these existing
columns by the updated seed task on first run, requiring no schema change.


## 2026-06-11 - Orphaned AppRolePermission Rows from Previously Uninstalled Plugins Not Purged

### Decision

`AppRolePermission` rows orphaned by plugins that were uninstalled before this
iteration are not proactively cleaned up by this iteration. They remain in the
database and are excluded from the role-permissions view because the view query
joins through `AppPermission` (which only contains live plugin keys after this
iteration's uninstall cleanup is in place).

### Rationale

A bulk cleanup of all historic orphans would require identifying which
`AppRolePermission` rows reference keys absent from `AppPermission` and deleting
them — a data-loss operation that should be explicit and auditable. The orphaned
rows are harmless to `DatabasePermissionService.HasPermissionAsync` (a missing
permission key simply produces a `false` result) and invisible to the new
role-permissions UI. A dedicated data cleanup migration can be scheduled
separately if required.


## Source: Plugin Admin Delete, Detail, and Activation Risk Check

Former file: `plugin-admin-delete-detail-risk.adr.md`


## 2026-06-10 - New IPluginUninstallService Abstraction, Not Extension of IPluginLifecycleService

### Decision

Uninstall logic is placed in a new `IPluginUninstallService` interface and `PluginUninstallService` implementation rather than adding an `UninstallAsync` method to the existing `IPluginLifecycleService`.

### Rationale

`IPluginLifecycleService` is concerned with runtime activation state (load, activate, deactivate). Uninstall is a destructive multi-step write path (deactivate, DB row delete, optional table teardown, filesystem cleanup) that crosses more boundaries than a lifecycle state transition. Separating the concern avoids growing the lifecycle service into a Swiss-army-knife class and keeps each abstraction testable in isolation. The delete controller action will depend only on `IPluginUninstallService`, while the existing `Activate`/`Deactivate` actions continue depending only on `IPluginLifecycleService`.


## 2026-06-10 - DeleteAsync Added to IPluginStatusStore and IPluginPackageStore

### Decision

`IPluginStatusStore` gains `Task DeleteAsync(string key, CancellationToken ct = default)`. `IPluginPackageStore` gains `Task DeleteAsync(string pluginKey, CancellationToken ct = default)`. Both existing implementations are extended to match.

### Rationale

Research confirmed that no delete path exists anywhere in the codebase for either abstraction. Adding the method to the interface is the correct extension point rather than leaking raw `ApplicationDbContext` or `Directory.Delete` calls into an orchestrating service that should not own those concerns. This follows the established pattern: the store interfaces own all DB and filesystem I/O for their respective resources.


## 2026-06-10 - Data Removal Uses Raw SQL DROP TABLE via sqlite_master Inspection

### Decision

When `removeData: true` is requested, `PluginUninstallService` queries `sqlite_master` for all table names that start with the plugin's `TablePrefix` value (read from `PluginMigrationStates`), then issues `DROP TABLE IF EXISTS` for each found table inside a transaction, before removing the `PluginMigrationState` rows with EF `RemoveRange`.

### Rationale

`PluginSchemaNamePolicy.GetTablePrefix` produces a deterministic prefix (`plugin_{normalised_key}_`). However, the plugin may have created multiple tables with that prefix; the exact set of table names is not recorded separately—only migration IDs are recorded. Inspecting `sqlite_master` is the only reliable way to discover all tables for a prefix without requiring plugins to declare a teardown manifest. The approach mirrors how the existing `ValidateSql` method in `PluginSchemaNamePolicy` operates on table names at write time. All teardown SQL runs in a single transaction so a partial failure does not leave the migration state rows removed but tables intact.


## 2026-06-10 - Signature Status Excluded From This Iteration

### Decision

No `SignatureVerified` boolean column or signer identity field is added to `PluginStatusRecord` or `PluginAdminStatusViewModel` in this iteration.

### Rationale

Research confirmed that the signer public key PEM is passed to `PluginInstallerService`, used only within `IPluginIntegrityVerifier.Verify`, and then discarded. Persisting the outcome would require a new `PluginStatusRecord` column, an idempotent schema migration, and a change to `PluginInstallerService.PersistSuccessAsync`. The task specifies "Signaturstatus" in the context of the detail view but the research shows there is no data to display without first building that new persistence path. Adding an empty or misleading "unbekannt" field adds noise without value. `PackageHash` (already stored) is surfaced instead as the integrity artifact and is sufficient for identifying the package at rest.


## 2026-06-10 - AppliedMigrationCount Is Computed at Query Time, Not Stored

### Decision

`PluginAdminStatusViewModel` gains an `int AppliedMigrationCount` field populated by a synchronous `.Count()` EF LINQ call on `PluginMigrationStates` inside `PluginAdminQueryService.GetPluginStatus` and `GetPluginStatuses`.

### Rationale

The migration count is a live aggregate that can change each time a plugin is activated and new migrations are run. Storing it as a column on `PluginStatusRecord` would require updating it at activation time, introducing a write-side coupling. Computing it at read time from `PluginMigrationStates` is authoritative and avoids any synchronisation risk. The `GetPluginStatus` method is already synchronous (mirrors the synchronous store `GetByKey`/`List` pattern), so using synchronous `.Count()` is internally consistent with the existing pattern rather than introducing async for a single scalar aggregation.


## 2026-06-10 - Activation Risk Check Implemented as Client-Side window.confirm, Not Server-Side GET Confirmation View

### Decision

The activation risk confirmation for `category == "technical"` plugins is implemented by adding a `data-confirm-if-technical="@plugin.Category"` attribute to the Activate form and extending `plugin-admin.js` to show `window.confirm(...)` when this attribute equals `"technical"`. No new GET action, no confirmation view, and no intermediate redirect are added.

### Rationale

Research confirmed that `Activate` is already a direct `POST` with no intermediate step. Introducing a server-side GET confirmation view would require: a new controller action, a new partial or full view, and a two-step round-trip. The codebase already uses `data-confirm` on the Deactivate form with `window.confirm` as the interstitial guard—this is the established convention. Reusing the same client-side pattern for the technical-plugin risk check is consistent with the existing UX, requires only a one-line addition to `plugin-admin.js`, and avoids adding a new controller surface that has no business logic of its own. The confirmation guards against accidental activation; it is not a security gate, so client-side is appropriate.


## 2026-06-10 - Delete Button Visible Only When Plugin Is Installed and Inactive

### Decision

The Delete action button in `_PluginStatusTable.cshtml` is rendered only when `plugin.IsInstalled && !plugin.IsActive`.

### Rationale

Deleting an active plugin while it is loaded in the runtime would leave the in-process module in an inconsistent state (registered routes and services pointing at an unloaded context). The service layer enforces this guard (it calls `DeactivateAsync` first), but the UI pre-emptively hides the button for active plugins to avoid presenting an action that would either fail or require an implicit deactivation the admin has not acknowledged. If the admin wants to delete an active plugin they must deactivate it first. This is the least-surprise flow.


## 2026-06-10 - No New Schema Migration File Required for This Iteration

### Decision

No new `Data/Migrations/` file is needed and `ApplicationSeeder.EnsureSqliteSchemaCompatibilityAsync` is not modified.

### Rationale

All new data surfaced in the detail view (`PackageHash`, `AppliedMigrationCount`) comes from columns and tables that already exist (`PackageHash` on `PluginStatusRecords`, `PluginMigrationStates`). The uninstall path only deletes rows and plugin-prefixed tables, it does not alter or create schema objects in the core `ApplicationDbContext`. Therefore no additive migration is required for this iteration.


## Source: Member Extension Data

Former file: `member-extension-data.adr.md`


## 2026-06-11 - IMemberExtensionStore Added as a New Contract Interface in Contracts/Plugin/

### Decision

A new public interface `IMemberExtensionStore` is added to `Contracts/Plugin/IMemberExtensionStore.cs` in the `ClubGear.Plugin.Contracts` namespace. It exposes five methods: `WriteAsync`, `ReadAsync`, `ReadAllAsync`, `DeleteAsync`, and `DeleteAllAsync`. All methods are scoped implicitly to the calling plugin's own table by the host implementation. The interface is exposed as a new `MemberData` property on `IPluginHostContext`.

### Rationale

The only existing mechanism for member-scoped data access is raw SQL via `IPluginDataStore.QueryAsync` / `ExecuteAsync` with a manually composed `WHERE MemberId = @memberId` clause (the CarInfo pattern). This approach is correct but forces every plugin to reinvent the same boilerplate and to understand the SQL schema. A typed, named-parameter API on top of the same underlying `PluginDataStore` / `PluginSchemaNamePolicy` stack gives member-profile plugins a first-class, discoverable surface without introducing any new persistence engine or bypassing the existing isolation guardrails. The existing `PluginSchemaNamePolicy.ValidateSql` continues to enforce the per-plugin table-prefix restriction on every SQL call made through `PluginMemberExtensionStore`.


## 2026-06-11 - PluginInvocationContext Added to IPluginHostContext; IsSelfService Signal Exposed to Plugins

### Decision

A new `readonly record struct PluginInvocationContext(bool IsSelfService, int? SelfServiceMemberId)` is added to `Contracts/Plugin/PluginInvocationContext.cs`. `IPluginHostContext` gains a new `Invocation` property of this type. `PluginRuntimeAdapter.CreateBridge` is extended with an optional `PluginInvocationContext invocation = default` parameter (default: `{ IsSelfService = false, SelfServiceMemberId = null }`). `MemberPluginSlotService.GetSlotsAsync` gains a corresponding optional parameter and passes the value through when the call originates from `SelfServiceApiController`.

### Rationale

Research confirmed (Q2) that plugins have no way to distinguish admin-initiated from self-service-initiated invocations. The `ClaimsPrincipal` is held privately in `PluginRuntimeBridge._user` and is never surfaced to plugins. A dedicated value-type signal on the host context is the minimal, backward-compatible addition: the default value produces current behavior for all existing call sites. Plugins that do not consume `Invocation` are unaffected. The struct is placed in Contracts because it is part of the plugin-visible API surface; it does not introduce any dependency on ASP.NET or `ClaimsPrincipal`.


## 2026-06-11 - Per-Plugin Member Permission Keys Follow moduleId.member.read / moduleId.member.write Convention

### Decision

A new static class `PluginPermissionKeys` is added to `Contracts/Plugin/PluginPermissionKeys.cs`. It defines two static factory methods: `MemberRead(string moduleId)` returning `"{moduleId}.member.read"` and `MemberWrite(string moduleId)` returning `"{moduleId}.member.write"`. These are not registered as core permission keys in `PermissionKeys.cs`. Plugins declare them in their `Permissions` manifest array and enforce them via the existing `bridge.HasPermissionAsync` path.

### Rationale

Research confirmed (Q3) that no `{moduleId}.member.read` or `{moduleId}.member.write` convention exists today. CarInfo uses only core permission keys (`members.read`, `members.manage`, `selfservice.profile.edit`). Placing the convention in Contracts as a static helper prevents key-naming drift across plugins while keeping the keys out of the core `PermissionKeys` static class (which defines only host-owned, system-wide permissions). The enforcement pathway — `IExtensionPermissionFacade.HasPermissionAsync` checking the plugin's declared permissions — is unchanged.


## 2026-06-11 - EnsureMemberExtTableAsync Extension Method Added to IPluginMigration.cs

### Decision

A public static extension method `EnsureMemberExtTableAsync(this IPluginMigrationContext, CancellationToken)` is added directly inside `Contracts/Plugin/IPluginMigration.cs`. It issues a `CREATE TABLE IF NOT EXISTS` for the `{prefix}member_ext` table with `(MemberId INTEGER NOT NULL, FieldKey TEXT NOT NULL, Value TEXT NULL, UpdatedAtUtc TEXT NOT NULL, PRIMARY KEY (MemberId, FieldKey))`. The host does not create this table automatically at activation time.

### Rationale

Plugins that choose to use `MemberData` must own their schema, consistent with GR-004 (plugin data is isolated; plugin migrations are tracked separately from core). Having plugins call a single shared helper rather than hand-crafting the DDL every time ensures schema consistency across all adopters. The host does not create the table automatically because not all plugins will use `MemberData`; automatic creation would generate unused tables for plugins that rely only on custom schemas. The extension method belongs in Contracts because it is part of the plugin authoring API.


## 2026-06-11 - ContractVersion Bumped from 1.4.0 to 1.5.0

### Decision

`Contracts/Plugin/ContractVersion.cs` is updated: `Current` advances from `new Version(1, 4, 0)` to `new Version(1, 5, 0)`. `MinimumSupported` remains `new Version(1, 0, 0)`.

### Rationale

This iteration adds three new public types to the Contracts assembly (`IMemberExtensionStore`, `PluginInvocationContext`, `PluginPermissionKeys`) and modifies one existing public interface (`IPluginHostContext`, gaining two new properties). Per the established policy (mirroring the extension-point-catalog ADR), adding new public types and interface members to the Contracts assembly warrants a minor version bump. All changes are additive; no existing method signature is removed or modified. Existing plugin assemblies compiled against `1.4.0` remain loadable.


## 2026-06-11 - No Core Database Migration Required for This Iteration

### Decision

No new `ApplicationDbContext` migration is added. The `plugin_{moduleId}_member_ext` table is exclusively created by plugin-provided `IPluginMigration` implementations using the `EnsureMemberExtTableAsync` helper.

### Rationale

Research confirmed (Q5) that all existing plugin tables are created through `IPluginMigration` with no FK into `Members` and no registration in `ApplicationDbContext.OnModelCreating`. This iteration follows the same pattern. The SQLite foreign-key pragma is not enabled by the host, so adding `REFERENCES Members(Id)` would provide no enforcement; a bare `INTEGER MemberId` is sufficient and consistent with the CarInfo schema.


## Source: Plugin Navigation Contributions

Former file: `plugin-nav-contributions.adr.md`


## 2026-06-13 - New Value Type: PluginNavEntry in Contracts

### Decision

A new sealed record `PluginNavEntry(string Label, string Icon, string Route, string? RequiredPermission, int SortOrder)` is added to `Contracts/Plugin/PluginNavEntry.cs` in the `ClubGear.Plugin.Contracts` namespace.

### Rationale

Plugins must be able to declare navigation entries at contribution time (inside `RegisterContributions`), not at runtime discovery. Placing the type in the contracts assembly ensures the type is available to both the host and plugin assemblies without the plugin taking a dependency on host internals. The record is immutable (sealed record) to match all other contribution types in the contracts assembly.


## 2026-06-13 - Default Interface Method for AddNavEntries

### Decision

`IPluginContributionSink` receives a new method with a default no-op body:
`void AddNavEntries(IReadOnlyList<PluginNavEntry> entries) { }`

### Rationale

The existing `AddAdminPanelProvider` method uses the same pattern. Because `PluginAssemblyLoadContext.Load()` explicitly redirects the contracts assembly to `AssemblyLoadContext.Default`, every plugin load context shares the same contracts assembly instance from the host. The default-body IL is always resolved from the host's copy. Existing compiled plugin DLLs that do not call or implement `AddNavEntries` dispatch to the default no-op body without recompilation. No version bump is required for runtime correctness.


## 2026-06-13 - NavMain Extension Point Constant

### Decision

`PluginExtensionPoints.NavMain = "nav.main"` is added as a public constant and registered in `KnownValuesInternal`.

### Rationale

`PluginManifestParser.ValidateExtensionPoints` calls `PluginExtensionPoints.IsKnown(value)` for every declared extension point during manifest parsing. Without `"nav.main"` in the known set, any plugin manifest declaring this extension point would fail validation with an error. The constant is added to both the `const` field and the `KnownValuesInternal` HashSet simultaneously.


## 2026-06-13 - RegisteredPluginRuntime: 9th Positional Parameter

### Decision

`RegisteredPluginRuntime` gains a ninth positional parameter `IReadOnlyList<PluginNavEntry> NavEntries`. All 6 construction sites are updated: the production path in `PluginLoader.cs` passes `collector.NavEntries`; the 5 test-only sites pass `Array.Empty<PluginNavEntry>()` or `sink.NavEntries` as appropriate.

### Rationale

`RegisteredPluginRuntime` is the single in-memory record representing a loaded plugin's runtime state. All existing contribution collections (`Routes`, `Services`, `MemberProviders`, `BackgroundJobs`) are stored here. Nav entries follow the same pattern. There is no `with`-expression shortcut that avoids updating construction sites — each site must be updated. The blast radius is 6 sites, all in the same repository.


## 2026-06-13 - IPluginNavEntryService: Claims-Only Permission Pre-Filter

### Decision

`IPluginNavEntryService.GetVisibleNavEntriesAsync(ClaimsPrincipal, CancellationToken)` filters nav entries using a claims-only check: `user.Claims.Any(c => c.Type == "permission" && (c.Value == entry.RequiredPermission || c.Value == PermissionKeys.Wildcard))`. No async DB call is made. The layout renders every entry returned without additional checks.

### Rationale

The layout is rendered on every page load. `DatabasePermissionService.HasPermissionAsync` makes async DB calls (`_userManager.GetUserAsync`, `_dbContext.RolePermissions.AnyAsync`). Running that per nav entry on every page request is disproportionate overhead for a nav bar. The existing layout already uses claims-only scanning for `hasAdminPermissionClaim` (lines 46-50 of `_Layout.cshtml`). The `"permission"` claim type is the same one `DatabasePermissionService` checks first (line 37 of `DatabasePermissionService.cs`) before falling back to the DB. This is the established fast path for in-request permission checks in ClubGear. Wildcard (`PermissionKeys.Wildcard`) is honored identically. `IPluginNavEntryService` is registered as `AddScoped` (not singleton) to match the layout's per-request scope and to be consistent with other permission-aware services.


## 2026-06-13 - Route Format: Absolute Path String

### Decision

`PluginNavEntry.Route` is a plain `string` containing an absolute path (e.g., `/inventar`). The host renders `<a href="@entry.Route">` with no further processing.

### Rationale

Plugins like Inventar or Vereinszeitung own their own route registration via `AddRoute`. They know their absolute paths. Using a plain string requires no reflection, no route helper invocation at nav render time, and no dependency on the host's routing infrastructure from the contracts layer. This is the simplest contract that meets the requirement.


## 2026-06-13 - PluginAdminStatusViewModel: NavEntryCount as Scalar, Detail Card for Structured List

### Decision

`PluginAdminStatusViewModel` gains a 25th positional parameter `int NavEntryCount`. The "Status & Laufzeit" card in `Detail.cshtml` shows the scalar count. A new full-width "Navigation" card in `Detail.cshtml` shows the structured list (Label, Icon, Route, Berechtigung, Reihenfolge) sourced from `IPluginRegistryReader` injected directly into the view. If the plugin is not runtime-registered, the card shows a "not active" notice.

### Rationale

All existing contribution types (Routes, Services, MemberProviders, BackgroundJobs) are surfaced as scalars in the Status card, consistent with the current design. The structured nav-entry list is a new addition specific to nav contributions; it follows the same card pattern used for Permissions and Extension Points. The view injects `IPluginRegistryReader` directly rather than expanding `PluginAdminStatusViewModel` with a `IReadOnlyList<PluginNavEntry>` property, keeping the view model a pure data-transfer record without holding domain types from the contracts namespace in its public surface.


## 2026-06-13 - Contract Version Not Bumped

### Decision

`ContractVersion.Current` remains at `1.4.0`. No version bump accompanies this iteration.

### Rationale

`ContractCompatibilityService.Validate` checks only major version parity and minimum version floor. Adding `AddNavEntries` with a default no-op body is purely additive at the IL level. Existing plugins compiled against `1.0.0`–`1.4.0` continue to load without recompilation. A minor bump to `1.5.0` is not required for runtime correctness. If a bump is later mandated as a documentation convention under ADR 0001 GR-009, it should be applied in a dedicated housekeeping commit.


## Source: Plugin Generic Pages

Former file: `plugin-generic-pages.adr.md`


## 2026-06-14 - IPluginPageProvider as the Contract Interface for Generic List/Detail Pages

### Decision

A new interface `IPluginPageProvider` is added to `Contracts/Plugin/IPluginPageProvider.cs`. It defines three methods: `GetPageDefinitionAsync` (returns `PluginPageDefinition` describing columns, commands, and permissions), `GetRowsAsync` (returns `IReadOnlyList<PluginDataRow>` with optional `filterValue` and `entityKey` parameters), and `ExecuteCommandAsync` (accepts `commandKey`, optional `entityKey`, and a string dictionary of arguments; returns `PluginCommandResult`). The interface does not extend any base interface; `IPluginProvider` does not exist in the codebase.

### Rationale

The existing `IAdminFunctionPanelProvider` pattern proves that a single provider interface with a definition-query method and a command-execution method is sufficient for schema-driven mutation flows. Reusing `PluginCommandResult` and `PluginFieldError` from `IAdminFunctionPanelProvider.cs` avoids a parallel result type. Passing `entityKey` as a nullable string to both `GetRowsAsync` and `ExecuteCommandAsync` covers both list mode (null entity key) and detail/row-action mode (non-null entity key) without adding a separate interface or overload.


## 2026-06-14 - PluginPageDefinition Carries ListPermission and FilterPlaceholder

### Decision

`PluginPageDefinition` is a `sealed record` with positional parameters: `PageKey`, `Title`, `EntityKeyColumn`, `IReadOnlyList<PluginPageColumn> Columns`, `IReadOnlyList<PluginPageCommand> Commands`, `string? ListPermission`, and `string? FilterPlaceholder`. A null `ListPermission` means any authenticated user can access the list. A null `FilterPlaceholder` means no filter input is rendered.

### Rationale

Embedding `ListPermission` in the definition record (rather than requiring the provider to enforce access itself) keeps permission enforcement in the host service, consistent with how `PluginAdminPanel.PermissionKey` is checked by `PluginAdminCommandService.GetPanelsAsync` before the provider's output is returned to the caller. `FilterPlaceholder` being nullable lets the provider opt out of filtering without requiring the host to implement a separate "does this page support filtering?" query.


## 2026-06-14 - PluginPageCommand Carries RequiredPermission and RequiresEntityKey

### Decision

`PluginPageCommand` is a `sealed record` with `Key`, `Label`, `Icon?`, `RequiredPermission?`, `IReadOnlyList<PluginFieldSchema>? ArgumentSchema`, and `bool RequiresEntityKey`. `RequiresEntityKey = false` marks list-level commands (e.g. Create); `true` marks row-level commands (e.g. Edit, Delete). `ArgumentSchema` reuses the existing `PluginFieldSchema` type from `PluginSchemaContracts.cs`.

### Rationale

The two-level permission pattern (page-level `ListPermission` + per-command `RequiredPermission`) mirrors the `PluginAdminPanel.PermissionKey` + `PluginAdminCommandDescriptor.PermissionKey` pattern confirmed in the research. `RequiresEntityKey` as a boolean flag on the command record lets the view render list-level buttons once at the top and row-level buttons once per row without additional host logic. Reusing `PluginFieldSchema` avoids a new schema type and ensures the existing modal JS (`renderField`, `applyFieldErrors`) works without modification.


## 2026-06-14 - Extension Point Constant PageGeneric = "page.generic"

### Decision

`PluginExtensionPoints.PageGeneric = "page.generic"` is added as a new `public const string` field in `Contracts/Plugin/PluginExtensionPoints.cs`, and `"page.generic"` is added to `KnownValuesInternal`. `ContractVersion.Current` is not bumped.

### Rationale

Adding a constant to `KnownValuesInternal` is required for `PluginManifestParser.ValidateExtensionPoints` to accept manifests that declare this extension point. Without it, any plugin declaring `"page.generic"` would be rejected at install time. The nav-contributions ADR (2026-06-13) established that additive default-body methods and new extension-point constants do not require a `ContractVersion` bump. This iteration follows the same precedent.


## 2026-06-14 - AddPageProvider as a Default No-op Method on IPluginContributionSink

### Decision

`IPluginContributionSink` gains one new default no-op method: `void AddPageProvider(PluginPageProviderContribution contribution) { }`. `PluginPageProviderContribution` is a new `sealed record(string ProviderType, int Order = 0)` added to `PluginContributions.cs`.

### Rationale

This follows the identical pattern used for `AddAdminPanelProvider` (confirmed as implemented in production). The default no-op body means existing `IPluginContributionSink` implementations (including `PluginLoader.PluginContributionCollector`) compile without changes. Page provider contributions are re-collected at query time by `PluginPageService` via a local `ContributionSink` that overrides the default body, exactly as `PluginAdminCommandService` does for admin panel providers. `RegisteredPluginRuntime` is not extended; all 6 existing construction sites remain positional and unchanged.


## 2026-06-14 - PluginPageService Mirrors PluginAdminCommandService Pattern

### Decision

`PluginPageService` (in `Services/Core/`) implements `IPluginPageService` (in `Services/Abstractions/`). It injects `IPluginRegistryReader`, `IPluginRuntimeAdapter`, and `ILogger<PluginPageService>`. On each call it creates a local `ContributionSink`, runs `module.RegisterContributions(sink)`, then iterates contributions to find the matching `IPluginPageProvider`. No caching of contributions or definitions is applied. Registered via `services.AddScoped<IPluginPageService, PluginPageService>()` in `ServiceCollectionExtensions.cs`.

### Rationale

The re-collect-at-call-time pattern is the established norm for provider-type contributions. Caching page definitions would require invalidation logic tied to plugin lifecycle events, adding complexity not justified by current load requirements. The scoped lifetime aligns with all other service registrations in `AddClubGearCoreServices`.


## 2026-06-14 - PluginPageResult<T> as the Service Return Envelope

### Decision

A generic `sealed record PluginPageResult<T>(T? Value, bool NotFound, bool Forbidden, string? ErrorMessage)` is introduced in `Services/Abstractions/IPluginPageService.cs`, with static factory helpers `PluginPageResult.Ok<T>`, `PluginPageResult.NotFound<T>`, and `PluginPageResult.Forbidden<T>`. `IPluginPageService` methods for `GetPageDefinitionAsync` and `GetRowsAsync` return `Task<PluginPageResult<...>>`. `ExecuteCommandAsync` returns `Task<PluginCommandResult>` directly (reusing the existing type).

### Rationale

The controller needs to distinguish "module not found" (404), "permission denied" (403), and "success" (200) without catching exceptions. Wrapping results in a discriminated record avoids exception-based control flow in the controller while keeping the service API strongly typed. `PluginCommandResult` already carries a discriminating `Status` string, so it serves as its own envelope for command execution and does not need wrapping.


## 2026-06-14 - Separate Razor Controller and API Controller for Plugin Pages

### Decision

Two files are created under `Controllers/PluginPage/`:
- `PluginPage_Controller.cs` — MVC `Controller` subclass with `[Authorize]`, no class-level permission attribute; routes `GET /plugin/{moduleId}/{pageKey}` (Index) and `GET /plugin/{moduleId}/{pageKey}/{entityKey}` (Detail).
- `PluginPage_API.cs` — `ControllerBase` subclass with `[ApiController]`, `[Authorize]`, no class-level permission attribute; routes `POST /api/plugin/page-commands` (ExecuteCommand).

Per-page and per-command permissions are enforced inside `PluginPageService`, not via `[PermissionAuthorize]` on the controller class.

### Rationale

The existing codebase splits Razor view controllers and JSON API controllers into separate files for each feature area (e.g., `Member_Controller.cs` / `Member_API.cs`). This design follows that convention. Placing no `[PermissionAuthorize]` at the class level is intentional: page access is controlled by the plugin-declared `ListPermission` (which may be null, meaning any authenticated user), not a static host permission key. Enforcing this in the service keeps the controller thin and ensures the permission check applies consistently regardless of how the service is called.


## 2026-06-14 - _PluginPageActionModal.cshtml as a Parallel Partial (No Modification to Existing Modal)

### Decision

A new partial view `Views/PluginPage/_PluginPageActionModal.cshtml` is created. It is untyped (no `@model` directive). Its JavaScript listens for `data-plugin-page-action` click events and posts to `/api/plugin/page-commands` with payload `{ moduleId, pageKey, commandKey, entityKey?, arguments: {} }`. The field-rendering logic (`renderField`, `applyFieldErrors`, `clearModalErrors`) is reimplemented inline (or extracted to a shared JS function via a new `<script>` partial if the Builder judges duplication excessive). The existing `Views/Members/_PluginActionModal.cshtml` is not modified.

### Rationale

`_PluginActionModal.cshtml` is typed `@model MemberPluginSlotSnapshot` and its payload always includes `memberId`. Making it generic would require changing its model binding, the payload shape, and the guard flag name (`clubGearMemberPluginActionsBound`), all of which risk breaking the existing member-context flows. A parallel partial eliminates coupling. The field-rendering JavaScript is already ~100 lines and is logically independent of the endpoint or payload shape; it can be safely duplicated or extracted without cross-contamination.


## 2026-06-14 - Column Rendering as Plain Text String Lookup

### Decision

In `Views/PluginPage/Index.cshtml`, each table cell is rendered as `@(row.Values.GetValueOrDefault(col.Key) ?? string.Empty)`, plain text with no HTML encoding risk beyond Razor's default escaping. No typed formatting, no hyperlink generation for individual cells.

### Rationale

`PluginDataRow.Values` is a `Dictionary<string, string?>` whose values are all `Convert.ToString(...)` outputs from SQLite — invariant-culture strings or null. There is no richer type information available. The user confirmed option A1: simple generic table, column key looks up string value, everything plain text. Razor auto-escapes the string, so XSS is not a concern.


## 2026-06-14 - Detail View Reuses GetRowsAsync Filtered by EntityKey

### Decision

`IPluginPageProvider.GetRowsAsync` with a non-null `entityKey` is the mechanism for loading the single row shown on the detail page. No new contract method (`GetRowAsync`, `GetDetailAsync`) is introduced. The host takes `rows[0]` and renders it as a `<dl>` key-value card.

### Rationale

The user confirmed option B1. Adding a separate `GetRowAsync` method would duplicate intent and force every provider implementation to implement an additional interface member even when `GetRowsAsync` with `entityKey` is sufficient. The `IReadOnlyList<PluginDataRow>` return type already handles the "not found" case naturally: an empty list means the controller returns 404.


## Source: Plugin Member Filter

Former file: `plugin-member-filter.adr.md`


## 2026-06-13 - Core Scope: Full Generic-Core Pipeline (ADR 0001 GR-002 Approved)

### Decision

Implement the full generic-core plugin member filter pipeline with all six core changes: `IPluginMemberFilterProvider` contract, `PluginMemberSlotKind.MemberFilter` enum value, `PluginExtensionPoints.MemberFilter` constant, `MemberSearchFilterViewModel.PluginFilters` dictionary, `IPluginMemberFilterService` (dedicated service), controller wiring, and dynamic `_SearchAndFilters.cshtml` rendering.

### Rationale

The generic-core approach means any future plugin can declare filterable fields without requiring a core code change. The alternative (ad-hoc per-plugin controller logic) would not scale beyond one plugin and would require touching MembersController for every new plugin filter. All six core changes are load-bearing: the contract defines the plugin API; the enum and extension point constant make the contribution type discoverable; the view model extension carries filter state through POST redirect; the dedicated service isolates filter aggregation logic; and the Razor partial renders the dynamic UI. ADR 0001 GR-002 approval was obtained before implementing.


## 2026-06-13 - Filter State Carrier: Dictionary on MemberSearchFilterViewModel

### Decision

Add `Dictionary<string, string> PluginFilters { get; set; }` (initialized to empty, `StringComparer.OrdinalIgnoreCase`) to `MemberSearchFilterViewModel`. Extend `RedirectToIndex` to forward each key-value pair as `pluginFilters[key]` route values. Extend the POST `Index` action to bind the dictionary from the submitted form.

### Rationale

`MemberSearchFilterViewModel` is the established filter state carrier. Keeping plugin filters in the same model makes the GET/POST/Redirect cycle consistent with how `Search` and `Status` already flow. The alternative of binding plugin filters as a separate `[FromQuery] Dictionary<string, string>` parameter on the GET action would require a parallel pass-through path and split the filter state across two objects, increasing complexity and test surface. A single flat `Dictionary<string, string>` with dot-separated keys (e.g., `carinfo.hasAnyCar`) is sufficient for the current and foreseeable plugin filter set.


## 2026-06-13 - CarInfo Reference Filter Scope: Has-Any-Car + Make=X on Static Schema

### Decision

The CarInfo reference implementation provides exactly two `PluginMemberFilterDefinition` entries: `carinfo.hasAnyCar` (checkbox, `AcceptsValue = false`) and `carinfo.make` (text input, `AcceptsValue = true`, label "Marke eingeben"). Both queries target only the static `plugin_clubgear_plugin_carinfo_cars` table. Dynamic schema (`field_definitions` / `field_values`) is explicitly out of scope for this iteration.

### Rationale

The static schema columns (`MemberId`, `Make`) are available in all CarInfo installations and require a single-table query, making them the lowest-risk reference implementation. Dynamic field filtering requires a two-table join and runtime knowledge of active `FieldKey` values, which adds complexity without demonstrating the generic filter pipeline. The two chosen filters together demonstrate both the boolean/presence filter pattern (`AcceptsValue = false`) and the value-input filter pattern (`AcceptsValue = true`), which is sufficient to validate the full contract.


## 2026-06-13 - Filter Evaluation Point: Post-Query In-Memory Set Intersection

### Decision

Plugin filters are evaluated after `IMemberFeatureService.GetListAsync` returns the full (text-search-filtered) member list. Each active plugin filter provider returns a `HashSet<int>` of matching member IDs. The host intersects this set with the running candidate list in memory. The final list is the intersection of all active plugin filter results.

### Rationale

The existing architecture already applies the `status` filter in-memory after the EF Core query returns. Post-query intersection is consistent with this established pattern and avoids coupling the core EF query to plugin-owned SQLite tables. No production member count target is documented; the architecture is suitable for club-scale deployments. If future scale requires pre-query SQL joins, the `IPluginMemberFilterProvider.GetMatchingMemberIdsAsync` contract can be augmented with an `IQueryable<Member>` overload without breaking existing providers.


## 2026-06-13 - SQL Safety: ValidateSql + Parameterized Queries Cover Filter Pattern

### Decision

Plugin filter SQL executes exclusively through `IPluginDataStore.QueryAsync`, which runs `PluginSchemaNamePolicy.ValidateSql` before any execution and builds parameterized `DbParameter` objects for all user-supplied values. No additional SQL injection guardrail is required for the filter pattern.

### Rationale

`ValidateSql` already checks that every referenced table name starts with the plugin's own prefix. Parameterized queries through `IPluginDataStore.QueryAsync` prevent injection of user-supplied filter values (e.g., the `make` string). The `EnsureIsolated` check in `PluginRuntimeAdapter` blocks plugins from bypassing `IPluginDataStore` by accessing `ApplicationDbContext` or core namespaces directly. The combination of these three guards is sufficient.


## 2026-06-13 - Service Location: Services/Plugins/ Namespace

### Decision

`PluginMemberFilterService.cs` is placed in `Services/Plugins/` (namespace `ClubGear.Services.Plugins`), not in `Services/Core/` where `MemberPluginSlotService.cs` lives. `IPluginMemberFilterService` is placed in `Services/Abstractions/`.

### Rationale

`PluginMemberFilterService` is plugin infrastructure: it iterates the plugin registry, invokes the runtime adapter, and aggregates plugin-provided results. This responsibility is analogous to other plugin infrastructure services (`PluginMigrationRunner`, `PluginAdminQueryService`) that live in `Services/Plugins/` sub-namespaces. `MemberPluginSlotService` in `Services/Core/` is a core feature service consumed by `MembersController`; the filter service plays the same consumer-facing role but its primary logic is plugin dispatch. Placing it in `Services/Plugins/` reinforces that boundary.


## 2026-06-13 - RegisteredPluginRuntime Record Extension Strategy

### Decision

`RegisteredPluginRuntime` gains a new optional positional parameter `IReadOnlyList<PluginMemberProviderContribution> MemberFilterProviders = []` appended at the end of the record constructor. The `PluginContributionCollector` in `PluginLoader.cs` gains a `_memberFilterProviders` list and exposes it as `MemberFilterProviders`. `IPluginContributionSink` gains `AddMemberFilterProvider(PluginMemberProviderContribution contribution)`. Existing call sites that construct `RegisteredPluginRuntime` positionally (in `PluginLoader.cs` and test fixtures) must be updated; the default value makes the parameter optional when using named arguments.

### Rationale

The filter contribution type is structurally identical to a `PluginMemberProviderContribution` — it carries a `SlotKind`, a `ProviderType`, and an `Order`. Reusing the existing record and sink method shape minimizes new surface area and keeps the `PluginMemberSlotKind.MemberFilter` enum value as the discriminator. A separate record type (e.g., `PluginMemberFilterContribution`) would duplicate the same fields without benefit. The `AddMemberFilterProvider` sink method name makes the contribution intent explicit in plugin module code.


## 2026-06-13 - IPluginMemberFilterProvider Does Not Inherit a Base Interface

### Decision

`IPluginMemberFilterProvider` is defined as a standalone interface with no base interface. The user prompt referenced `IPluginProvider` as a base type, but no such interface exists in the codebase. All existing provider interfaces (`IMemberDetailCardProvider`, `IMemberEditTabProvider`, `IMemberStatusBadgeProvider`, `IMemberActionProvider`) are also standalone. The filter provider follows the same pattern.

### Rationale

Introducing a marker base interface `IPluginProvider` solely for `IPluginMemberFilterProvider` would be a broader contract change not warranted by this iteration. The existing providers have been type-safe and discoverable without a common base. `CreateMemberProvider<TProvider>` in `IPluginRegistryReader` already handles type-safe provider instantiation via generic constraints.


## Source: Plugin Tab Grouping

Former file: `plugin-tab-grouping.adr.md`


## 2026-06-18 - GroupKey/GroupTitle Added as Non-Positional Init Properties on MemberEditTabSlot

### Decision

Two optional, non-positional `init`-only properties — `GroupKey` (string?) and `GroupTitle`
(string?) — are added to the `MemberEditTabSlot` sealed record in
`Contracts/Plugin/IMemberEditTabProvider.cs`. Corresponding properties are added to the host-side
view model `MemberPluginEditTabView` in `Services/Abstractions/IMemberPluginSlotService.cs` and
are populated by `CollectEditTabsAsync` in `MemberPluginSlotService`.

### Rationale

Adding non-positional init properties to a C# positional record does not change the positional
constructor arity. Plugin assemblies compiled against the previous version of the contract DLL
continue to load and run without modification (binary compatibility is preserved). All existing
call sites using `new MemberEditTabSlot("key", "Title", html)` or the four-argument form continue
to compile and produce correct results; `GroupKey` and `GroupTitle` default to null, which the
partial interprets as "no group" and renders as a solo flat card (unchanged from previous
behaviour). This approach requires no contract versioning, no assembly retargeting, and no test
changes.


## 2026-06-18 - Grouping Is Resolved Entirely in _PluginSlots.cshtml (edit-cards Branch)

### Decision

The `edit-cards` branch of `Views/Members/_PluginSlots.cshtml` is the sole site where grouping
logic is applied. `MemberPluginSlotService.GetSlotsAsync` does not group, sort within groups, or
otherwise change its output contract. The partial groups the already-sorted `EditTabs` list using
LINQ `GroupBy` on `GroupKey`, preserving the insertion order of groups (first-occurrence of each
key determines group order).

### Rationale

Keeping grouping in the partial satisfies all existing architecture test constraints without any
test changes:
(a) `Profile.cshtml` does not change, so the assertion that it does not contain
    "Plugin-Erweiterungen" and does contain `ViewData["PluginSlotMode"] = "edit-cards"` passes.
(b) No host file gains a `carinfo`-specific string.
(c) The outer `<div data-plugin-slot="edit-cards">` wrapper asserted by `MemberPluginActionTests`
    is unchanged.
Moving grouping into the service would require adding a new output type or changing
`MemberPluginSlotSnapshot`, which would widen the blast radius to all consumers of the service.


## 2026-06-18 - CarInfo and ServiceBook Share GroupKey "fahrzeuge" with GroupTitle "Fahrzeuge"

### Decision

`CarInfoEditTabProvider` sets `GroupKey = "fahrzeuge"` and `GroupTitle = "Fahrzeuge"` on its
`MemberEditTabSlot`. `ServiceBookEditTabProvider` sets the same `GroupKey = "fahrzeuge"` and
`GroupTitle = "Fahrzeuge"`. The CarInfo tab title remains `"Fahrzeuge"` (used as the inner tab
label); the ServiceBook tab title remains `"Serviceheft"` (used as the inner tab label).

### Rationale

A shared, stable `GroupKey` string is the coordination mechanism between two independently loaded
plugins. The key is meaningful, lowercase, and kebab-safe; it does not need to match any database
identifier or route segment. Both plugins set an identical `GroupTitle` because the partial uses
only the first entry's `GroupTitle` as the card heading — if the two differed, the displayed
heading would depend on sort order, which is undesirable. Enforcing agreement is a plugin-author
convention; the host does not validate it.


## 2026-06-18 - Grouped Cards Use Bootstrap Nav-Tabs Inside a Single card-body

### Decision

When the partial detects two or more tabs sharing the same `GroupKey`, it renders a single
`<section class="card border-light-subtle mb-3">` with a `<ul class="nav nav-tabs">` strip followed by a
`<div class="tab-content">`. This mirrors the Bootstrap tab structure already used in the `edit`
mode (`_PluginSlots.cshtml` lines 60–101), making the HTML patterns consistent. Solo tabs
(null `GroupKey` or no peer with the same key) continue to render the flat layout unchanged.

### Rationale

Reusing the `edit`-mode Bootstrap tab structure requires no new CSS or JavaScript. Bootstrap's
tab initialisation is data-attribute-driven (`data-bs-toggle="tab"`), so no additional JS wiring
is needed. The `document.querySelector('[data-carinfo-vehicles]')` call in
`ServiceBookProviders.cgcs` queries the full document DOM and is unaffected by tab-pane
visibility toggling (`d-none` / `show active` are CSS-only and do not remove elements from the
DOM tree).


## Source: SelfService Plugin Dashboard

Former file: `selfservice-plugin-dashboard.adr.md`


## 2026-06-18 - Plugin DetailCards Wired into SelfService Index Dashboard

### Decision

`SelfServiceController.Index` is extended to call `_memberPluginSlotService.GetSlotsAsync` when a
member is linked, populating `ViewData["MemberPluginSlots"]`, `ViewData["PluginActionEndpoint"]`
(`/api/self-service/plugin-actions`), and `ViewData["PluginSlotMode"]` (`details`) before returning
the view. `Views/SelfService/Index.cshtml` reads the snapshot and renders the existing shared
partial `~/Views/Members/_PluginSlots.cshtml` in `details` mode when `DetailCards.Count > 0`,
inside a new `selfservice-dashboard-plugins` section. A "Profil verwalten" link directs members to
the Profile page for editing. No plugin code, no shared partials, and no architecture tests are
modified.

### Rationale

The SelfService Profile page already provides full plugin editing via `edit-cards` mode. The missing
piece is that the Index dashboard gives no indication that plugin data (vehicles, ServiceBook
entries) exists for the member. The `details` mode of `_PluginSlots.cshtml` renders only
`DetailCards` and `StatusBadges` — the read-only summary surface that is appropriate for a
dashboard. Routing mutations (add/edit/delete) remain on the Profile page where the `edit-cards`
mode and `_PluginActionModal.cshtml` are already wired. This avoids duplicating the action modal
infrastructure on the dashboard and keeps the Index page as a read/navigate-only surface consistent
with its current design intent.


## 2026-06-18 - Inline Slot Population in Index, Not via PopulatePluginViewDataAsync

### Decision

The Index action populates plugin ViewData inline rather than reusing the existing private helper
`PopulatePluginViewDataAsync`. The Index action already holds the `SelfServiceDashboardOutcome`
(including `Member`) from its own `GetDashboardAsync` call. Reusing the helper would cause a second
`GetDashboardAsync` call, redundantly fetching the same member record.

### Rationale

`PopulatePluginViewDataAsync` was designed for the Profile GET and POST actions, which do not
perform a prior `GetDashboardAsync` call. The Index action's control flow is different: it calls
`GetDashboardAsync` first and branches on the result. Extracting a version that accepts an already-
resolved `Member` would be premature abstraction for a two-site pattern. Inline population keeps the
code readable and avoids the redundant service call.


## Source: Notification Channel Provider

Former file: `notification-channel-provider.adr.md`


## 2026-06-16 - INotificationChannelProvider Placed in Contracts Assembly with Contracts-Only Types

### Decision

A new interface `INotificationChannelProvider` is added to `Contracts/Plugin/INotificationChannelProvider.cs`. Its properties and method use only Contracts-assembly types: `PluginNotificationMessage` (mirrors `NotificationMessage` from `Services.Abstractions` but defined in Contracts) and `PluginNotificationResult` (mirrors `NotificationResult`). The interface is not `INotificationChannel` re-exported — it is a separate Contracts-level type.

### Rationale

`INotificationChannel` lives in `ClubGear.Services.Abstractions`, a forbidden namespace for plugin code per `PluginRuntimeAdapter.EnsureIsolated` and `PluginBoundaryTests`. Plugins must only reference `ClubGear.Plugin.Contracts`. Introducing a parallel `INotificationChannelProvider` in Contracts preserves the boundary. The host maps between the two representations in `NotificationService`. This mirrors the existing pattern where `PluginRuntimeNotification` / `PluginRuntimeNotificationResult` in Contracts mirror `NotificationMessage` / `NotificationResult` in Services.Abstractions.


## 2026-06-16 - Dynamic Plugin Channel Lookup via IPluginNotificationChannelService (Re-collect-at-call-time)

### Decision

A new Scoped service `IPluginNotificationChannelService` / `PluginNotificationChannelService` is introduced. It reads `ChannelProviders` from `RegisteredPluginRuntime` (pre-loaded at plugin activation time) and instantiates the matching provider via `IPluginRegistryReader.CreateMemberProvider<INotificationChannelProvider>` at send-time. `NotificationService` injects this service and falls back to it when the core `_channels` dictionary does not contain the requested channel name. No singleton registry or DI container rebuild is needed.

### Rationale

`NotificationService` builds `_channels` once at Scoped construction from DI-enumerated `IEnumerable<INotificationChannel>`. Plugins cannot be inserted into this dictionary post-startup without rebuilding the DI container. Two candidate patterns were considered: (a) a singleton `IPluginNotificationChannelRegistry` that `NotificationService` queries alongside the static dict, or (b) re-collect-at-call-time reading from `RegisteredPluginRuntime.ChannelProviders`. Pattern (b) is chosen because it mirrors the established `PluginDashboardWidgetService` and `PluginPageService` patterns in this codebase, avoids introducing a new singleton with mutable state, and is consistent with how all other plugin contribution types (page providers, widget providers) are resolved. The performance cost of `CreateMemberProvider` per send is accepted because notification sends are infrequent and not on hot paths.


## 2026-06-16 - PluginNotificationChannelContribution as 10th Positional Parameter of RegisteredPluginRuntime

### Decision

`RegisteredPluginRuntime` gains a 10th positional parameter `IReadOnlyList<PluginNotificationChannelContribution> ChannelProviders`. `PluginNotificationChannelContribution(string ProviderType, int Order = 0)` is added to `PluginContributions.cs`. `IPluginContributionSink` gains `void AddNotificationChannelProvider(PluginNotificationChannelContribution contribution) { }` with a default no-op body. `PluginLoader.PluginContributionCollector` adds a backing list and overrides the method. All 9 `RegisteredPluginRuntime` construction sites are updated in the same commit (1 production site in `PluginLoader.cs`, 8 test sites).

### Rationale

Channel contributions are structural metadata declared at load time via `RegisterContributions`, analogous to `NavEntries` (9th positional parameter). Storing them in the runtime record avoids re-running `RegisterContributions` on every notification send. The default no-op body on `IPluginContributionSink` means existing compiled plugins dispatch to the default without recompilation. The blast radius of 9 construction sites is consistent with prior iterations (`NavEntries` addition touched a similar set of sites).


## 2026-06-16 - NotificationService: Core Channels Take Priority; Plugin Channel as Fallback

### Decision

`NotificationService.NotifyAsync` first checks the static `_channels` dictionary (core DI-registered channels). Only on a miss does it call `_pluginChannelService.FindChannel(message.Channel)`. If the plugin channel is found, its `SendAsync` is called with type mapping and wrapped in a `try/catch` for error isolation. The failure is written to `NotificationRecord` with `Status=Failed` and does not propagate to the caller. If no channel is found at all (core or plugin), the existing "Channel nicht registriert" failure path is taken.

### Rationale

Core channels (SMTP, InApp) are registered via DI and form the primary set. Plugin channels extend this set dynamically. Priority ordering (core first) prevents a plugin from shadowing a core channel by registering the same `ChannelName`. Error isolation per-channel is a confirmed requirement: a plugin channel failure must not block delivery via other channels or throw to the caller. The `try/catch` in `NotificationService` (not in the plugin) is the correct location because the plugin cannot be trusted to catch its own exceptions.


## 2026-06-16 - Tracing Convention: Channel Field as "plugin:{moduleId}" Prefix in NotificationRecord

### Decision

Plugin channel providers are expected to set their `ChannelName` to a value prefixed with `plugin:` (e.g., `"plugin:matrix"`, `"plugin:webhook"`). This convention is documented in the contract but not enforced at runtime. The `NotificationRecord.Channel` field stores whatever `ChannelName` the provider returns, giving administrators a filtering anchor.

### Rationale

`NotificationRecord` has no `ModuleId` column and adding one would require a schema migration, which was ruled out. The `Channel` field is the only per-row identifier that can encode plugin origin. Using a `plugin:` prefix convention (without schema enforcement) follows the same pattern as `loadContext.Name = $"plugin:{record.Key}"` already used in `PluginLoader`. A query `WHERE Channel LIKE 'plugin:%'` gives admins a straightforward filter for plugin-originated notifications.


## 2026-06-16 - ContractVersion Bumped to 1.5.0

### Decision

`ContractVersion.Current` is bumped from `1.4.0` to `1.5.0`. `MinimumSupported` remains `1.0.0`.

### Rationale

`INotificationChannelProvider` is a new capability surface in the Contracts assembly. Plugins wishing to provide a notification channel must declare `RequiredCoreVersion: ">=1.5.0"` in their manifest to signal compatibility. The `ContractCompatibilityService` will allow all plugins with `RequiredContractVersion` between `1.0.0` and `1.5.0` inclusive. Per user approval in ADR 0001 GR-002.


## 2026-06-16 - notification.channel Extension Point Added to PluginExtensionPoints

### Decision

`PluginExtensionPoints.NotificationChannel = "notification.channel"` is added as a `public const string` field, and `"notification.channel"` is added to `KnownValuesInternal`.

### Rationale

`PluginManifestParser.ValidateExtensionPoints` rejects unknown extension point values. Without this entry, any plugin declaring `"notification.channel"` is rejected at install time. This is the identical pattern used for all prior extension points (`nav.main`, `page.generic`, etc.).


## 2026-06-16 - PluginAdminStatusViewModel Gains 26th Positional Parameter ChannelProviderCount

### Decision

`PluginAdminStatusViewModel` gains a 26th positional parameter `int ChannelProviderCount` after `NavEntryCount`. The construction site in `PluginAdminQueryService.CreateStatus` passes `runtime?.ChannelProviders.Count ?? 0`. All test construction sites pass `0`.

### Rationale

All existing runtime contribution counts (Routes, Services, MemberProviders, BackgroundJobs, NavEntries) are surfaced in the admin status view model. `ChannelProviderCount` follows the same observability pattern so administrators can see at a glance how many notification channels a plugin contributes.


## Source: Audit Sink Plugins

Former file: `audit-sink-plugins.adr.md`


## 2026-06-17 - IAuditSinkProvider Placed in Contracts Assembly with Structural-Fields-Only Event Model

### Decision

A new interface `IAuditSinkProvider` is added to `Contracts/Plugin/IAuditSinkProvider.cs`. Its single method `OnAuditEventAsync(PluginAuditEvent auditEvent, CancellationToken cancellationToken)` accepts a Contracts-only value type `PluginAuditEvent`. The event exposes six structural fields only: `Action`, `Actor`, `Source`, `TargetType`, `TargetId`, `OccurredAtUtc`. The fields `BeforeJson`, `AfterJson`, and `MetadataJson` are explicitly excluded.

### Rationale

`IAuditSinkProvider` must live in `ClubGear.Plugin.Contracts` because plugin code may not reference `ClubGear.Services`, `ClubGear.Data`, `ClubGear.Controllers`, or `ClubGear.Models` (enforced by `PluginRuntimeAdapter.ForbiddenNamespaces` and `PluginBoundaryTests`). The structural-fields-only payload was selected by user decision (2026-06-17): serialized payload fields (`BeforeJson`/`AfterJson`/`MetadataJson`) may contain PII and are withheld as the safest default; they can be relaxed in a later iteration.


## 2026-06-17 - Decorator Pattern for Sink Dispatch: AuditSinkDispatchDecorator Wraps DatabaseAuditLogService

### Decision

Sink dispatch is implemented as a decorator `AuditSinkDispatchDecorator` that wraps the inner `IAuditLogService` (bound to `DatabaseAuditLogService` in DI). The decorator calls the inner `LogAsync` first; after the inner call returns, the decorator builds a `PluginAuditEvent` and calls `IPluginAuditSinkService.DispatchAsync` in a separate, independent `try/catch`. The DI registration in `ServiceCollectionExtensions` replaces the direct `AddScoped<IAuditLogService, DatabaseAuditLogService>()` with a factory that wraps the concrete service.

### Rationale

Two candidate placements were evaluated: (a) appending sink dispatch inside `DatabaseAuditLogService.LogAsync` after `SaveChangesAsync`, and (b) a decorator wrapping `IAuditLogService`. The decorator is the lower-risk approach because it does not touch the existing swallow-boundary in `DatabaseAuditLogService`. The existing method has a single `try/catch` that encompasses the entire body including `SaveChangesAsync`; appending sink dispatch inside that block would conflate sink errors with DB write errors in log output. The decorator keeps the two concerns entirely independent: DB commit swallow semantics are unchanged; sink dispatch failure is caught and logged in the decorator's own guard. This is the pattern with the smallest blast radius on `DatabaseAuditLogService` itself.


## 2026-06-17 - PluginAuditSinkContribution as 11th Positional Parameter of RegisteredPluginRuntime

### Decision

`RegisteredPluginRuntime` gains an 11th positional parameter `IReadOnlyList<PluginAuditSinkContribution> AuditSinks`, appended after the 10th parameter `IReadOnlyList<PluginNotificationChannelContribution> ChannelProviders` (added by the notification-channel-provider iteration). `PluginAuditSinkContribution(string ProviderType, int Order = 0)` is added to `PluginContributions.cs`. `IPluginContributionSink` gains `void AddAuditSink(PluginAuditSinkContribution contribution) { }` with a default no-op body. `PluginLoader.PluginContributionCollector` adds a backing list and overrides the method. All 9 `RegisteredPluginRuntime` construction sites receive the new trailing argument.

### Rationale

Audit sink contributions are structural metadata declared at load time via `RegisterContributions`, identical to `NavEntries`, `ChannelProviders`, and all prior contribution types. Storing them in the runtime record avoids re-running `RegisterContributions` on every audit event. The default no-op body on `IPluginContributionSink` means existing compiled plugins are unaffected without recompilation. The blast radius of 9 sites (1 production, 8 test) is consistent with prior iterations.


## 2026-06-17 - IPluginAuditSinkService as Scoped Re-collect-at-call-time Service

### Decision

A new scoped service `IPluginAuditSinkService` / `PluginAuditSinkService` is introduced under `Services/Plugins/AuditSink/`. It reads `AuditSinks` from all registered runtimes via `IPluginRegistryReader.GetRegisteredPlugins()` at dispatch time and instantiates each `IAuditSinkProvider` via `IPluginRegistryReader.CreateMemberProvider<IAuditSinkProvider>`. Each sink invocation is wrapped in a per-sink `try/catch`; a failing sink is logged and skipped; remaining sinks in the loop continue.

### Rationale

This mirrors the established `PluginPageService`, `PluginNavEntryService`, and (per ADR) `PluginNotificationChannelService` patterns: re-collect-at-call-time from `RegisteredPluginRuntime` at dispatch, no singleton registry with mutable state, no DI container rebuild. Per-sink isolation (individual `try/catch` per sink, not a single block around the entire loop) ensures one misbehaving plugin does not suppress dispatch to all remaining sinks.


## 2026-06-17 - audit.sink Extension Point Added to PluginExtensionPoints

### Decision

`PluginExtensionPoints.AuditSink = "audit.sink"` is added as a `public const string` field and `"audit.sink"` is added to `KnownValuesInternal`.

### Rationale

`PluginManifestParser.ValidateExtensionPoints` is the single rejection point for unknown extension point values. Without this entry, any plugin declaring `"audit.sink"` is rejected at install time. The value `"audit.sink"` follows the established `<domain>.<role>` convention (matching `"notification.channel"`, `"nav.main"`, `"page.generic"`, etc.).


## 2026-06-17 - ContractVersion Bumped to 1.6.0

### Decision

`ContractVersion.Current` is bumped from `1.5.0` to `1.6.0`. `MinimumSupported` remains `1.0.0`. This iteration lands after the notification-channel-provider iteration (which bumped from `1.4.0` to `1.5.0`).

### Rationale

`IAuditSinkProvider` is a new capability surface in the Contracts assembly. Plugins wishing to provide an audit sink must declare `RequiredCoreVersion: ">=1.6.0"` in their manifest to signal compatibility. The `ContractCompatibilityService` allows all plugins with `RequiredContractVersion` between `1.0.0` and `1.6.0` inclusive. Per user approval gate ADR 0001 GR-002.


## 2026-06-17 - PluginAdminStatusViewModel Gains 27th Positional Parameter AuditSinkCount

### Decision

`PluginAdminStatusViewModel` gains a 27th positional parameter `int AuditSinkCount` after `ChannelProviderCount` (26th, added by notification-channel iteration). The construction site in `PluginAdminQueryService.CreateStatus` passes `runtime?.AuditSinks.Count ?? 0`. All test construction sites pass `0`.

### Rationale

All runtime contribution counts are surfaced in the admin status view model for operator observability. `AuditSinkCount` follows the same pattern as `RouteCount`, `NavEntryCount`, `ChannelProviderCount`, and all prior counters.


## 2026-06-17 - Pre-Implementation User Approval Gate (ADR 0001 GR-002)

### Decision

Before the Builder begins any file changes, the user must explicitly confirm approval for the core changes described in this design. The Builder's Phase I plan must start with this gate and halt until approval is received. Core changes covered by the gate: decorator DI registration, `RegisteredPluginRuntime` 11th parameter addition, `ContractVersion` bump to `1.6.0`.

### Rationale

`01_task.md` classifies this command as `generic-core` / `mixed` for `technical` plugins and explicitly states: "Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich." This gate must not be bypassed.


## Source: Background Job Runner

Former file: `background-job-runner.adr.md`


## 2026-06-17 - IPluginBackgroundJob Interface Placed in Contracts Assembly with IPluginHostContext Parameter

### Decision

A new interface `IPluginBackgroundJob` is added to `Contracts/Plugin/IPluginBackgroundJob.cs` with a single method `ExecuteAsync(IPluginHostContext hostContext, CancellationToken cancellationToken)`. The parameter type is `IPluginHostContext`, not `IPluginRuntimeBridge`.

### Rationale

All existing plugin-side contributor interfaces that need runtime services receive `IPluginHostContext` directly (`IAdminFunctionPanelProvider`, `IMemberDetailCardProvider`, `IMemberEditTabProvider`, etc.). `IPluginRuntimeBridge` is a host-side construct passed only within `IPluginRuntimeAdapter.RunAsync` / `InvokeAsync` delegates. Background jobs run without a user session, making the permission-enforcement layer in `IPluginRuntimeBridge.HasPermissionAsync` inappropriate. `IPluginHostContext` provides member access and persistence without exposing permission checks or audit delegation. The interface must live in `ClubGear.Plugin.Contracts` because plugin assemblies may not reference `ClubGear.Services`, `ClubGear.Data`, `ClubGear.Controllers`, or `ClubGear.Models`.


## 2026-06-17 - PluginBackgroundJobRunner is a Singleton IHostedService-equivalent Using IServiceScopeFactory

### Decision

A new singleton `PluginBackgroundJobRunner` (implementing `IPluginBackgroundJobRunner`) is registered via `services.AddSingleton<IPluginBackgroundJobRunner, PluginBackgroundJobRunner>()`. It is NOT registered as an `IHostedService`. It is started by `PluginLifecycleService.LoadIntoRuntimeAsync` after each successful plugin registration, and stopped by `PluginLifecycleService.DeactivateAsync` before `PluginRegistry.Unregister` is called. It uses `IServiceScopeFactory` to open a new DI scope per job execution to access scoped services such as `ApplicationDbContext`.

### Rationale

No `AddHostedService` call exists anywhere in the codebase. Putting job management inside `PluginRegistry` would add scoped service coupling to a singleton. Starting jobs inside `PluginLifecycleService` (scoped) would require holding long-lived `Task` references in a scoped object. The singleton pattern using `IServiceScopeFactory` is already established by `PluginEndpointRegistrar`. A manual singleton runner that is driven by lifecycle events is simpler than a hosted service because it maps directly to the `LoadIntoRuntimeAsync` / `DeactivateAsync` call sites and avoids any startup-ordering dependency on `IHostedService.StartAsync`.


## 2026-06-17 - Cancellation and Drain Sequence Before AssemblyLoadContext.Unload

### Decision

`PluginLifecycleService.DeactivateAsync` calls `await _jobRunner.StopJobsForModuleAsync(moduleId)` before calling `_runtimeRegistry.Unregister(moduleId)`. `StopJobsForModuleAsync` signals the per-module `CancellationTokenSource`, awaits all running job tasks for that module with a 5-second timeout, then removes state entries. Only after this drain does `PluginRegistry.Unregister` call `loadContext.Unload()`.

### Rationale

`PluginRegistry.Unregister` currently calls `loadContext.Unload()` synchronously with no drain. A job delegate that is still executing at the point of unload would reference types in an unloaded `AssemblyLoadContext`, causing `TypeLoadException` or `AccessViolationException`. The drain must live in `StopJobsForModuleAsync` (on the runner) rather than directly in `DeactivateAsync` to keep lifecycle orchestration clean and testable independently.


## 2026-06-17 - Job State Stored In-Memory on the Runner Singleton

### Decision

Live job state (`State`, `LastRunUtc`, `LastError`) is stored in a `ConcurrentDictionary<string, PluginJobEntry>` on `PluginBackgroundJobRunner`. The Admin UI `Detail.cshtml` injects `IPluginBackgroundJobRunner` and reads `GetJobStatuses(moduleId)` at render time. `PluginAdminStatusViewModel` receives one new int parameter `BackgroundJobRunningCount` (count only). No new database table is introduced.

### Rationale

The architecture for `NavEntries` and `AuditSinks` is the established precedent: counts in `PluginAdminStatusViewModel`, per-item detail fetched at render time via an injected singleton reader. In-memory state is lost on process restart, which is accepted: background jobs re-register and start fresh on the next activation. A new `PluginJobExecutionRecord` DB table would require a migration and add complexity disproportionate to the informational value of persisted last-run timestamps for this iteration.


## 2026-06-17 - System Identity for Background Job Execution

### Decision

`PluginBackgroundJobRunner` constructs a system `ClaimsPrincipal` as `new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "system.plugin-job-runner")], "System"))` for passing to `IPluginRuntimeAdapter.CreateBridge`. Job classes receive `IPluginHostContext` directly and do not call `HasPermissionAsync`; the system identity is used only to satisfy the non-null requirement of `CreateBridge`.

### Rationale

No system-identity helper or factory exists in production code. The empty-identity pattern `new ClaimsPrincipal(new ClaimsIdentity())` is used in tests but would fail any authentication check. A named system identity with an explicit authentication type ("System") is a minimal, self-documenting pattern that satisfies the `ArgumentNullException.ThrowIfNull(user)` guard in `PluginRuntimeAdapter` without granting any permissions. Because background jobs receive `IPluginHostContext` (not `IPluginRuntimeBridge`), they bypass the permission-check layer entirely, making the exact claim set irrelevant to security.


## 2026-06-17 - Test Fixture Pattern: Extend AllowedPluginRuntimeFixtures with RuntimeLoadedBackgroundJobA

### Decision

A concrete `RuntimeLoadedBackgroundJobA : IPluginBackgroundJob` type is added to the existing `tests/ClubGear.ArchitectureTests/Fixtures/AllowedPluginRuntimeFixtures.cs`. The new `PluginBackgroundJobRunnerTests` test class is a separate file in `tests/ClubGear.ArchitectureTests/`. No new fixture file is created; the existing `RuntimeLoadedPluginModuleA` already contributes the `members.sync` job, and the new concrete class can be referenced from the same assembly.

### Rationale

Isolated fixture files (`ForbiddenServicePluginFixture`, `ForbiddenControllerPluginFixture`, `ForbiddenDbContextPluginFixture`) exist specifically for boundary-violation cases that must not pollute the allowed-plugin assembly. `RuntimeLoadedBackgroundJobA` is an allowed, conforming job implementation. The `PluginTestPackageBuilder` packages the `RuntimeLoadedPluginModuleA` assembly, which includes all types in `AllowedPluginRuntimeFixtures.cs`, so the job type is automatically available in loaded test plugins without changing the packaging logic.


## Source: IdP Plugin Interface

Former file: `idp-plugin-interface.adr.md`

## 2026-08-05 - Provider-Specific Named OIDC Schemes and Local Identity Session

### Decision

The earlier single-`oidc.generic` state-dispatch design is superseded. ClubGear keeps the OIDC
handler type in the core, registers named authentication schemes `oidc.{providerKey}` after
activated plugins have loaded at startup, and keeps challenge-time registration as a fallback for
providers enabled later in the process. Named options are post-configured from
`externallogin.{providerKey}` and use `/signin-oidc-{providerKey}` as their callback path. Saving
provider configuration invalidates that named options cache entry; the generic system-config
write and delete paths invalidate it as well.

Only enabled providers with all required settings and a valid authority are registered. HTTPS is
mandatory except for loopback-only development authorities. If a previously registered scheme is
disabled, becomes invalid, or its configuration cannot be read, named-options materialization
falls back to static quarantine metadata. This keeps `AuthenticationMiddleware` and the admin
repair path available while challenge and callback validation continue to fail closed.

After the remote callback, the temporary external cookie is cleared before plugin claim mapping
and account lookup. A mapped `sub` is the effective `Member.OauthID`; non-reserved profile claims
are included in the local application cookie. Local identity, role, permission, security-stamp,
and system-role claim types are rejected by the host and cannot be injected directly by an IAM
plugin. The mapper-controlled effective `sub` remains a security trust boundary because it selects
the already linked local account and, indirectly, that account's local roles.
The host removes ASP.NET Core's default `iss` claim-deletion action for valid provider options so
the mapper can bind its global link key to the issuer that token validation already accepted.
Success now requires the member to reference an existing local account through
`Member.ApplicationUserId`. The local account must pass `CanSignInAsync` and lockout checks;
accounts with local two-factor enabled are denied until a continuation flow exists. It still
performs no automatic member or account provisioning. Duplicate keys, invalid keys, missing
provider types, and interface/contribution key mismatches are unavailable and fail closed.

### Rationale

The previous implementation registered only `oidc.generic` while the controller challenged
`oidc.{providerKey}`. It also returned a successful outcome without creating a local application
cookie. Provider-specific schemes make independent Keycloak, Entra ID, and other OIDC authorities
possible without mutating shared options across concurrent requests. Requiring an explicit local
account link prevents an external subject from implicitly provisioning or claiming a ClubGear
account.

### Current Contract Notes

`IIdentityProviderPlugin` has the required metadata properties `ProviderKey` and `DisplayName` in
addition to `GetConfigSchema`, `TestConnectionAsync`, and `MapClaimsAsync`.
`PluginIdentityProviderContribution` currently has the signature
`(string ProviderKey, string ProviderType)`. The current accepted contract window is defined by
`ContractVersion.Current` and `ContractVersion.MinimumSupported`; historical version numbers below
describe when the surface was introduced, not the version a newly built plugin should declare.


## 2026-06-17 - Runtime IdP Plugins Are Not Viable Under Current ASP.NET Core Startup Ordering

### Decision

Runtime-dynamic OIDC/OAuth2 auth handler registration via loaded plugins is rejected as unviable. The design uses a configuration-based alternative: a host-registered generic OIDC handler whose options are populated at request time from `SystemConfigEntry`, with plugin code contributing metadata and claims-mapping logic — not the middleware handler itself.

### Rationale

`Program.cs` loads plugins after `app.Build()` via `IPluginLifecycleService.LoadActivatedAsync()`. Remote auth handler types (`OpenIdConnectHandler`, etc.) must be registered in DI before `app.Build()` because the middleware pipeline snapshot is frozen at that point. `IAuthenticationSchemeProvider.AddScheme` can add scheme names post-startup, but only points at handler types already in DI. Since `Program.cs` registers no OIDC or OAuth2 handler type today, plugin-supplied handlers arrive too late. The only viable architecture without a host restart is to have the host own the handler type (registered before Build) and let plugins supply configuration and mapping logic.


## 2026-06-17 - Configuration-Based Alternative: OidcOptionsReloader + SystemConfigEntry

### Decision

A single `AddOpenIdConnect("oidc.generic", ...)` call is added to `Program.cs` before `builder.Build()`. Its runtime parameters (authority, clientId, clientSecret, scopes, callbackPath) are supplied at request time by `OidcOptionsReloader`, a singleton implementing `IConfigureNamedOptions<OpenIdConnectOptions>`. `OidcOptionsReloader` reads `SystemConfigEntry` rows under `Section = "externallogin.<providerKey>"` via `ISystemConfigService`. No per-provider DI scheme registration is performed; all plugin-declared providers share the single generic handler and are disambiguated by a `providerKey` state parameter in the challenge.

### Rationale

This is the standard ASP.NET Core pattern for deferred/runtime-configurable OIDC options — the handler type is static (satisfying the DI constraint before Build), while the option values are dynamic (read at request time from persistent config). This unblocks plugin-supplied IdP providers without requiring a host restart and without duplicating handler type registrations per provider.


## 2026-06-17 - IIdentityProviderPlugin: Metadata + Claims Mapping Only, No Handler Code

### Decision

`IIdentityProviderPlugin` (in `ClubGear.Plugin.Contracts`) exposes three capabilities: `GetConfigSchema()` (field schema for admin UI), `TestConnectionAsync(config, ct)` (optional connectivity check), and `MapClaimsAsync(context, ct)` (transform or augment raw IdP claims into `PluginClaimEntry` entries). Plugin code never touches ASP.NET Core Identity or Authentication types directly. All auth types are mirrored at the Contracts boundary: `PluginClaimEntry`, `PluginExternalLoginContext`, `PluginExternalLoginTestResult`.

### Rationale

`ForbiddenNamespaces` enforces isolation from `ClubGear.*` core namespaces. While `Microsoft.AspNetCore.Authentication` is not currently blocked by the enforcement array, the Contracts assembly must not reference it (confirmed by `ContractsAssembly_ShouldNotReference_CoreAssembly` boundary test convention). Mirror types analogous to `PluginRuntimeNotification` / `NotificationMessage` are the established pattern for crossing this boundary without coupling Contracts to ASP.NET Core.


## 2026-06-17 - ExternalLogin Path Added to AccountController, Not a New Controller

### Decision

Two new `[AllowAnonymous]` GET actions — `ExternalLoginChallenge` and `ExternalLoginCallback` — are added to the existing `AccountController` (`Controllers/Account/Account_Controller.cs`). No new account-facing controller is created.

### Rationale

`AccountController` already owns all unauthenticated entry points (Login, Register, AccessDenied, Logout). Adding the challenge and callback actions there keeps route coherence (`/Account/ExternalLogin*`) and avoids splitting login-related routes across two controllers. The controller remains thin: both actions delegate immediately to `IExternalLoginService`.


## 2026-06-17 - OauthID as the External Identity Link Key

### Decision

`Member.OauthID` (string, max 100, UNIQUE index) is the lookup key for linking an ASP.NET Identity `ApplicationUser` to a `Member` record after successful external login. `ExternalLoginService.HandleCallbackAsync` queries `Member` by `OauthID = subjectClaim` after obtaining claims from the IdP. If no `Member` is found, the outcome `ExternalLoginStatus.NoLinkedMember` is returned, and the user is shown a link-account page; no automatic `Member` record is created.

### Rationale

`Member.OauthID` already carries a UNIQUE constraint enforced in `ApplicationDbContext.OnModelCreating`, confirming intent as a uniqueness key for external identity linking. The existing admin `Edit` view already allows operators to set `OauthID`. No automatic member provisioning is designed in this iteration (deferred to a future iteration) to avoid scope creep and unintended trust escalation.


## 2026-06-17 - PluginSchemaFieldType.Secret Added for Secret Field Masking

### Decision

A new enum value `Secret` is appended to `PluginSchemaFieldType` in `PluginSchemaContracts.cs`. The admin UI renders `Secret` fields as `<input type="password">` and displays `***` for any saved non-empty value. The underlying storage remains a plaintext `string` in `SystemConfigEntry.Value`. Encryption at rest is explicitly deferred.

### Rationale

The task requirement is a masked admin UI field for IdP credentials (client secret, etc.). No `Password` or `IsSecret` flag exists in the current schema contracts. Adding a `Secret` enum value is additive and backward-compatible for existing plugins. Encryption at rest (e.g., ASP.NET Core Data Protection API applied to `SystemConfigEntry`) is a separate concern deferred to a future iteration; the `Secret` field type establishes the UI contract independently of the storage contract.


## 2026-06-17 - PluginIdentityProviderContribution as 11th Positional Parameter of RegisteredPluginRuntime

### Decision

`RegisteredPluginRuntime` gains an 11th positional parameter `IReadOnlyList<PluginIdentityProviderContribution> IdentityProviders` appended after `AuditSinks` (10th). `PluginIdentityProviderContribution(string ProviderType, int Order = 0)` is added to `PluginContributions.cs`. `IPluginContributionSink` gains `void AddIdentityProvider(PluginIdentityProviderContribution contribution) { }` with a default no-op body. All construction sites receive the new trailing argument.

### Rationale

This follows the identical pattern established for `AuditSinks` (10th parameter, added in the audit-sink-plugins iteration) and all prior contribution types. The default no-op body ensures existing compiled plugins are unaffected without recompilation.


## 2026-06-17 - ContractVersion Bumped to 1.7.0

### Decision

`ContractVersion.Current` advances from `1.6.0` to `1.7.0`. `MinimumSupported` remains `1.0.0`.

### Rationale

`IIdentityProviderPlugin`, the three mirror record types, `PluginIdentityProviderContribution`, `PluginSchemaFieldType.Secret`, and the `identity.provider` extension point are all new additions to the public Contracts surface. These are additive (minor) changes. Plugins using the new interface must declare `requiredCoreVersion: ">=1.7.0"`.


## 2026-06-17 - Pre-Implementation User Approval Gate (ADR 0001 GR-002)

### Decision

Before the Builder begins any file changes, the user must explicitly confirm approval for the core changes described in this design. The Builder's plan must start with an explicit approval gate. Core changes requiring approval: `Program.cs` `AddOpenIdConnect` addition, `RegisteredPluginRuntime` 11th parameter, `ContractVersion` bump to `1.7.0`, new `PluginSchemaFieldType.Secret` enum value.

### Rationale

`01_task.md` states: "Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich." The changes above modify core infrastructure (startup registration, the shared runtime record, the public Contracts version). This gate must not be bypassed.

---

## Source: Backlog Consolidation and Core/Plugin Re-Classification

Iteration: audit and re-cut backlog, 2026-09. Documentation only — no production code, no contract change, no migration. Design: `.crispy/04_design.md`.

## 2026-09-01 - Consolidated Backlog Lives in `docs/backlog/` as Index Plus Area Files

### Decision

The four rival open-item lists are merged into one tracked backlog under `docs/backlog/`, laid out as `README.md` (index, classification rules, ID scheme, status and priority vocabulary, `CG-BL-xxx` crosswalk) plus four area files: `core-platform.md`, `core-capabilities.md`, `domains.md`, `plugins.md`. Every item appears exactly once, in `core-platform.md`, `core-capabilities.md` or `plugins.md`, each carrying an explicit owner column (`core`, or a named plugin repository) and a one-line rationale for its classification. `domains.md` holds no items of its own — only ordered pointers forming the sequencing view. Plugin-owned items carry a one-line summary and a pointer to the plugin's own file, and no detail that would go stale when the plugin ships. All five files are written in English and open with the `docs/architecture/*` metadata block (`Audience:`, `Scope:`, `Last-Validated:`, `Source-Commit:`).

The sources are retired: `backlog/finance-plugin.md` and the `backlog/` directory are deleted, and the ignored copies `tmp/TodoFeatures.md`, the eleven `tmp/P0-*.md` / `tmp/P1-*.md` specs and `.backlog/backlog.md` are removed. Only section A of `backlog/finance-plugin.md` — the half-done `kassenwart` to `treasurer` rename — survives, together with a pointer to `plugins/Finance/nextSteps.md`.

### Rationale

Four lists dated 2026-06-09, 2026-07-10 and 2026-08-27, in three ID schemes, disagreed with each other and with the code: nine `CG-BL` items were already shipped, `CHAT-01` was filed as a plugin after `DECISIONS.md` had folded it into the core, and `FIN-01` listed work delivered in Finance `1.9.0`. Two of the four lists were git-ignored and existed on one machine only, so the project's real backlog was invisible to the repository. The index-plus-area-files layout mirrors `docs/architecture/proposal-2026-08/`, the only existing nested documentation set, so no new convention is introduced. The metadata header is a deliberate divergence from the flat `docs/*.md` pages: backlog files go stale fastest, and `Last-Validated` plus `Source-Commit` are the specific guard against a repeat of the four-rival-lists situation. `Readme.md` is the only navigation surface in the repository — there is no `mkdocs.yml`, `docs/README.md` or `docs/index.md` — so both its layout table and its Further Reading list gain an entry.

## 2026-09-01 - Core/Plugin Classification Rules R1-R8 Adopted as the Standing Test

### Decision

Every backlog item is classified against a fixed, published rule set rather than case by case. R1: the capability belongs to the core, the vendor binding belongs to a plugin. R2: there is exactly one template system, and it is central. R3: local file storage stays in the core as the default, but a storage plugin must be able to supersede and disable it. R4: the core provides one generic workflow engine; every concrete workflow is declared by a plugin. R5: there are two plugin kinds — service/provider plugins, consumable by other plugins as well as by the host, and functional plugins. R6: a security boundary that must exist before any plugin loads stays in the core. R7: any new plugin is its own `plugins/<Name>/` sub-project with its own `.csproj`, `plugin.json` and `CHANGELOG.md`, versioned independently as a submodule. R8, a derived tie-break: when R1 to R7 do not decide, an item is core only if the host itself needs it or two independent consumers need it; otherwise it is a plugin.

### Rationale

"The core provides only minimal functions, extensible by plugin" is not actionable as a slogan; it produced four lists that classified the same item differently. A written rule set makes each classification checkable and makes disagreements resolvable by pointing at a rule rather than at an opinion. R1 to R7 are the user's own rules stated verbatim in substance. R8 is marked as derived so it is visible as an inference and can be overruled without disturbing the rest.

## 2026-09-01 - One Provider Contribution Seam, Not Two

### Decision

The user's "service plugin" concept and the `channel-provider` plugin type called for but never written by `DECISIONS.md` D2 are the same idea and are implemented once, as backlog item CORE-13. The seam extends the existing audit-sink and identity-provider contribution pattern and gives the already-shipped but inert `PluginServiceContribution(string Key, string ServiceType)` a consumer: a new host-side `IPluginProviderRegistry` offering `Resolve<T>(capabilityKey)` for single-active capabilities and `ResolveAll<T>(capabilityKey)` for fan-out capabilities, reachable from a plugin through `IPluginHostContext` so one plugin can consume another's provider without either knowing the other. Notification channels, storage providers, document providers and authentication-scheme providers are all capability keys on this one seam.

Three named blockers are carried as separate backlog items rather than assumed away. CORE-18: contributions are instantiated by `Activator.CreateInstance` on a public parameterless constructor and receive no DI, no bridge and no host context, so provider-to-provider consumption is impossible until instantiation is changed. CORE-17: `PluginRegistry.Unregister` calls `AssemblyLoadContext.Unload()` with no drain of in-flight provider calls. CORE-16: providers created through `PluginRegistry.CreateMemberProvider` bypass `EnsureIsolated()` entirely, and that check inspects only a delegate target type's instance fields.

### Rationale

`IPluginContributionSink.AddService` is already a required, non-default member of the shipped contract, and `PluginLoader` already collects it onto `RegisteredPluginRuntime.Services`; only the host side is missing. Building on it is therefore additive and needs no `ContractVersion` major bump, whereas inventing a second parallel mechanism for channels would leave the repository with two ways to do one thing and an inert record that never dies. Widening the provider surface without widening the boundary check (CORE-16) or the lifecycle guarantees (CORE-17) would widen an existing hole, so those items are sequenced ahead of or alongside CORE-13 rather than deferred. The 2026-06-17 decision "Cancellation and Drain Sequence Before AssemblyLoadContext.Unload", made for background jobs, is the existing precedent CORE-17 generalises.

## 2026-09-01 - The 2026-06-16 Notification-Channel-Provider Decisions Are Recorded as Not Implemented

### Decision

The eight dated entries under `## Source: Notification Channel Provider` in this file, dated 2026-06-16, describe a seam that does not exist in the tree. They are retained unaltered as history, per the append-only rule, and are corrected here by appending rather than by rewriting. Backlog item CORE-22 covers reconciling them, together with the other documentation defects found during this audit.

### Rationale

Those entries record `INotificationChannelProvider` placed in the Contracts assembly, `PluginNotificationChannelContribution` as the 10th positional parameter of `RegisteredPluginRuntime`, a `notification.channel` extension point, a core-first / plugin-fallback precedence rule in `NotificationService`, and a `ContractVersion` bump to `1.5.0`. A repository-wide search for `INotificationChannelProvider`, `PluginNotificationChannelContribution` and `notification.channel` matches only this ADR and the phase-R research note. `Contracts/Plugin/` contains 23 files and no notification-channel contract. `PluginExtensionPoints.cs` declares exactly eleven constants and `notification.channel` is not among them. Independently, the 2026-06-11 member-extension-data section also claims the bump to `1.5.0`, so the two records cannot both be true. An ADR that asserts a shipped seam which does not exist is worse than a gap, because it stops anyone from building the seam; leaving the claim uncorrected would have let CG-BL-030 stay closed forever.

## 2026-09-01 - Provider Selection Is Explicit and Admin-Controlled, Not Implicit DI Precedence

### Decision

For single-active capabilities — file storage first among them — the active provider is chosen explicitly by an administrator per capability key, defaulting to the core implementation. This is backlog item CORE-14. There is no implicit last-registration-wins, no ordering-by-module-id and no silent fallback. CORE-14 must also define what happens when two providers claim the same capability key.

### Rationale

R3 requires a storage plugin to supersede and disable the core local store, and no mechanism for that exists: `IProfileImageStorageService` is a single `AddSingleton` with no precedence concept, and the four provider-shaped interfaces in the host today resolve in four different, unrelated ways (single injected instance, name-keyed dictionary, all-enumerated, first-match-by-key). `PluginAuditSinkContribution.Order` exists and is never read. Worse, `NotificationService` builds its channel dictionary with `ToDictionary` keyed by `ChannelName` in its constructor, which throws on a duplicate — so today two providers claiming one channel is a scope-construction failure rather than an override. Making the choice explicit and admin-visible is the only option that satisfies "supersede and disable" without turning registration order into a hidden configuration surface. It also confronts, rather than hides, the fact that the local store's on-disk behaviour is baked into a returned string: `ProfileImageStorageService` returns `"/uploads/profile-images/" + fileName`, `DeleteProfileImageAsync` silently returns for any path not starting with that prefix, `app.UseStaticFiles()` is what actually serves it, and both the maintenance-mode whitelist and the release-packaging scripts special-case `wwwroot/uploads/`.

## 2026-09-01 - API-Key Extraction Accepted; Cookie Authentication Stays in the Core Permanently

### Decision

CORE-09 API keys are extracted from the core into a plugin, reversing the reasoning recorded in the August list. The extraction is sequenced behind CORE-13, CORE-17, CORE-18 and CORE-15, and is subject to a permanent constraint: cookie/Identity authentication remains in the core, and an api-key plugin can never be the only way to authenticate. The bootstrap order is "cookie auth boots, admin signs in, plugin installs and activates, api-key scheme becomes available", never the reverse. Moving out: `Models/ApiKey.cs`, `IApiKeyService`, `ApiKeyService`, `ApiKeyAuthenticationHandler`, the `DbSet` on `ApplicationDbContext` and the `202608310101_AddApiKeys` migration. Staying regardless: the `IAuthenticationSchemeProvider` registration path, the permission-definition seam and audit. The three gaps listed in `docs/api-keys.md` — admin UI, an endpoint that accepts the scheme, per-key rate limiting — are built inside the plugin after extraction, not in the core beforehand. The backlog item carries an explicit decision point on whether existing key rows are migrated into the plugin's prefixed tables or reset.

### Rationale

Extraction is cheap today and gets more expensive with every consumer added: the scheme is non-default, a missing header returns `AuthenticateResult.NoResult()`, and no endpoint or policy consumes it — `docs/api-keys.md` states this outright. Closing the three gaps in the core first would mean building code destined for deletion. The bootstrap constraint is not a preference but an enforced fact: all three plugin-admin controllers carry `[Authorize]` plus `[PermissionAuthorize(AdminAccess)]`, and plugins load during startup before `app.UseAuthentication()` runs. Sequencing behind CORE-17 and CORE-18 is necessary because authentication sits on the request hot path, where a handler type resolved from a collectible `AssemblyLoadContext` that may unload mid-request is the in-flight problem at its most dangerous. Dynamic scheme *registration* is already solved and proven by `ExternalLoginService.RegisterDeclaredSchemesAsync`; dynamic handler *types* from a plugin assembly are not, and that is the actual gap.

## 2026-09-01 - CG-BL-xxx Retired as an ID Space; CORE-xx Continues

### Decision

The `CORE-xx` / `INV-01` / `FIN-01` prefix scheme is kept. `CG-BL-xxx` is retired as an ID space: its still-open items are remapped into the `CORE-xx` sequence, continuing as `CORE-13` through `CORE-23` rather than opening a second namespace. `docs/backlog/README.md` carries a `CG-BL-xxx` to new-ID crosswalk table, and that crosswalk is the only place `CG-BL` appears. Item status is verified against HEAD, never against the source lists, using the vocabulary `delivered`, `partial`, `open`, `superseded`, `retired`. Priority is re-anchored so that platform seams lead: P0 unblocks two or more other items or corrects a false record, P1 is a user-visible capability with a named consumer, P2 blocks nothing, P3 is optional.

### Rationale

Two parallel ID spaces for one backlog is what allowed the same item to be tracked twice with different conclusions. The June `CG-BL` list is largely history — nine of its twenty-five items are shipped and two are contradicted by the shipped naming — so preserving it as a live namespace would preserve the confusion; a crosswalk preserves the traceability without the ambiguity. Re-anchoring priority away from the August list's ordering follows from the classification work itself: the minimality rule is only enforceable once the plugin boundary is actually tested (CORE-16) and a plugin can actually supersede a core capability (CORE-13 and CORE-14), so those now precede the capability items that the August list ranked P0.

## 2026-09-02 - The Lowercase `adr/` Directory Is Retired for Real

### Decision

The three files remaining in the lowercase `adr/` directory — `payment-capture.adr.md`, `club-accounts-bookings.adr.md` and `finance-plugin-packaging.adr.md` — are appended verbatim to `ADRs/consolidated/finance-plugin.adr.md`, each under its own `## Source:` heading naming the former file. `adr/` is deleted, and `ADRs/README.md` is corrected to state that the migration is complete and to list the three newly absorbed files in the Finance row.

### Rationale

`ADRs/README.md:9` claimed the consolidated files "preserve the dated decision entries that previously lived in the former lowercase ADR directory", but that directory still existed and held three unmigrated files, none of which appeared in the Former-files column. The repository therefore carried two ADR trees differing only in case and a plural `s`, while its own documentation asserted there was one. All three files were Finance-domain decisions, so a single target existed and no new topic-level ADR was needed. All 17 dated entries were carried over and counted before the source files were removed; the per-file `## Purpose` blocks were identical boilerplate ("Append new entries. Do not rewrite prior history.") already covered by the target file's own Purpose, so dropping them lost nothing. Backlog item CORE-22 records this as one of four documentation-integrity defects found during the 2026-09 audit.

## 2026-09-02 - The Provider Seam Resolves On Demand, It Does Not Register Into DI

### Decision

`IPluginProviderRegistry` (CORE-13) reads `RegisteredPluginRuntime.Services` from the runtime registry at call time and instantiates the provider through the existing `IPluginRegistryReader.CreateMemberProvider<T>` path. Plugin-supplied providers are **not** registered into the ASP.NET DI container.

### Rationale

The container is built once during `Program.cs` startup, whereas plugins are activated and deactivated at runtime through `PluginLifecycleService`. A provider registered into the container at startup could not be withdrawn when its plugin is deactivated, and one registered after startup is not possible at all. Resolving on demand keeps activation and deactivation as the single place where a provider appears and disappears, and it is the approach the already-shipped audit-sink and identity-provider seams take, so the seam adds no new lifetime concept. The cost is that a provider is constructed per resolution rather than pooled; that is acceptable while providers are stateless, and it becomes a real decision only when CORE-18 replaces `Activator.CreateInstance` with DI.

## 2026-09-02 - An Unrecognised Capability Key Rejects the Plugin at Load Time

### Decision

`PluginCapabilityKeys` is a host-validated allowlist. `PluginLoader.LoadAsync` checks every `PluginServiceContribution.Key` after `RegisterContributions` and, on an unknown key, unloads the assembly load context and fails the load with a message listing the permitted keys. The check is deliberately not a warning.

### Rationale

`AddService` was a required contract member whose contributions nothing read, so any string was accepted and silently discarded. Preserving that leniency while giving the seam a consumer would produce the worst outcome available: a plugin that installs, activates, reports healthy and never runs its provider, with no diagnostic pointing at the typo. Rejecting at load time makes the failure loud and local to its cause. This is a behavioural break for a third-party plugin that declared a service against the old no-op behaviour; it is recorded as such in `docs/plugin-contract.md` under 1.12.0 and in `CHANGELOG.md`. No plugin in this repository calls `AddService`, so nothing in-tree was affected. The two fixture plugins in `tests/.../Fixtures/AllowedPluginRuntimeFixtures.cs` used the invented keys `members.service` and `members.service.b` and were moved onto real capability keys rather than widening the allowlist to accommodate test scaffolding.

## 2026-09-02 - A Capability Collision Is Logged and Resolved Deterministically, Never Thrown

### Decision

When several plugins claim the same `SingleActive` capability, `IPluginProviderRegistry.Resolve` returns the candidate whose module id sorts first, logs a warning naming every competing module, and continues. Selection by an administrator is CORE-14 and is not implemented here.

### Rationale

The seam exists partly to fix the opposite behaviour. `NotificationService` builds its channel lookup with `channels.ToDictionary(c => c.ChannelName, …)` in its constructor (`Services/Core/NotificationService.cs:32`), and `ToDictionary` throws on a duplicate key — so two providers claiming one name is a scope-construction failure that surfaces far from its cause, as a request-time DI error rather than as a configuration problem. Reproducing that in the new seam would carry the defect forward. Ordering by module id was chosen over registration order because registration order depends on activation history and is therefore not stable across restarts, which would make the active provider change without anyone changing anything. `NotificationService` itself is deliberately left untouched: no plugin can contribute an `INotificationChannel` today, so the collision is currently unreachable there, and the swap belongs with the increment that routes channels through this seam rather than as speculative churn now.

## 2026-09-03 - Contributions Are Constructed Through a Host-Controlled Scope With the Canonical Constructor `T(IPluginHostContext)`

### Decision

CORE-18. `PluginRegistry.CreateMemberProvider<T>` and its `Activator.CreateInstance(providerClrType)` call are removed outright. `IPluginRegistryReader` exposes `Type? ResolveContributionType(string moduleId, string contributionType)` instead — a pure lookup that runs no plugin code. A new singleton `IPluginContributionActivator` constructs contributions: it opens an `AsyncServiceScope`, builds an `IPluginHostContext` through the existing `IPluginRuntimeAdapter.CreateBridge`, and invokes exactly one accepted signature, the public constructor `T(IPluginHostContext)`. There is no parameterless fallback and no `ActivatorUtilities` fallback; any other constructor shape rejects the plugin at load time. `IPluginHostContext` is reused as-is from `Contracts/Plugin/IPluginHostContext.cs` and is not modified.

`Rent<T>` returns a `PluginContributionLease<T>` implementing both `IDisposable` and `IAsyncDisposable`, carrying the instance, the owning module id and an error string when construction did not happen. Disposing the lease disposes the scope.

### Rationale

Provider-to-provider consumption — the thing CORE-13 needs and `docs/plugin-contract.md:92-97` already names as blocked — is impossible while the host cannot hand anything to a contribution at construction. A parameterless fallback was rejected because it would preserve exactly the state being fixed: two construction shapes, one of which receives nothing, with no way for a plugin author to discover which one the host chose. `ActivatorUtilities` was rejected because nothing outside the contracts assembly is legitimately resolvable by a plugin — the `AssemblyLoadContext` boundary exists to prevent it — so its flexibility would only invite requests for `ClubGear.Services` types that the isolation scan then has to reject, with a late and opaque message. One canonical signature is documentable in a single sentence and testable in a single assertion.

`IPluginHostContext` was chosen as the parameter over `IPluginRuntimeBridge` for the reason already recorded on 2026-06-17: the bridge is a host-side construct carrying permission and audit delegation tied to a `ClaimsPrincipal`, whereas the host context is what every contribution-facing method already receives. Promoting it to the constructor therefore adds no new type to a plugin's compile surface.

Returning a lease rather than a bare instance is what makes CORE-17 tractable: it gives the host a defined window during which plugin code from a module is live, which a bare `T` never did.

## 2026-09-03 - The Module Entry Point Keeps Its Parameterless Constructor and Is Scanned Before Instantiation

### Decision

`PluginLoader.CreateModuleInstance` continues to construct the `IPluginModule` entry point with `Activator.CreateInstance` and a parameterless constructor. This is the single documented exception to the canonical-constructor rule. As compensation, the entry-point `Type` is passed through the isolation scanner *before* it is instantiated, so a module that references a forbidden core namespace is rejected before any of its code — including `RegisterContributions` — runs.

### Rationale

The host builds `IPluginHostContext` from `module.Manifest`, and the manifest is only reachable from an already-constructed module. Requiring `(IPluginHostContext)` on the entry point is therefore circular, not merely inconvenient. Leaving the exception undocumented was the worse option: it would read as an oversight in a contract that otherwise admits exactly one shape. Scanning the type before instantiation is strictly more than the status quo, where `Activator.CreateInstance` at `PluginLoader.cs:127` and the subsequent `RegisterContributions` at `:53` both executed with no check at all.

## 2026-09-03 - The Isolation Check Becomes a Load-Time Gate Over All Declared Contribution Types

### Decision

CORE-16. The scan engine (`TryFindForbiddenReference`, `EnumerateReferencedTypes`, `IsForbiddenType`, `FlattenTypes` and the `ForbiddenNamespaces` array) moves out of `PluginRuntimeAdapter` into an internal static `PluginIsolationScanner`. `PluginRuntimeAdapter.EnsureIsolated(Delegate)` keeps its signature and its pinned `UserFriendlyException` message and forwards to it. `PluginLoader.LoadAsync` calls the scanner over the module entry-point type and over every declared contribution type — member providers, capability services, background jobs, audit sinks, identity providers and self-service sections — resolving each from the module's own assembly. A violation, an unresolvable type, a non-public or abstract type, or a missing canonical constructor unloads the load context and fails the load with a message naming the module and the type. The scan is not repeated per call at runtime.

`EnumerateReferencedTypes` gains exactly one new source: the parameter types of every public constructor. Base types, interfaces, properties, method signatures, static fields, nested types and the assembly reference list remain uninspected.

### Rationale

Research counted three checked entry points against eight unchecked ones, and the unchecked set included every provider path — which is the hot path. Repeating a reflection scan on each of those calls would put per-request reflection on member detail rendering to verify a property that cannot change after the assembly is loaded, so the check belongs at load time. Making it a rejection rather than a warning follows the precedent set on 2026-09-02 for unknown capability keys and for the same reason: a plugin that reports installed and healthy while its contribution is unreachable is the worst available outcome.

Constructor parameters are added to the scan because that is precisely the surface CORE-18 opens. Today a provider taking `ApplicationDbContext` is caught only indirectly, and only if it stores the value in an instance field — which is what `Fixtures/ForbiddenDbContextPluginFixture.cs:10-13` happens to do. Nothing else is added, because each further surface carries its own false-positive risk and would reject plugins over references the host does not in fact let them use.

## 2026-09-03 - The `plugins/**` Boundary Guardrail Is a Source Scan, Not Reflection

### Decision

The architecture test that fails the build when plugin code references the core `DbContext` or a forbidden core namespace is implemented as a source scan over `plugins/**/*.cs` and `plugins/**/*.cgcs` (excluding `bin` and `obj`), extending `tests/ClubGear.ArchitectureTests/PluginBoundaryTests.cs` and reusing its `ResolveRepositoryRoot()` helper. It reads the forbidden-namespace list from `PluginIsolationScanner` rather than repeating the strings. It first asserts that all five plugin module directories exist and fails with an explicit "submodules not checked out" message otherwise.

### Rationale

`plugins/IamOidc/IamOidc.Plugin.csproj` is not referenced by `tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj:28-35`, so a reflection-based test over loaded plugin assemblies would silently skip the one submodule whose code sits closest to the host — three identity providers plus a shared base class. A guardrail that skips a module without saying so is worse than no guardrail, because it is read as coverage. The source scan covers all five and needs no project reference. The explicit submodule-presence assertion exists because a scan of an empty directory passes vacuously; CI sets `GIT_SUBMODULE_STRATEGY: recursive` (`.gitlab-ci.yml:29-31`) and already validates the same five paths at `:136-148`, so the assertion costs nothing there and catches the local case where a developer forgot to init.

## 2026-09-03 - Bounded Drain Before Unload, Generalised From Background Jobs to All Contributions

### Decision

CORE-17, generalising the 2026-06-17 "Cancellation and Drain Sequence" decision. A singleton `IPluginActivityTracker` counts in-flight plugin work per module. A token is taken by `PluginContributionActivator.Rent` (released when the lease is disposed) and by `PluginRuntimeAdapter.RunAsync`/`InvokeAsync` around the capability invocation; plugin HTTP routes inherit it because they already funnel through `InvokeAsync`.

`IPluginRuntimeRegistry.Unregister` becomes `Task<PluginUnloadOutcome> UnregisterAsync(string, CancellationToken)` and `Register` becomes `RegisterAsync`. `UnregisterAsync` seals the module and removes it from `_plugins` under the lock, then leaves the lock, awaits the drain for a configured budget, logs a warning naming the module id and the in-flight count if the budget expires, and calls `loadContext.Unload()` unconditionally. The replace path inside `RegisterAsync` runs the same helper on the context it displaces. The budget is `Plugins:Drain:TimeoutSeconds`, default 5, bound to a new `PluginRuntimeOptions`; `PluginBackgroundJobRunner` drops its hard-coded `TimeSpan.FromSeconds(5)` and reads the same option, and its previously silent timeout branch now logs.

`PluginLifecycleService.DeactivateAsync` keeps its pinned ordering — endpoint unregister, `StopJobsForModuleAsync`, then registry unregister — and, when the outcome reports an undrained module, writes a note to `PluginStatusRecord.LastError` instead of clearing it. Deactivation itself still reports success.

### Rationale

The 2026-06-17 decision closed the window for background jobs only; providers are called on the request hot path and open a far wider one. Counting in-flight calls is sufficient and no cache invalidation is needed, because research confirmed that nothing anywhere holds a provider instance beyond the call that created it — the lease window and the reachability window are the same window.

The drain lives in the registry rather than in `PluginLifecycleService` so that every unload path is covered. `PluginRegistry.Unload` is reached both from `Unregister` and from the replace branch of `Register`; putting the drain only in the lifecycle service would leave the replace branch unloading without one.

Unloading regardless of the outcome is deliberate: a plugin that never returns must not make itself un-deactivatable, and must not pin the host. The alternative — waiting indefinitely — turns a misbehaving plugin into an outage. Persisting the overrun on the record rather than logging alone was chosen because a repeatedly overrunning plugin is an operational fact an administrator needs without reading logs; it is surfaced in the plugin admin detail view through the existing `LastError` rendering, so no new UI or migration is required.

This decision **supersedes** the earlier position, recorded at `ADRs/consolidated/finance-plugin.adr.md:467`, that `PluginRegistry` holds no dependencies and no logger. The registry now takes `IPluginActivityTracker`, `IOptions<PluginRuntimeOptions>` and `ILogger<PluginRegistry>`. All three are singletons, so the registry stays a singleton with no scoped coupling, and the original concern — that a scoped dependency would be captured by a singleton — still holds and is still respected. A drain that cannot report which module overran fails the CORE-17 acceptance criterion, so a logger is no longer optional.

## 2026-09-03 - The CORE-18 Break Ships as a Contract-Version Rejection at Activation, Not Only at Install

### Decision

`ContractVersion.Current` moves to `1.13.0` and `ContractVersion.MinimumSupported` moves to `1.13.0`. `PluginLoader.LoadAsync` gains `IContractCompatibilityService` and validates `module.Manifest.RequiredContractVersion` immediately after the manifest-key check and before `RegisterContributions`, failing the load through the existing `PluginLoadResult.CreateFailure` channel. `docs/plugin-contract.md` gains a `1.13.0` section in the same shape as the 1.12.0 one, recording the removal and rewriting the "must have a parameterless constructor" sentence. `Slice1IdentityProviderContractTests` is updated to assert `1.13.0`/`1.13.0`. Plugin-side adoption for CarInfo, Finance, ServiceBook, Inventar and IamOidc is filed in `docs/backlog/plugins.md` as plugin-owned items; no submodule is edited in this iteration.

### Rationale

The floor moves because a shape is genuinely removed, which is the exact condition `docs/plugin-contract.md:37` names — unlike 1.11.0 and 1.12.0, which only added. A major bump to `2.0.0` was considered and rejected as heavier than the in-tree precedent: it would reset a version line that has run to 1.12.0 and force every plugin to re-declare against a new generation for a change that touches one construction rule.

The load-time gate is the load-bearing part. `IContractCompatibilityService` is invoked from exactly one production place, `PluginInstallerService.cs:185`, so raising the floor alone blocks new installs while leaving every already-installed plugin loading at startup as before. Without the gate the break would be discovered at step 9 of the load as "no `(IPluginHostContext)` constructor" — true, but the wrong diagnosis — or, worse, at the call sites, where research found the failure surface is inconsistent: silently skipped in `MemberPluginSlotService.cs:111-114` and `PluginAuditSinkService.cs:28-31`, but thrown as an `InvalidOperationException` at login in `IdpClaimsEnricher.cs:40-48`. A user discovering a contract break as a failed login is not an acceptable diagnostic.

**Implementation note (2026-09-05).** `PluginLoader` takes `IContractCompatibilityService` as an *optional* constructor parameter defaulting to the real `ContractCompatibilityService`, rather than as a required one. The host resolves the registered implementation through DI either way, and the default means the floor applies everywhere unless a test deliberately substitutes — which `ServiceBookDependencyActivationSmokeTests` does, and is what lets it reach the constructor-shape rejection specifically. This follows the same pattern the CORE-17 work applied to `PluginRegistry`, `PluginRuntimeAdapter` and `PluginBackgroundJobRunner`, and avoided a purely mechanical edit at roughly twenty existing `new PluginLoader(store, logger)` call sites.

The consequence is accepted and stated rather than mitigated: all five shipped submodules stop activating at HEAD until their own repositories adopt. `FinanceBugfixTypeloadTests` and `ServiceBookDependencyActivationSmokeTests`, which today round-trip real submodule types, are rewritten to assert the new rejection, making them the pinning tests for the break instead of casualties of it.
