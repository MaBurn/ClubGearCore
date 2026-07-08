# ADR: Plugin Member Filter

## Purpose

Persistent architectural decisions for the plugin-member-filter iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-13 - Core Scope: Full Generic-Core Pipeline (ADR 0001 GR-002 Approved)

### Decision

Implement the full generic-core plugin member filter pipeline with all six core changes: `IPluginMemberFilterProvider` contract, `PluginMemberSlotKind.MemberFilter` enum value, `PluginExtensionPoints.MemberFilter` constant, `MemberSearchFilterViewModel.PluginFilters` dictionary, `IPluginMemberFilterService` (dedicated service), controller wiring, and dynamic `_SearchAndFilters.cshtml` rendering.

### Rationale

The generic-core approach means any future plugin can declare filterable fields without requiring a core code change. The alternative (ad-hoc per-plugin controller logic) would not scale beyond one plugin and would require touching MembersController for every new plugin filter. All six core changes are load-bearing: the contract defines the plugin API; the enum and extension point constant make the contribution type discoverable; the view model extension carries filter state through POST redirect; the dedicated service isolates filter aggregation logic; and the Razor partial renders the dynamic UI. ADR 0001 GR-002 approval was obtained before implementing.

---

## 2026-06-13 - Filter State Carrier: Dictionary on MemberSearchFilterViewModel

### Decision

Add `Dictionary<string, string> PluginFilters { get; set; }` (initialized to empty, `StringComparer.OrdinalIgnoreCase`) to `MemberSearchFilterViewModel`. Extend `RedirectToIndex` to forward each key-value pair as `pluginFilters[key]` route values. Extend the POST `Index` action to bind the dictionary from the submitted form.

### Rationale

`MemberSearchFilterViewModel` is the established filter state carrier. Keeping plugin filters in the same model makes the GET/POST/Redirect cycle consistent with how `Search` and `Status` already flow. The alternative of binding plugin filters as a separate `[FromQuery] Dictionary<string, string>` parameter on the GET action would require a parallel pass-through path and split the filter state across two objects, increasing complexity and test surface. A single flat `Dictionary<string, string>` with dot-separated keys (e.g., `carinfo.hasAnyCar`) is sufficient for the current and foreseeable plugin filter set.

---

## 2026-06-13 - CarInfo Reference Filter Scope: Has-Any-Car + Make=X on Static Schema

### Decision

The CarInfo reference implementation provides exactly two `PluginMemberFilterDefinition` entries: `carinfo.hasAnyCar` (checkbox, `AcceptsValue = false`) and `carinfo.make` (text input, `AcceptsValue = true`, label "Marke eingeben"). Both queries target only the static `plugin_clubgear_plugin_carinfo_cars` table. Dynamic schema (`field_definitions` / `field_values`) is explicitly out of scope for this iteration.

### Rationale

The static schema columns (`MemberId`, `Make`) are available in all CarInfo installations and require a single-table query, making them the lowest-risk reference implementation. Dynamic field filtering requires a two-table join and runtime knowledge of active `FieldKey` values, which adds complexity without demonstrating the generic filter pipeline. The two chosen filters together demonstrate both the boolean/presence filter pattern (`AcceptsValue = false`) and the value-input filter pattern (`AcceptsValue = true`), which is sufficient to validate the full contract.

---

## 2026-06-13 - Filter Evaluation Point: Post-Query In-Memory Set Intersection

### Decision

Plugin filters are evaluated after `IMemberFeatureService.GetListAsync` returns the full (text-search-filtered) member list. Each active plugin filter provider returns a `HashSet<int>` of matching member IDs. The host intersects this set with the running candidate list in memory. The final list is the intersection of all active plugin filter results.

### Rationale

The existing architecture already applies the `status` filter in-memory after the EF Core query returns. Post-query intersection is consistent with this established pattern and avoids coupling the core EF query to plugin-owned SQLite tables. No production member count target is documented; the architecture is suitable for club-scale deployments. If future scale requires pre-query SQL joins, the `IPluginMemberFilterProvider.GetMatchingMemberIdsAsync` contract can be augmented with an `IQueryable<Member>` overload without breaking existing providers.

---

## 2026-06-13 - SQL Safety: ValidateSql + Parameterized Queries Cover Filter Pattern

### Decision

Plugin filter SQL executes exclusively through `IPluginDataStore.QueryAsync`, which runs `PluginSchemaNamePolicy.ValidateSql` before any execution and builds parameterized `DbParameter` objects for all user-supplied values. No additional SQL injection guardrail is required for the filter pattern.

### Rationale

`ValidateSql` already checks that every referenced table name starts with the plugin's own prefix. Parameterized queries through `IPluginDataStore.QueryAsync` prevent injection of user-supplied filter values (e.g., the `make` string). The `EnsureIsolated` check in `PluginRuntimeAdapter` blocks plugins from bypassing `IPluginDataStore` by accessing `ApplicationDbContext` or core namespaces directly. The combination of these three guards is sufficient.

---

## 2026-06-13 - Service Location: Services/Plugins/ Namespace

### Decision

`PluginMemberFilterService.cs` is placed in `Services/Plugins/` (namespace `ClubGear.Services.Plugins`), not in `Services/Core/` where `MemberPluginSlotService.cs` lives. `IPluginMemberFilterService` is placed in `Services/Abstractions/`.

### Rationale

`PluginMemberFilterService` is plugin infrastructure: it iterates the plugin registry, invokes the runtime adapter, and aggregates plugin-provided results. This responsibility is analogous to other plugin infrastructure services (`PluginMigrationRunner`, `PluginAdminQueryService`) that live in `Services/Plugins/` sub-namespaces. `MemberPluginSlotService` in `Services/Core/` is a core feature service consumed by `MembersController`; the filter service plays the same consumer-facing role but its primary logic is plugin dispatch. Placing it in `Services/Plugins/` reinforces that boundary.

---

## 2026-06-13 - RegisteredPluginRuntime Record Extension Strategy

### Decision

`RegisteredPluginRuntime` gains a new optional positional parameter `IReadOnlyList<PluginMemberProviderContribution> MemberFilterProviders = []` appended at the end of the record constructor. The `PluginContributionCollector` in `PluginLoader.cs` gains a `_memberFilterProviders` list and exposes it as `MemberFilterProviders`. `IPluginContributionSink` gains `AddMemberFilterProvider(PluginMemberProviderContribution contribution)`. Existing call sites that construct `RegisteredPluginRuntime` positionally (in `PluginLoader.cs` and test fixtures) must be updated; the default value makes the parameter optional when using named arguments.

### Rationale

The filter contribution type is structurally identical to a `PluginMemberProviderContribution` — it carries a `SlotKind`, a `ProviderType`, and an `Order`. Reusing the existing record and sink method shape minimizes new surface area and keeps the `PluginMemberSlotKind.MemberFilter` enum value as the discriminator. A separate record type (e.g., `PluginMemberFilterContribution`) would duplicate the same fields without benefit. The `AddMemberFilterProvider` sink method name makes the contribution intent explicit in plugin module code.

---

## 2026-06-13 - IPluginMemberFilterProvider Does Not Inherit a Base Interface

### Decision

`IPluginMemberFilterProvider` is defined as a standalone interface with no base interface. The user prompt referenced `IPluginProvider` as a base type, but no such interface exists in the codebase. All existing provider interfaces (`IMemberDetailCardProvider`, `IMemberEditTabProvider`, `IMemberStatusBadgeProvider`, `IMemberActionProvider`) are also standalone. The filter provider follows the same pattern.

### Rationale

Introducing a marker base interface `IPluginProvider` solely for `IPluginMemberFilterProvider` would be a broader contract change not warranted by this iteration. The existing providers have been type-safe and discoverable without a common base. `CreateMemberProvider<TProvider>` in `IPluginRegistryReader` already handles type-safe provider instantiation via generic constraints.
