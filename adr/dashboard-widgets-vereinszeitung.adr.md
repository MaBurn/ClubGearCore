# ADR: Dashboard Widgets and Vereinszeitung Plugin

## Purpose

Persistent architectural decisions for the dashboard-widgets-vereinszeitung iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-16 - IDashboardWidgetProvider Placed in Contracts Assembly with IPluginHostContext Parameter

### Decision

A new interface `IDashboardWidgetProvider` is added to `Contracts/Plugin/IDashboardWidgetProvider.cs`. Its single method signature is `Task<IReadOnlyList<DashboardWidgetSlot>> GetWidgetsAsync(IPluginHostContext hostContext, CancellationToken cancellationToken = default)`. The associated slot record `DashboardWidgetSlot(string Key, string Title, string Body, string? PermissionKey, int SortOrder = 0)` is co-located in the same file.

### Rationale

Every existing provider interface (`IMemberDetailCardProvider`, `IAdminFunctionPanelProvider`, `IPluginPageProvider`) takes `IPluginHostContext` as a parameter regardless of whether the provider performs simple reads or complex operations. Consistency with all existing provider interfaces is the deciding factor. `DashboardWidgetSlot` mirrors `MemberDetailCardSlot` structurally (Key, Title, Body, Order) and adds `PermissionKey` as confirmed in the task requirements. Both types belong in the contracts assembly to give plugin authors access without depending on host internals.

---

## 2026-06-16 - PluginWidgetProviderContribution as 10th Positional Parameter of RegisteredPluginRuntime

### Decision

`RegisteredPluginRuntime` gains a 10th positional parameter `IReadOnlyList<PluginWidgetProviderContribution> WidgetProviders`. `PluginWidgetProviderContribution(string ProviderType, int Order = 0)` is added to `PluginContributions.cs`. `IPluginContributionSink` gains `void AddWidgetProvider(PluginWidgetProviderContribution contribution) { }` with a default no-op body. `PluginLoader.PluginContributionCollector` adds a backing list and overrides the method. All 6 `RegisteredPluginRuntime` construction sites are updated in the same commit.

### Rationale

Widget contributions are structural metadata declared at load time via `RegisterContributions`, analogous to nav entries (`NavEntries` as 9th positional parameter, added in the plugin-nav-contributions iteration). Storing them in the runtime record avoids re-running `RegisterContributions` on every dashboard request. The default no-op body on `IPluginContributionSink` means existing compiled plugins dispatch to the default without recompilation. The blast radius of 6 construction sites is identical to the NavEntries addition and is accepted.

---

## 2026-06-16 - PluginDashboardWidgetService: Reads from RegisteredPluginRuntime.WidgetProviders, Invokes via IPluginRuntimeAdapter

### Decision

`PluginDashboardWidgetService` (in `Services/Core/`) implements `IPluginDashboardWidgetService` (in `Services/Abstractions/`). It injects `IPluginRegistryReader`, `IPluginRuntimeAdapter`, and `ILogger<PluginDashboardWidgetService>`. `GetWidgetsAsync` iterates `GetRegisteredPlugins()`, reads `runtime.WidgetProviders` from the pre-loaded runtime record, instantiates each `IDashboardWidgetProvider` via `CreateMemberProvider<IDashboardWidgetProvider>`, invokes via `InvokeAsync`, and applies a per-slot `bridge.HasPermissionAsync(slot.PermissionKey)` guard for slots with a non-null `PermissionKey`. Registered as `services.AddScoped<IPluginDashboardWidgetService, PluginDashboardWidgetService>()`.

### Rationale

Widget content (Title, Body) requires async provider invocation — it cannot be pre-computed at load time. Reading `WidgetProviders` from the runtime record (rather than re-collecting via a local `ContributionSink`) avoids calling `RegisterContributions` on every home page render, following the same reasoning as `PluginNavEntryService` for nav entries. Permission filtering at the per-slot level (not at the contribution level) is necessary because a single provider may return multiple slots with different permission requirements. `HasPermissionAsync` is used (not claims-only) to support DB-backed role-permission grants, consistent with how `PluginPageService` checks `ListPermission`.

---

## 2026-06-16 - DashboardWidget Extension Point Constant

### Decision

`PluginExtensionPoints.DashboardWidget = "dashboard.widget"` is added as a new `public const string` field, and `"dashboard.widget"` is added to `KnownValuesInternal`. `ContractVersion.Current` is not bumped.

### Rationale

`PluginManifestParser.ValidateExtensionPoints` calls `PluginExtensionPoints.IsKnown` for every declared extension point. Without this entry, any plugin declaring `"dashboard.widget"` (including the Vereinszeitung plugin) would be rejected at install time. Adding a constant and a HashSet entry follows the identical pattern established in plugin-nav-contributions (`NavMain`) and plugin-generic-pages (`PageGeneric`). No `ContractVersion` bump is required per the precedent set in those iterations.

---

## 2026-06-16 - HomeController.Index Becomes Async with IPluginDashboardWidgetService Injection

### Decision

`HomeController.Index()` is refactored from `IActionResult` to `async Task<IActionResult>`. `IPluginDashboardWidgetService` is added as a constructor dependency. The method calls `await _dashboardWidgetService.GetWidgetsAsync(User, ct)` and returns `View(new HomeIndexViewModel { Widgets = widgets })`. `HomeIndexViewModel` is a new `sealed class` in `Models/Home/` with a single `IReadOnlyList<DashboardWidgetView> Widgets` property defaulting to `Array.Empty<DashboardWidgetView>()`.

### Rationale

`HomeController` is the host's primary landing page controller. Dashboard widgets are a user-facing aggregation requiring async DB/runtime calls through the plugin adapter. Making `Index` async is the correct pattern; the controller already uses `CancellationToken` via its base class. `DashboardWidgetView` (in `Services/Abstractions/`) wraps the contracts-layer `DashboardWidgetSlot` with host metadata (`ModuleId`, `PluginDisplayName`, `SortOrder`), following the identical view-record pattern used for `MemberPluginDetailCardView` and `MemberPluginStatusBadgeView`.

---

## 2026-06-16 - PluginAdminStatusViewModel Gains 26th Positional Parameter WidgetProviderCount

### Decision

`PluginAdminStatusViewModel` gains a 26th positional parameter `int WidgetProviderCount` after `NavEntryCount`. The construction site in `PluginAdminQueryService` passes `runtime?.WidgetProviders.Count ?? 0`. The 4 test construction sites pass `0`.

### Rationale

All existing runtime contribution counts (Routes, Services, MemberProviders, BackgroundJobs, NavEntries) are surfaced in the admin status view model as scalars. Widget provider count follows the same pattern for observability. Consistency with the existing scalar-count pattern is the sole deciding factor.

---

## 2026-06-16 - Vereinszeitung Plugin: Two Migrations, Three Providers, plugin.json Declares Three Extension Points

### Decision

The Vereinszeitung plugin lives in `plugins/Vereinszeitung/` and consists of four `.cgcs` source files plus `plugin.json`. It declares permissions `["vereinszeitung.read", "vereinszeitung.manage"]` and extension points `["dashboard.widget", "page.generic", "nav.main"]`. Two migrations create `plg_vereinszeitung_ausgaben` and `plg_vereinszeitung_artikel`. Three contributions are registered: `VereinszeItungWidgetProvider` (for `dashboard.widget`), `VereinszeItungPageProvider` (for `page.generic`), and a nav entry pointing to `/plugin/clubgear.plugin.vereinszeitung/ausgaben` with `RequiredPermission = "vereinszeitung.manage"`.

### Rationale

`IPluginPageProvider` is the correct admin-management pattern for Ausgaben CRUD (established in plugin-generic-pages). `IDashboardWidgetProvider` delivers the self-service Ausgaben view to the home dashboard. Nav entry follows the Inventar pattern (`sink.AddNavEntries`) for the admin link. Two separate migrations are used because `plg_vereinszeitung_artikel` has a foreign key to `plg_vereinszeitung_ausgaben`; the parent table must exist before the child table is created. `vereinszeitung.read` gates the widget (members need it to see published issues); `vereinszeitung.manage` gates the page provider and nav entry (admins need it for CRUD).

---

## 2026-06-16 - PluginDashboardWidgetService Permission Guard: bridge.HasPermissionAsync per Slot

### Decision

For each `DashboardWidgetSlot` returned by a provider, if `slot.PermissionKey` is non-null, `PluginDashboardWidgetService` calls `bridge.HasPermissionAsync(slot.PermissionKey, ct)`. Slots that fail the check are excluded from the result. Slots with a null `PermissionKey` are included unconditionally. `CreateBridge` is called once per runtime (reused across all slots from that runtime's providers).

### Rationale

Dashboard widgets may carry different permission requirements than the page-level check. A provider might return multiple slots with distinct keys. Per-slot checking is the only way to handle this correctly. Reusing the bridge per runtime avoids creating multiple bridge instances for the same module within a single request. This mirrors the slot-level permission pattern used for member actions in `MemberPluginSlotService`.

---

## 2026-06-16 - Architecture Tests: PluginBoundaryTests Extended, HomeDashboardRenderTests Added

### Decision

`PluginBoundaryTests.ContractsAssembly_ShouldExpose_HostContext_AndMemberReadModels` gains two new assertions: `Assert.Same(typeof(IPluginModule).Assembly, typeof(IDashboardWidgetProvider).Assembly)` and `Assert.Same(typeof(IPluginModule).Assembly, typeof(DashboardWidgetSlot).Assembly)`. A new file `HomeDashboardRenderTests.cs` is added with tests verifying that `Views/Home/Index.cshtml` contains `id="dashboardWidgets"` and `Model.Widgets`. A new `VereinszeItungPluginSliceTests.cs` covers manifest, contribution registration, widget and CRUD round-trips, and disk manifest validity.

### Rationale

Boundary tests enforce that widget contract types do not escape the contracts assembly — a mandatory guard under ADR 0001. `HomeDashboardRenderTests` follows the `MemberIndexSectionsRenderTests` pattern for view-structure regression tests. Vereinszeitung slice tests follow `InventarPluginSliceTests` in structure and use an in-memory SQLite fixture, which is the established pattern for plugin data-layer tests.
