# ADR: Notification Channel Provider

## Purpose

Persistent architectural decisions for the notification-channel-provider iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-16 - INotificationChannelProvider Placed in Contracts Assembly with Contracts-Only Types

### Decision

A new interface `INotificationChannelProvider` is added to `Contracts/Plugin/INotificationChannelProvider.cs`. Its properties and method use only Contracts-assembly types: `PluginNotificationMessage` (mirrors `NotificationMessage` from `Services.Abstractions` but defined in Contracts) and `PluginNotificationResult` (mirrors `NotificationResult`). The interface is not `INotificationChannel` re-exported — it is a separate Contracts-level type.

### Rationale

`INotificationChannel` lives in `ClubGear.Services.Abstractions`, a forbidden namespace for plugin code per `PluginRuntimeAdapter.EnsureIsolated` and `PluginBoundaryTests`. Plugins must only reference `ClubGear.Plugin.Contracts`. Introducing a parallel `INotificationChannelProvider` in Contracts preserves the boundary. The host maps between the two representations in `NotificationService`. This mirrors the existing pattern where `PluginRuntimeNotification` / `PluginRuntimeNotificationResult` in Contracts mirror `NotificationMessage` / `NotificationResult` in Services.Abstractions.

---

## 2026-06-16 - Dynamic Plugin Channel Lookup via IPluginNotificationChannelService (Re-collect-at-call-time)

### Decision

A new Scoped service `IPluginNotificationChannelService` / `PluginNotificationChannelService` is introduced. It reads `ChannelProviders` from `RegisteredPluginRuntime` (pre-loaded at plugin activation time) and instantiates the matching provider via `IPluginRegistryReader.CreateMemberProvider<INotificationChannelProvider>` at send-time. `NotificationService` injects this service and falls back to it when the core `_channels` dictionary does not contain the requested channel name. No singleton registry or DI container rebuild is needed.

### Rationale

`NotificationService` builds `_channels` once at Scoped construction from DI-enumerated `IEnumerable<INotificationChannel>`. Plugins cannot be inserted into this dictionary post-startup without rebuilding the DI container. Two candidate patterns were considered: (a) a singleton `IPluginNotificationChannelRegistry` that `NotificationService` queries alongside the static dict, or (b) re-collect-at-call-time reading from `RegisteredPluginRuntime.ChannelProviders`. Pattern (b) is chosen because it mirrors the established `PluginDashboardWidgetService` and `PluginPageService` patterns in this codebase, avoids introducing a new singleton with mutable state, and is consistent with how all other plugin contribution types (page providers, widget providers) are resolved. The performance cost of `CreateMemberProvider` per send is accepted because notification sends are infrequent and not on hot paths.

---

## 2026-06-16 - PluginNotificationChannelContribution as 10th Positional Parameter of RegisteredPluginRuntime

### Decision

`RegisteredPluginRuntime` gains a 10th positional parameter `IReadOnlyList<PluginNotificationChannelContribution> ChannelProviders`. `PluginNotificationChannelContribution(string ProviderType, int Order = 0)` is added to `PluginContributions.cs`. `IPluginContributionSink` gains `void AddNotificationChannelProvider(PluginNotificationChannelContribution contribution) { }` with a default no-op body. `PluginLoader.PluginContributionCollector` adds a backing list and overrides the method. All 9 `RegisteredPluginRuntime` construction sites are updated in the same commit (1 production site in `PluginLoader.cs`, 8 test sites).

### Rationale

Channel contributions are structural metadata declared at load time via `RegisterContributions`, analogous to `NavEntries` (9th positional parameter). Storing them in the runtime record avoids re-running `RegisterContributions` on every notification send. The default no-op body on `IPluginContributionSink` means existing compiled plugins dispatch to the default without recompilation. The blast radius of 9 construction sites is consistent with prior iterations (`NavEntries` addition touched a similar set of sites).

---

## 2026-06-16 - NotificationService: Core Channels Take Priority; Plugin Channel as Fallback

### Decision

`NotificationService.NotifyAsync` first checks the static `_channels` dictionary (core DI-registered channels). Only on a miss does it call `_pluginChannelService.FindChannel(message.Channel)`. If the plugin channel is found, its `SendAsync` is called with type mapping and wrapped in a `try/catch` for error isolation. The failure is written to `NotificationRecord` with `Status=Failed` and does not propagate to the caller. If no channel is found at all (core or plugin), the existing "Channel nicht registriert" failure path is taken.

### Rationale

Core channels (SMTP, InApp) are registered via DI and form the primary set. Plugin channels extend this set dynamically. Priority ordering (core first) prevents a plugin from shadowing a core channel by registering the same `ChannelName`. Error isolation per-channel is a confirmed requirement: a plugin channel failure must not block delivery via other channels or throw to the caller. The `try/catch` in `NotificationService` (not in the plugin) is the correct location because the plugin cannot be trusted to catch its own exceptions.

---

## 2026-06-16 - Tracing Convention: Channel Field as "plugin:{moduleId}" Prefix in NotificationRecord

### Decision

Plugin channel providers are expected to set their `ChannelName` to a value prefixed with `plugin:` (e.g., `"plugin:matrix"`, `"plugin:webhook"`). This convention is documented in the contract but not enforced at runtime. The `NotificationRecord.Channel` field stores whatever `ChannelName` the provider returns, giving administrators a filtering anchor.

### Rationale

`NotificationRecord` has no `ModuleId` column and adding one would require a schema migration, which was ruled out. The `Channel` field is the only per-row identifier that can encode plugin origin. Using a `plugin:` prefix convention (without schema enforcement) follows the same pattern as `loadContext.Name = $"plugin:{record.Key}"` already used in `PluginLoader`. A query `WHERE Channel LIKE 'plugin:%'` gives admins a straightforward filter for plugin-originated notifications.

---

## 2026-06-16 - ContractVersion Bumped to 1.5.0

### Decision

`ContractVersion.Current` is bumped from `1.4.0` to `1.5.0`. `MinimumSupported` remains `1.0.0`.

### Rationale

`INotificationChannelProvider` is a new capability surface in the Contracts assembly. Plugins wishing to provide a notification channel must declare `RequiredCoreVersion: ">=1.5.0"` in their manifest to signal compatibility. The `ContractCompatibilityService` will allow all plugins with `RequiredContractVersion` between `1.0.0` and `1.5.0` inclusive. Per user approval in ADR 0001 GR-002.

---

## 2026-06-16 - notification.channel Extension Point Added to PluginExtensionPoints

### Decision

`PluginExtensionPoints.NotificationChannel = "notification.channel"` is added as a `public const string` field, and `"notification.channel"` is added to `KnownValuesInternal`.

### Rationale

`PluginManifestParser.ValidateExtensionPoints` rejects unknown extension point values. Without this entry, any plugin declaring `"notification.channel"` is rejected at install time. This is the identical pattern used for all prior extension points (`nav.main`, `page.generic`, etc.).

---

## 2026-06-16 - PluginAdminStatusViewModel Gains 26th Positional Parameter ChannelProviderCount

### Decision

`PluginAdminStatusViewModel` gains a 26th positional parameter `int ChannelProviderCount` after `NavEntryCount`. The construction site in `PluginAdminQueryService.CreateStatus` passes `runtime?.ChannelProviders.Count ?? 0`. All test construction sites pass `0`.

### Rationale

All existing runtime contribution counts (Routes, Services, MemberProviders, BackgroundJobs, NavEntries) are surfaced in the admin status view model. `ChannelProviderCount` follows the same observability pattern so administrators can see at a glance how many notification channels a plugin contributes.
