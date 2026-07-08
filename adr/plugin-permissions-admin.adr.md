# ADR: Plugin Permissions Admin

## Purpose

Persistent architectural decisions for the plugin-permissions-admin iteration.

Append new entries. Do not rewrite prior history.

---

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

---

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

---

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

---

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

---

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

---

## 2026-06-11 - No New DB Migration Required for This Iteration

### Decision

This iteration adds no new database columns or tables. All changes operate on
existing `AppPermissions` and `AppRolePermissions` tables.

### Rationale

The `AppPermission` model already has `Key`, `Description`, and `Category`
columns. The `AppRolePermission` model already has `RoleName` and `PermissionKey`.
The richer metadata from `PluginStatusRecord` is projected into these existing
columns by the updated seed task on first run, requiring no schema change.

---

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
