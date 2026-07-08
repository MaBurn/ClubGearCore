# ADR: Plugin Generic Pages

## Purpose

Persistent architectural decisions for the plugin-generic-pages iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-14 - IPluginPageProvider as the Contract Interface for Generic List/Detail Pages

### Decision

A new interface `IPluginPageProvider` is added to `Contracts/Plugin/IPluginPageProvider.cs`. It defines three methods: `GetPageDefinitionAsync` (returns `PluginPageDefinition` describing columns, commands, and permissions), `GetRowsAsync` (returns `IReadOnlyList<PluginDataRow>` with optional `filterValue` and `entityKey` parameters), and `ExecuteCommandAsync` (accepts `commandKey`, optional `entityKey`, and a string dictionary of arguments; returns `PluginCommandResult`). The interface does not extend any base interface; `IPluginProvider` does not exist in the codebase.

### Rationale

The existing `IAdminFunctionPanelProvider` pattern proves that a single provider interface with a definition-query method and a command-execution method is sufficient for schema-driven mutation flows. Reusing `PluginCommandResult` and `PluginFieldError` from `IAdminFunctionPanelProvider.cs` avoids a parallel result type. Passing `entityKey` as a nullable string to both `GetRowsAsync` and `ExecuteCommandAsync` covers both list mode (null entity key) and detail/row-action mode (non-null entity key) without adding a separate interface or overload.

---

## 2026-06-14 - PluginPageDefinition Carries ListPermission and FilterPlaceholder

### Decision

`PluginPageDefinition` is a `sealed record` with positional parameters: `PageKey`, `Title`, `EntityKeyColumn`, `IReadOnlyList<PluginPageColumn> Columns`, `IReadOnlyList<PluginPageCommand> Commands`, `string? ListPermission`, and `string? FilterPlaceholder`. A null `ListPermission` means any authenticated user can access the list. A null `FilterPlaceholder` means no filter input is rendered.

### Rationale

Embedding `ListPermission` in the definition record (rather than requiring the provider to enforce access itself) keeps permission enforcement in the host service, consistent with how `PluginAdminPanel.PermissionKey` is checked by `PluginAdminCommandService.GetPanelsAsync` before the provider's output is returned to the caller. `FilterPlaceholder` being nullable lets the provider opt out of filtering without requiring the host to implement a separate "does this page support filtering?" query.

---

## 2026-06-14 - PluginPageCommand Carries RequiredPermission and RequiresEntityKey

### Decision

`PluginPageCommand` is a `sealed record` with `Key`, `Label`, `Icon?`, `RequiredPermission?`, `IReadOnlyList<PluginFieldSchema>? ArgumentSchema`, and `bool RequiresEntityKey`. `RequiresEntityKey = false` marks list-level commands (e.g. Create); `true` marks row-level commands (e.g. Edit, Delete). `ArgumentSchema` reuses the existing `PluginFieldSchema` type from `PluginSchemaContracts.cs`.

### Rationale

The two-level permission pattern (page-level `ListPermission` + per-command `RequiredPermission`) mirrors the `PluginAdminPanel.PermissionKey` + `PluginAdminCommandDescriptor.PermissionKey` pattern confirmed in the research. `RequiresEntityKey` as a boolean flag on the command record lets the view render list-level buttons once at the top and row-level buttons once per row without additional host logic. Reusing `PluginFieldSchema` avoids a new schema type and ensures the existing modal JS (`renderField`, `applyFieldErrors`) works without modification.

---

## 2026-06-14 - Extension Point Constant PageGeneric = "page.generic"

### Decision

`PluginExtensionPoints.PageGeneric = "page.generic"` is added as a new `public const string` field in `Contracts/Plugin/PluginExtensionPoints.cs`, and `"page.generic"` is added to `KnownValuesInternal`. `ContractVersion.Current` is not bumped.

### Rationale

Adding a constant to `KnownValuesInternal` is required for `PluginManifestParser.ValidateExtensionPoints` to accept manifests that declare this extension point. Without it, any plugin declaring `"page.generic"` would be rejected at install time. The nav-contributions ADR (2026-06-13) established that additive default-body methods and new extension-point constants do not require a `ContractVersion` bump. This iteration follows the same precedent.

---

## 2026-06-14 - AddPageProvider as a Default No-op Method on IPluginContributionSink

### Decision

`IPluginContributionSink` gains one new default no-op method: `void AddPageProvider(PluginPageProviderContribution contribution) { }`. `PluginPageProviderContribution` is a new `sealed record(string ProviderType, int Order = 0)` added to `PluginContributions.cs`.

### Rationale

This follows the identical pattern used for `AddAdminPanelProvider` (confirmed as implemented in production). The default no-op body means existing `IPluginContributionSink` implementations (including `PluginLoader.PluginContributionCollector`) compile without changes. Page provider contributions are re-collected at query time by `PluginPageService` via a local `ContributionSink` that overrides the default body, exactly as `PluginAdminCommandService` does for admin panel providers. `RegisteredPluginRuntime` is not extended; all 6 existing construction sites remain positional and unchanged.

---

## 2026-06-14 - PluginPageService Mirrors PluginAdminCommandService Pattern

### Decision

`PluginPageService` (in `Services/Core/`) implements `IPluginPageService` (in `Services/Abstractions/`). It injects `IPluginRegistryReader`, `IPluginRuntimeAdapter`, and `ILogger<PluginPageService>`. On each call it creates a local `ContributionSink`, runs `module.RegisterContributions(sink)`, then iterates contributions to find the matching `IPluginPageProvider`. No caching of contributions or definitions is applied. Registered via `services.AddScoped<IPluginPageService, PluginPageService>()` in `ServiceCollectionExtensions.cs`.

### Rationale

The re-collect-at-call-time pattern is the established norm for provider-type contributions. Caching page definitions would require invalidation logic tied to plugin lifecycle events, adding complexity not justified by current load requirements. The scoped lifetime aligns with all other service registrations in `AddClubGearCoreServices`.

---

## 2026-06-14 - PluginPageResult<T> as the Service Return Envelope

### Decision

A generic `sealed record PluginPageResult<T>(T? Value, bool NotFound, bool Forbidden, string? ErrorMessage)` is introduced in `Services/Abstractions/IPluginPageService.cs`, with static factory helpers `PluginPageResult.Ok<T>`, `PluginPageResult.NotFound<T>`, and `PluginPageResult.Forbidden<T>`. `IPluginPageService` methods for `GetPageDefinitionAsync` and `GetRowsAsync` return `Task<PluginPageResult<...>>`. `ExecuteCommandAsync` returns `Task<PluginCommandResult>` directly (reusing the existing type).

### Rationale

The controller needs to distinguish "module not found" (404), "permission denied" (403), and "success" (200) without catching exceptions. Wrapping results in a discriminated record avoids exception-based control flow in the controller while keeping the service API strongly typed. `PluginCommandResult` already carries a discriminating `Status` string, so it serves as its own envelope for command execution and does not need wrapping.

---

## 2026-06-14 - Separate Razor Controller and API Controller for Plugin Pages

### Decision

Two files are created under `Controllers/PluginPage/`:
- `PluginPage_Controller.cs` — MVC `Controller` subclass with `[Authorize]`, no class-level permission attribute; routes `GET /plugin/{moduleId}/{pageKey}` (Index) and `GET /plugin/{moduleId}/{pageKey}/{entityKey}` (Detail).
- `PluginPage_API.cs` — `ControllerBase` subclass with `[ApiController]`, `[Authorize]`, no class-level permission attribute; routes `POST /api/plugin/page-commands` (ExecuteCommand).

Per-page and per-command permissions are enforced inside `PluginPageService`, not via `[PermissionAuthorize]` on the controller class.

### Rationale

The existing codebase splits Razor view controllers and JSON API controllers into separate files for each feature area (e.g., `Member_Controller.cs` / `Member_API.cs`). This design follows that convention. Placing no `[PermissionAuthorize]` at the class level is intentional: page access is controlled by the plugin-declared `ListPermission` (which may be null, meaning any authenticated user), not a static host permission key. Enforcing this in the service keeps the controller thin and ensures the permission check applies consistently regardless of how the service is called.

---

## 2026-06-14 - _PluginPageActionModal.cshtml as a Parallel Partial (No Modification to Existing Modal)

### Decision

A new partial view `Views/PluginPage/_PluginPageActionModal.cshtml` is created. It is untyped (no `@model` directive). Its JavaScript listens for `data-plugin-page-action` click events and posts to `/api/plugin/page-commands` with payload `{ moduleId, pageKey, commandKey, entityKey?, arguments: {} }`. The field-rendering logic (`renderField`, `applyFieldErrors`, `clearModalErrors`) is reimplemented inline (or extracted to a shared JS function via a new `<script>` partial if the Builder judges duplication excessive). The existing `Views/Members/_PluginActionModal.cshtml` is not modified.

### Rationale

`_PluginActionModal.cshtml` is typed `@model MemberPluginSlotSnapshot` and its payload always includes `memberId`. Making it generic would require changing its model binding, the payload shape, and the guard flag name (`clubGearMemberPluginActionsBound`), all of which risk breaking the existing member-context flows. A parallel partial eliminates coupling. The field-rendering JavaScript is already ~100 lines and is logically independent of the endpoint or payload shape; it can be safely duplicated or extracted without cross-contamination.

---

## 2026-06-14 - Column Rendering as Plain Text String Lookup

### Decision

In `Views/PluginPage/Index.cshtml`, each table cell is rendered as `@(row.Values.GetValueOrDefault(col.Key) ?? string.Empty)`, plain text with no HTML encoding risk beyond Razor's default escaping. No typed formatting, no hyperlink generation for individual cells.

### Rationale

`PluginDataRow.Values` is a `Dictionary<string, string?>` whose values are all `Convert.ToString(...)` outputs from SQLite — invariant-culture strings or null. There is no richer type information available. The user confirmed option A1: simple generic table, column key looks up string value, everything plain text. Razor auto-escapes the string, so XSS is not a concern.

---

## 2026-06-14 - Detail View Reuses GetRowsAsync Filtered by EntityKey

### Decision

`IPluginPageProvider.GetRowsAsync` with a non-null `entityKey` is the mechanism for loading the single row shown on the detail page. No new contract method (`GetRowAsync`, `GetDetailAsync`) is introduced. The host takes `rows[0]` and renders it as a `<dl>` key-value card.

### Rationale

The user confirmed option B1. Adding a separate `GetRowAsync` method would duplicate intent and force every provider implementation to implement an additional interface member even when `GetRowsAsync` with `entityKey` is sufficient. The `IReadOnlyList<PluginDataRow>` return type already handles the "not found" case naturally: an empty list means the controller returns 404.
