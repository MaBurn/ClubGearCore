# ADR: Plugin Admin Delete, Detail, and Activation Risk Check

## Purpose

Persistent architectural decisions for the plugin-admin-delete-detail-risk iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-10 - New IPluginUninstallService Abstraction, Not Extension of IPluginLifecycleService

### Decision

Uninstall logic is placed in a new `IPluginUninstallService` interface and `PluginUninstallService` implementation rather than adding an `UninstallAsync` method to the existing `IPluginLifecycleService`.

### Rationale

`IPluginLifecycleService` is concerned with runtime activation state (load, activate, deactivate). Uninstall is a destructive multi-step write path (deactivate, DB row delete, optional table teardown, filesystem cleanup) that crosses more boundaries than a lifecycle state transition. Separating the concern avoids growing the lifecycle service into a Swiss-army-knife class and keeps each abstraction testable in isolation. The delete controller action will depend only on `IPluginUninstallService`, while the existing `Activate`/`Deactivate` actions continue depending only on `IPluginLifecycleService`.

---

## 2026-06-10 - DeleteAsync Added to IPluginStatusStore and IPluginPackageStore

### Decision

`IPluginStatusStore` gains `Task DeleteAsync(string key, CancellationToken ct = default)`. `IPluginPackageStore` gains `Task DeleteAsync(string pluginKey, CancellationToken ct = default)`. Both existing implementations are extended to match.

### Rationale

Research confirmed that no delete path exists anywhere in the codebase for either abstraction. Adding the method to the interface is the correct extension point rather than leaking raw `ApplicationDbContext` or `Directory.Delete` calls into an orchestrating service that should not own those concerns. This follows the established pattern: the store interfaces own all DB and filesystem I/O for their respective resources.

---

## 2026-06-10 - Data Removal Uses Raw SQL DROP TABLE via sqlite_master Inspection

### Decision

When `removeData: true` is requested, `PluginUninstallService` queries `sqlite_master` for all table names that start with the plugin's `TablePrefix` value (read from `PluginMigrationStates`), then issues `DROP TABLE IF EXISTS` for each found table inside a transaction, before removing the `PluginMigrationState` rows with EF `RemoveRange`.

### Rationale

`PluginSchemaNamePolicy.GetTablePrefix` produces a deterministic prefix (`plugin_{normalised_key}_`). However, the plugin may have created multiple tables with that prefix; the exact set of table names is not recorded separately—only migration IDs are recorded. Inspecting `sqlite_master` is the only reliable way to discover all tables for a prefix without requiring plugins to declare a teardown manifest. The approach mirrors how the existing `ValidateSql` method in `PluginSchemaNamePolicy` operates on table names at write time. All teardown SQL runs in a single transaction so a partial failure does not leave the migration state rows removed but tables intact.

---

## 2026-06-10 - Signature Status Excluded From This Iteration

### Decision

No `SignatureVerified` boolean column or signer identity field is added to `PluginStatusRecord` or `PluginAdminStatusViewModel` in this iteration.

### Rationale

Research confirmed that the signer public key PEM is passed to `PluginInstallerService`, used only within `IPluginIntegrityVerifier.Verify`, and then discarded. Persisting the outcome would require a new `PluginStatusRecord` column, an idempotent schema migration, and a change to `PluginInstallerService.PersistSuccessAsync`. The task specifies "Signaturstatus" in the context of the detail view but the research shows there is no data to display without first building that new persistence path. Adding an empty or misleading "unbekannt" field adds noise without value. `PackageHash` (already stored) is surfaced instead as the integrity artifact and is sufficient for identifying the package at rest.

---

## 2026-06-10 - AppliedMigrationCount Is Computed at Query Time, Not Stored

### Decision

`PluginAdminStatusViewModel` gains an `int AppliedMigrationCount` field populated by a synchronous `.Count()` EF LINQ call on `PluginMigrationStates` inside `PluginAdminQueryService.GetPluginStatus` and `GetPluginStatuses`.

### Rationale

The migration count is a live aggregate that can change each time a plugin is activated and new migrations are run. Storing it as a column on `PluginStatusRecord` would require updating it at activation time, introducing a write-side coupling. Computing it at read time from `PluginMigrationStates` is authoritative and avoids any synchronisation risk. The `GetPluginStatus` method is already synchronous (mirrors the synchronous store `GetByKey`/`List` pattern), so using synchronous `.Count()` is internally consistent with the existing pattern rather than introducing async for a single scalar aggregation.

---

## 2026-06-10 - Activation Risk Check Implemented as Client-Side window.confirm, Not Server-Side GET Confirmation View

### Decision

The activation risk confirmation for `category == "technical"` plugins is implemented by adding a `data-confirm-if-technical="@plugin.Category"` attribute to the Activate form and extending `plugin-admin.js` to show `window.confirm(...)` when this attribute equals `"technical"`. No new GET action, no confirmation view, and no intermediate redirect are added.

### Rationale

Research confirmed that `Activate` is already a direct `POST` with no intermediate step. Introducing a server-side GET confirmation view would require: a new controller action, a new partial or full view, and a two-step round-trip. The codebase already uses `data-confirm` on the Deactivate form with `window.confirm` as the interstitial guard—this is the established convention. Reusing the same client-side pattern for the technical-plugin risk check is consistent with the existing UX, requires only a one-line addition to `plugin-admin.js`, and avoids adding a new controller surface that has no business logic of its own. The confirmation guards against accidental activation; it is not a security gate, so client-side is appropriate.

---

## 2026-06-10 - Delete Button Visible Only When Plugin Is Installed and Inactive

### Decision

The Delete action button in `_PluginStatusTable.cshtml` is rendered only when `plugin.IsInstalled && !plugin.IsActive`.

### Rationale

Deleting an active plugin while it is loaded in the runtime would leave the in-process module in an inconsistent state (registered routes and services pointing at an unloaded context). The service layer enforces this guard (it calls `DeactivateAsync` first), but the UI pre-emptively hides the button for active plugins to avoid presenting an action that would either fail or require an implicit deactivation the admin has not acknowledged. If the admin wants to delete an active plugin they must deactivate it first. This is the least-surprise flow.

---

## 2026-06-10 - No New Schema Migration File Required for This Iteration

### Decision

No new `Data/Migrations/` file is needed and `ApplicationSeeder.EnsureSqliteSchemaCompatibilityAsync` is not modified.

### Rationale

All new data surfaced in the detail view (`PackageHash`, `AppliedMigrationCount`) comes from columns and tables that already exist (`PackageHash` on `PluginStatusRecords`, `PluginMigrationStates`). The uninstall path only deletes rows and plugin-prefixed tables, it does not alter or create schema objects in the core `ApplicationDbContext`. Therefore no additive migration is required for this iteration.
