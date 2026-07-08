# ADR: Background Job Runner

## Purpose

Persistent architectural decisions for the background-job-runner iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-17 - IPluginBackgroundJob Interface Placed in Contracts Assembly with IPluginHostContext Parameter

### Decision

A new interface `IPluginBackgroundJob` is added to `Contracts/Plugin/IPluginBackgroundJob.cs` with a single method `ExecuteAsync(IPluginHostContext hostContext, CancellationToken cancellationToken)`. The parameter type is `IPluginHostContext`, not `IPluginRuntimeBridge`.

### Rationale

All existing plugin-side contributor interfaces that need runtime services receive `IPluginHostContext` directly (`IAdminFunctionPanelProvider`, `IMemberDetailCardProvider`, `IMemberEditTabProvider`, etc.). `IPluginRuntimeBridge` is a host-side construct passed only within `IPluginRuntimeAdapter.RunAsync` / `InvokeAsync` delegates. Background jobs run without a user session, making the permission-enforcement layer in `IPluginRuntimeBridge.HasPermissionAsync` inappropriate. `IPluginHostContext` provides member access and persistence without exposing permission checks or audit delegation. The interface must live in `ClubGear.Plugin.Contracts` because plugin assemblies may not reference `ClubGear.Services`, `ClubGear.Data`, `ClubGear.Controllers`, or `ClubGear.Models`.

---

## 2026-06-17 - PluginBackgroundJobRunner is a Singleton IHostedService-equivalent Using IServiceScopeFactory

### Decision

A new singleton `PluginBackgroundJobRunner` (implementing `IPluginBackgroundJobRunner`) is registered via `services.AddSingleton<IPluginBackgroundJobRunner, PluginBackgroundJobRunner>()`. It is NOT registered as an `IHostedService`. It is started by `PluginLifecycleService.LoadIntoRuntimeAsync` after each successful plugin registration, and stopped by `PluginLifecycleService.DeactivateAsync` before `PluginRegistry.Unregister` is called. It uses `IServiceScopeFactory` to open a new DI scope per job execution to access scoped services such as `ApplicationDbContext`.

### Rationale

No `AddHostedService` call exists anywhere in the codebase. Putting job management inside `PluginRegistry` would add scoped service coupling to a singleton. Starting jobs inside `PluginLifecycleService` (scoped) would require holding long-lived `Task` references in a scoped object. The singleton pattern using `IServiceScopeFactory` is already established by `PluginEndpointRegistrar`. A manual singleton runner that is driven by lifecycle events is simpler than a hosted service because it maps directly to the `LoadIntoRuntimeAsync` / `DeactivateAsync` call sites and avoids any startup-ordering dependency on `IHostedService.StartAsync`.

---

## 2026-06-17 - Cancellation and Drain Sequence Before AssemblyLoadContext.Unload

### Decision

`PluginLifecycleService.DeactivateAsync` calls `await _jobRunner.StopJobsForModuleAsync(moduleId)` before calling `_runtimeRegistry.Unregister(moduleId)`. `StopJobsForModuleAsync` signals the per-module `CancellationTokenSource`, awaits all running job tasks for that module with a 5-second timeout, then removes state entries. Only after this drain does `PluginRegistry.Unregister` call `loadContext.Unload()`.

### Rationale

`PluginRegistry.Unregister` currently calls `loadContext.Unload()` synchronously with no drain. A job delegate that is still executing at the point of unload would reference types in an unloaded `AssemblyLoadContext`, causing `TypeLoadException` or `AccessViolationException`. The drain must live in `StopJobsForModuleAsync` (on the runner) rather than directly in `DeactivateAsync` to keep lifecycle orchestration clean and testable independently.

---

## 2026-06-17 - Job State Stored In-Memory on the Runner Singleton

### Decision

Live job state (`State`, `LastRunUtc`, `LastError`) is stored in a `ConcurrentDictionary<string, PluginJobEntry>` on `PluginBackgroundJobRunner`. The Admin UI `Detail.cshtml` injects `IPluginBackgroundJobRunner` and reads `GetJobStatuses(moduleId)` at render time. `PluginAdminStatusViewModel` receives one new int parameter `BackgroundJobRunningCount` (count only). No new database table is introduced.

### Rationale

The architecture for `NavEntries` and `AuditSinks` is the established precedent: counts in `PluginAdminStatusViewModel`, per-item detail fetched at render time via an injected singleton reader. In-memory state is lost on process restart, which is accepted: background jobs re-register and start fresh on the next activation. A new `PluginJobExecutionRecord` DB table would require a migration and add complexity disproportionate to the informational value of persisted last-run timestamps for this iteration.

---

## 2026-06-17 - System Identity for Background Job Execution

### Decision

`PluginBackgroundJobRunner` constructs a system `ClaimsPrincipal` as `new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "system.plugin-job-runner")], "System"))` for passing to `IPluginRuntimeAdapter.CreateBridge`. Job classes receive `IPluginHostContext` directly and do not call `HasPermissionAsync`; the system identity is used only to satisfy the non-null requirement of `CreateBridge`.

### Rationale

No system-identity helper or factory exists in production code. The empty-identity pattern `new ClaimsPrincipal(new ClaimsIdentity())` is used in tests but would fail any authentication check. A named system identity with an explicit authentication type ("System") is a minimal, self-documenting pattern that satisfies the `ArgumentNullException.ThrowIfNull(user)` guard in `PluginRuntimeAdapter` without granting any permissions. Because background jobs receive `IPluginHostContext` (not `IPluginRuntimeBridge`), they bypass the permission-check layer entirely, making the exact claim set irrelevant to security.

---

## 2026-06-17 - Test Fixture Pattern: Extend AllowedPluginRuntimeFixtures with RuntimeLoadedBackgroundJobA

### Decision

A concrete `RuntimeLoadedBackgroundJobA : IPluginBackgroundJob` type is added to the existing `tests/ClubGear.ArchitectureTests/Fixtures/AllowedPluginRuntimeFixtures.cs`. The new `PluginBackgroundJobRunnerTests` test class is a separate file in `tests/ClubGear.ArchitectureTests/`. No new fixture file is created; the existing `RuntimeLoadedPluginModuleA` already contributes the `members.sync` job, and the new concrete class can be referenced from the same assembly.

### Rationale

Isolated fixture files (`ForbiddenServicePluginFixture`, `ForbiddenControllerPluginFixture`, `ForbiddenDbContextPluginFixture`) exist specifically for boundary-violation cases that must not pollute the allowed-plugin assembly. `RuntimeLoadedBackgroundJobA` is an allowed, conforming job implementation. The `PluginTestPackageBuilder` packages the `RuntimeLoadedPluginModuleA` assembly, which includes all types in `AllowedPluginRuntimeFixtures.cs`, so the job type is automatically available in loaded test plugins without changing the packaging logic.
