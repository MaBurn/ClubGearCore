# ADR: Plugin Navigation Contributions

## Purpose

Persistent architectural decisions for the plugin-nav-contributions iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-13 - New Value Type: PluginNavEntry in Contracts

### Decision

A new sealed record `PluginNavEntry(string Label, string Icon, string Route, string? RequiredPermission, int SortOrder)` is added to `Contracts/Plugin/PluginNavEntry.cs` in the `ClubGear.Plugin.Contracts` namespace.

### Rationale

Plugins must be able to declare navigation entries at contribution time (inside `RegisterContributions`), not at runtime discovery. Placing the type in the contracts assembly ensures the type is available to both the host and plugin assemblies without the plugin taking a dependency on host internals. The record is immutable (sealed record) to match all other contribution types in the contracts assembly.

---

## 2026-06-13 - Default Interface Method for AddNavEntries

### Decision

`IPluginContributionSink` receives a new method with a default no-op body:
`void AddNavEntries(IReadOnlyList<PluginNavEntry> entries) { }`

### Rationale

The existing `AddAdminPanelProvider` method uses the same pattern. Because `PluginAssemblyLoadContext.Load()` explicitly redirects the contracts assembly to `AssemblyLoadContext.Default`, every plugin load context shares the same contracts assembly instance from the host. The default-body IL is always resolved from the host's copy. Existing compiled plugin DLLs that do not call or implement `AddNavEntries` dispatch to the default no-op body without recompilation. No version bump is required for runtime correctness.

---

## 2026-06-13 - NavMain Extension Point Constant

### Decision

`PluginExtensionPoints.NavMain = "nav.main"` is added as a public constant and registered in `KnownValuesInternal`.

### Rationale

`PluginManifestParser.ValidateExtensionPoints` calls `PluginExtensionPoints.IsKnown(value)` for every declared extension point during manifest parsing. Without `"nav.main"` in the known set, any plugin manifest declaring this extension point would fail validation with an error. The constant is added to both the `const` field and the `KnownValuesInternal` HashSet simultaneously.

---

## 2026-06-13 - RegisteredPluginRuntime: 9th Positional Parameter

### Decision

`RegisteredPluginRuntime` gains a ninth positional parameter `IReadOnlyList<PluginNavEntry> NavEntries`. All 6 construction sites are updated: the production path in `PluginLoader.cs` passes `collector.NavEntries`; the 5 test-only sites pass `Array.Empty<PluginNavEntry>()` or `sink.NavEntries` as appropriate.

### Rationale

`RegisteredPluginRuntime` is the single in-memory record representing a loaded plugin's runtime state. All existing contribution collections (`Routes`, `Services`, `MemberProviders`, `BackgroundJobs`) are stored here. Nav entries follow the same pattern. There is no `with`-expression shortcut that avoids updating construction sites — each site must be updated. The blast radius is 6 sites, all in the same repository.

---

## 2026-06-13 - IPluginNavEntryService: Claims-Only Permission Pre-Filter

### Decision

`IPluginNavEntryService.GetVisibleNavEntriesAsync(ClaimsPrincipal, CancellationToken)` filters nav entries using a claims-only check: `user.Claims.Any(c => c.Type == "permission" && (c.Value == entry.RequiredPermission || c.Value == PermissionKeys.Wildcard))`. No async DB call is made. The layout renders every entry returned without additional checks.

### Rationale

The layout is rendered on every page load. `DatabasePermissionService.HasPermissionAsync` makes async DB calls (`_userManager.GetUserAsync`, `_dbContext.RolePermissions.AnyAsync`). Running that per nav entry on every page request is disproportionate overhead for a nav bar. The existing layout already uses claims-only scanning for `hasAdminPermissionClaim` (lines 46-50 of `_Layout.cshtml`). The `"permission"` claim type is the same one `DatabasePermissionService` checks first (line 37 of `DatabasePermissionService.cs`) before falling back to the DB. This is the established fast path for in-request permission checks in ClubGear. Wildcard (`PermissionKeys.Wildcard`) is honored identically. `IPluginNavEntryService` is registered as `AddScoped` (not singleton) to match the layout's per-request scope and to be consistent with other permission-aware services.

---

## 2026-06-13 - Route Format: Absolute Path String

### Decision

`PluginNavEntry.Route` is a plain `string` containing an absolute path (e.g., `/inventar`). The host renders `<a href="@entry.Route">` with no further processing.

### Rationale

Plugins like Inventar or Vereinszeitung own their own route registration via `AddRoute`. They know their absolute paths. Using a plain string requires no reflection, no route helper invocation at nav render time, and no dependency on the host's routing infrastructure from the contracts layer. This is the simplest contract that meets the requirement.

---

## 2026-06-13 - PluginAdminStatusViewModel: NavEntryCount as Scalar, Detail Card for Structured List

### Decision

`PluginAdminStatusViewModel` gains a 25th positional parameter `int NavEntryCount`. The "Status & Laufzeit" card in `Detail.cshtml` shows the scalar count. A new full-width "Navigation" card in `Detail.cshtml` shows the structured list (Label, Icon, Route, Berechtigung, Reihenfolge) sourced from `IPluginRegistryReader` injected directly into the view. If the plugin is not runtime-registered, the card shows a "not active" notice.

### Rationale

All existing contribution types (Routes, Services, MemberProviders, BackgroundJobs) are surfaced as scalars in the Status card, consistent with the current design. The structured nav-entry list is a new addition specific to nav contributions; it follows the same card pattern used for Permissions and Extension Points. The view injects `IPluginRegistryReader` directly rather than expanding `PluginAdminStatusViewModel` with a `IReadOnlyList<PluginNavEntry>` property, keeping the view model a pure data-transfer record without holding domain types from the contracts namespace in its public surface.

---

## 2026-06-13 - Contract Version Not Bumped

### Decision

`ContractVersion.Current` remains at `1.4.0`. No version bump accompanies this iteration.

### Rationale

`ContractCompatibilityService.Validate` checks only major version parity and minimum version floor. Adding `AddNavEntries` with a default no-op body is purely additive at the IL level. Existing plugins compiled against `1.0.0`–`1.4.0` continue to load without recompilation. A minor bump to `1.5.0` is not required for runtime correctness. If a bump is later mandated as a documentation convention under ADR 0001 GR-009, it should be applied in a dedicated housekeeping commit.
