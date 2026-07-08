# ADR: Plugin Categories

## Purpose

Persistent architectural decisions for the plugin-categories iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-09 - Category Property Added as Init-Only, Not Positional

### Decision

`PluginManifest.Category` is added as a non-positional `init`-only property with a default value of `"General"`, not as a new parameter in the positional primary constructor.

### Rationale

`PluginManifest` is a `sealed record` in the Contracts package consumed by external plugin assemblies. Adding a new required positional parameter would be a breaking binary change for all external assemblies that call `new PluginManifest(...)` with positional arguments. An init-only property with a default leaves the primary constructor signature unchanged, making the addition backward-compatible with pre-compiled plugins.

---

## 2026-06-09 - Category Default Assigned at Parse Time, Not Installer or Query Layer

### Decision

`PluginManifestParser` is the sole layer that assigns the `"General"` default when `"category"` is absent from `plugin.json`. No fallback logic is duplicated in `PluginInstallerService` or `PluginAdminQueryService`.

### Rationale

This follows the established precedent set by `author` and `license` optional fields, which are both assigned their defaults (`"Unknown"` and `"Unspecified"`) in `PluginManifestParser` via `ReadOptionalString(...) ?? <default>`. Centralising the default in the parser means that anywhere a `PluginManifest` is produced the property is already populated with a valid, non-null value.

---

## 2026-06-09 - Category Persisted in DB Column, Not Derived at Runtime

### Decision

A new `Category TEXT NOT NULL DEFAULT 'General'` column is added to the `PluginStatusRecords` table via an additive SQLite migration. `PluginStatusRecord` gains the corresponding `Category` property, and `PluginInstallerService.PersistSuccessAsync` maps `manifest.Category` to it.

### Rationale

For installed-but-not-loaded plugins, the runtime module is unavailable, so `runtimeModule?.Manifest.Category` cannot supply the value. Without DB persistence, the Admin UI would show `"General"` for all such plugins regardless of what their manifest declared at install time. Persisting the column ensures the declared category is faithfully displayed at query time for all plugin states. The established pattern for additive schema changes in this codebase is an idempotent `ALTER TABLE ... ADD COLUMN ... DEFAULT` migration, which is exactly what `AddPluginPackagePath_202605310151` does.

---

## 2026-06-09 - Category Surfaced as Badge in Plugin Column, Not New Table Column

### Decision

The category is rendered as a small Bootstrap `badge bg-info` inside the existing "Plugin" `<td>` cell in `_PluginStatusTable.cshtml`, alongside the existing `DisplayName`, `ModuleId`, and `Author·License` lines. No new `<th>` column is added and no row-grouping section headers are introduced.

### Rationale

The table already has seven columns. The existing "Plugin" cell already displays three stacked pieces of metadata. Adding a badge there requires no structural changes to the table, no view-model grouping, and no changes to the `<thead>`. A grouped/section-header layout would require breaking the flat `@foreach` and pre-grouping the model; that is a larger view change not justified by the current requirements. The badge approach is consistent with how the Status column renders multiple states and can be extended to grouping in a future iteration if needed.

---

## 2026-06-09 - ContractVersion Bumped to 1.3.0 (Minor)

### Decision

`ContractVersion.Current` advances from `1.2.0` to `1.3.0`.

### Rationale

`PluginManifest` is part of the public Contracts package. Adding a new observable property (`Category`) to the record is a backward-compatible additive change. Under semantic versioning, additive changes to a public API surface warrant a minor version bump. A patch bump (`1.2.1`) would be inappropriate because the API surface changes. No existing plugin's `requiredCoreVersion` (e.g. `">=1.0.0"`) is broken by this increment; the compatibility validator still passes them.
