# ADR: Audit Sink Plugins

## Purpose

Persistent architectural decisions for the audit-sink-plugins iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-17 - IAuditSinkProvider Placed in Contracts Assembly with Structural-Fields-Only Event Model

### Decision

A new interface `IAuditSinkProvider` is added to `Contracts/Plugin/IAuditSinkProvider.cs`. Its single method `OnAuditEventAsync(PluginAuditEvent auditEvent, CancellationToken cancellationToken)` accepts a Contracts-only value type `PluginAuditEvent`. The event exposes six structural fields only: `Action`, `Actor`, `Source`, `TargetType`, `TargetId`, `OccurredAtUtc`. The fields `BeforeJson`, `AfterJson`, and `MetadataJson` are explicitly excluded.

### Rationale

`IAuditSinkProvider` must live in `ClubGear.Plugin.Contracts` because plugin code may not reference `ClubGear.Services`, `ClubGear.Data`, `ClubGear.Controllers`, or `ClubGear.Models` (enforced by `PluginRuntimeAdapter.ForbiddenNamespaces` and `PluginBoundaryTests`). The structural-fields-only payload was selected by user decision (2026-06-17): serialized payload fields (`BeforeJson`/`AfterJson`/`MetadataJson`) may contain PII and are withheld as the safest default; they can be relaxed in a later iteration.

---

## 2026-06-17 - Decorator Pattern for Sink Dispatch: AuditSinkDispatchDecorator Wraps DatabaseAuditLogService

### Decision

Sink dispatch is implemented as a decorator `AuditSinkDispatchDecorator` that wraps the inner `IAuditLogService` (bound to `DatabaseAuditLogService` in DI). The decorator calls the inner `LogAsync` first; after the inner call returns, the decorator builds a `PluginAuditEvent` and calls `IPluginAuditSinkService.DispatchAsync` in a separate, independent `try/catch`. The DI registration in `ServiceCollectionExtensions` replaces the direct `AddScoped<IAuditLogService, DatabaseAuditLogService>()` with a factory that wraps the concrete service.

### Rationale

Two candidate placements were evaluated: (a) appending sink dispatch inside `DatabaseAuditLogService.LogAsync` after `SaveChangesAsync`, and (b) a decorator wrapping `IAuditLogService`. The decorator is the lower-risk approach because it does not touch the existing swallow-boundary in `DatabaseAuditLogService`. The existing method has a single `try/catch` that encompasses the entire body including `SaveChangesAsync`; appending sink dispatch inside that block would conflate sink errors with DB write errors in log output. The decorator keeps the two concerns entirely independent: DB commit swallow semantics are unchanged; sink dispatch failure is caught and logged in the decorator's own guard. This is the pattern with the smallest blast radius on `DatabaseAuditLogService` itself.

---

## 2026-06-17 - PluginAuditSinkContribution as 11th Positional Parameter of RegisteredPluginRuntime

### Decision

`RegisteredPluginRuntime` gains an 11th positional parameter `IReadOnlyList<PluginAuditSinkContribution> AuditSinks`, appended after the 10th parameter `IReadOnlyList<PluginNotificationChannelContribution> ChannelProviders` (added by the notification-channel-provider iteration). `PluginAuditSinkContribution(string ProviderType, int Order = 0)` is added to `PluginContributions.cs`. `IPluginContributionSink` gains `void AddAuditSink(PluginAuditSinkContribution contribution) { }` with a default no-op body. `PluginLoader.PluginContributionCollector` adds a backing list and overrides the method. All 9 `RegisteredPluginRuntime` construction sites receive the new trailing argument.

### Rationale

Audit sink contributions are structural metadata declared at load time via `RegisterContributions`, identical to `NavEntries`, `ChannelProviders`, and all prior contribution types. Storing them in the runtime record avoids re-running `RegisterContributions` on every audit event. The default no-op body on `IPluginContributionSink` means existing compiled plugins are unaffected without recompilation. The blast radius of 9 sites (1 production, 8 test) is consistent with prior iterations.

---

## 2026-06-17 - IPluginAuditSinkService as Scoped Re-collect-at-call-time Service

### Decision

A new scoped service `IPluginAuditSinkService` / `PluginAuditSinkService` is introduced under `Services/Plugins/AuditSink/`. It reads `AuditSinks` from all registered runtimes via `IPluginRegistryReader.GetRegisteredPlugins()` at dispatch time and instantiates each `IAuditSinkProvider` via `IPluginRegistryReader.CreateMemberProvider<IAuditSinkProvider>`. Each sink invocation is wrapped in a per-sink `try/catch`; a failing sink is logged and skipped; remaining sinks in the loop continue.

### Rationale

This mirrors the established `PluginPageService`, `PluginNavEntryService`, and (per ADR) `PluginNotificationChannelService` patterns: re-collect-at-call-time from `RegisteredPluginRuntime` at dispatch, no singleton registry with mutable state, no DI container rebuild. Per-sink isolation (individual `try/catch` per sink, not a single block around the entire loop) ensures one misbehaving plugin does not suppress dispatch to all remaining sinks.

---

## 2026-06-17 - audit.sink Extension Point Added to PluginExtensionPoints

### Decision

`PluginExtensionPoints.AuditSink = "audit.sink"` is added as a `public const string` field and `"audit.sink"` is added to `KnownValuesInternal`.

### Rationale

`PluginManifestParser.ValidateExtensionPoints` is the single rejection point for unknown extension point values. Without this entry, any plugin declaring `"audit.sink"` is rejected at install time. The value `"audit.sink"` follows the established `<domain>.<role>` convention (matching `"notification.channel"`, `"nav.main"`, `"page.generic"`, etc.).

---

## 2026-06-17 - ContractVersion Bumped to 1.6.0

### Decision

`ContractVersion.Current` is bumped from `1.5.0` to `1.6.0`. `MinimumSupported` remains `1.0.0`. This iteration lands after the notification-channel-provider iteration (which bumped from `1.4.0` to `1.5.0`).

### Rationale

`IAuditSinkProvider` is a new capability surface in the Contracts assembly. Plugins wishing to provide an audit sink must declare `RequiredCoreVersion: ">=1.6.0"` in their manifest to signal compatibility. The `ContractCompatibilityService` allows all plugins with `RequiredContractVersion` between `1.0.0` and `1.6.0` inclusive. Per user approval gate ADR 0001 GR-002.

---

## 2026-06-17 - PluginAdminStatusViewModel Gains 27th Positional Parameter AuditSinkCount

### Decision

`PluginAdminStatusViewModel` gains a 27th positional parameter `int AuditSinkCount` after `ChannelProviderCount` (26th, added by notification-channel iteration). The construction site in `PluginAdminQueryService.CreateStatus` passes `runtime?.AuditSinks.Count ?? 0`. All test construction sites pass `0`.

### Rationale

All runtime contribution counts are surfaced in the admin status view model for operator observability. `AuditSinkCount` follows the same pattern as `RouteCount`, `NavEntryCount`, `ChannelProviderCount`, and all prior counters.

---

## 2026-06-17 - Pre-Implementation User Approval Gate (ADR 0001 GR-002)

### Decision

Before the Builder begins any file changes, the user must explicitly confirm approval for the core changes described in this design. The Builder's Phase I plan must start with this gate and halt until approval is received. Core changes covered by the gate: decorator DI registration, `RegisteredPluginRuntime` 11th parameter addition, `ContractVersion` bump to `1.6.0`.

### Rationale

`01_task.md` classifies this command as `generic-core` / `mixed` for `technical` plugins and explicitly states: "Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich." This gate must not be bypassed.
