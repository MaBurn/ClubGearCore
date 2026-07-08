# ADR: Finance Bugfix Typeload

## Purpose

Persistent architectural decisions for the finance-bugfix-typeload iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-25 - Root Cause: Deployment Gap, Not Code Bug

### Decision

The `TypeLoadException` on `FinanceSelfServiceSectionProvider` is caused by a deployment gap: `ISelfServiceProfileSectionProvider` was added to `Contracts/Plugin/` in iteration 5, but the host container image was not rebuilt after that change. The plugin ZIP contains only `Finance.Plugin.dll` and `plugin.json`; `PluginAssemblyLoadContext.Load` unconditionally delegates the contracts assembly to `AssemblyLoadContext.Default`, so the plugin always sees the host-loaded binary. The CLR assembly version is always `1.0.0.0` (no `<Version>` property in the csproj), so the CLR cannot detect the stale binary at load time. The JIT throws when materializing the vtable for `FinanceSelfServiceSectionProvider` inside `PluginRegistry.CreateMemberProvider<ISelfServiceProfileSectionProvider>` at `Activator.CreateInstance(providerClrType)`. The `SelfServiceSectionService` catch block swallows the exception as a `LogWarning`.

The primary fix is a container rebuild and redeploy. No code change in `PluginLoader`, `PluginRegistry`, or `SelfServiceSectionService` is required.

### Rationale

The architecture test suite and source tree are in the post-iteration-5 state; all required types exist. The problem is exclusively a runtime deployment state mismatch. Treating this as a code bug requiring defensive code in `CreateMemberProvider` would add complexity without addressing the underlying cause, and would mask future deployment gaps of the same class.

---

## 2026-06-25 - Install-Time Guard: Raise MinimumSupported to 1.8.0 and Finance requiredCoreVersion to >=1.8.0

### Decision

`ContractVersion.MinimumSupported` is raised from `1.0.0` to `1.8.0` in `Contracts/Plugin/ContractVersion.cs`. Finance's `"requiredCoreVersion"` in `plugins/Finance/plugin.json` is updated from `">=1.0.0"` to `">=1.8.0"`. No change is made to `ContractCompatibilityService.Validate` or `PluginManifestParser`.

After this change, installing Finance 1.5.0 on a stale host (where `ContractVersion.Current < 1.8.0`) will be rejected at install time with `"Plugin-Vertrag ist nicht kompatibel."` rather than silently degrading at runtime. All currently-installed plugins that declare `">=1.0.0"` continue to pass validation on the current (iteration-5+) host because `Validate` compares the plugin's declared `RequiredContractVersion` against `MinimumSupported`, and plugins pass `Version(1,0,0)` which is below the new minimum — meaning they will also be rejected on a stale host. This is the desired outcome: any plugin compiled against the iteration-5 contracts surface should not be installable on a host that predates iteration 5.

The existing `ContractCompatibilityServiceTests` "below minimum" test uses `new Version(MinimumSupported.Major, MinimumSupported.Minor)` which resolves to `Version(1, 8)`. In .NET, `Version(1, 8)` is less than `Version(1, 8, 0)` because `Build` is `-1` vs `0`. The test continues to pass without modification.

### Rationale

The `requiredCoreVersion` mechanism exists precisely to express a minimum host contract version a plugin requires. Finance 1.5.0 requires `ISelfServiceProfileSectionProvider`, which was introduced in `ContractVersion.Current = 1.8.0`. Declaring `>=1.8.0` is the semantically correct declaration. Raising `MinimumSupported` to `1.8.0` ensures the rejection is both at the contract level (individual plugin declares `>=1.8.0`) and at the floor level (the host rejects any plugin that would require a contracts surface older than the current minimum). This is a one-time ratchet: `MinimumSupported` only moves forward.

---

## 2026-06-25 - Regression Test: CreateMemberProvider Round-Trip for ISelfServiceProfileSectionProvider

### Decision

A new test file `FinanceBugfixTypeloadTests.cs` is added to `tests/ClubGear.ArchitectureTests/`. It contains a single xUnit Fact that:

1. Builds a test plugin package using `PluginTestPackageBuilder` with a new fixture module (`FixtureSelfServiceProviderModule`) that calls `sink.AddSelfServiceProfileSection(new PluginSelfServiceProfileProviderContribution(typeof(FixtureSelfServiceProvider).FullName!, 0))`.
2. Loads the plugin via `PluginLoader.LoadAsync`.
3. Registers it in a `PluginRegistry`.
4. Calls `registry.CreateMemberProvider<ISelfServiceProfileSectionProvider>(moduleId, providerTypeName)`.
5. Asserts the returned value is not null.

The fixture types (`FixtureSelfServiceProviderModule` and `FixtureSelfServiceProvider`) are appended to `tests/ClubGear.ArchitectureTests/Fixtures/AllowedPluginRuntimeFixtures.cs`, following the established pattern for all other plugin fixture types in that file. `FixtureSelfServiceProvider` is a concrete minimal implementation of `ISelfServiceProfileSectionProvider` with `Task.FromResult<SelfServiceProfileSection?>(null)` and `Task.FromResult(new PluginMemberActionResult(false, "noop", ""))` bodies.

### Rationale

The failure mode (silent null from `CreateMemberProvider` on a stale host, or a `TypeLoadException` swallowed as a warning) does not surface in any existing test because no existing test exercises the `ISelfServiceProfileSectionProvider` round-trip through `PluginLoader` -> `PluginRegistry` -> `CreateMemberProvider`. Adding this test ensures that any future regression (e.g., a refactor that breaks the type delegation in `PluginAssemblyLoadContext`) is caught at CI time rather than at deployment time. The test runs in-process against the current post-iteration-5 contracts assembly and does not require a Finance ZIP or a real SQLite database.

---

## 2026-06-25 - Defensive Guard: TypeLoadException Catch in SelfServiceSectionService

### Decision

In `Services/Core/SelfServiceSectionService.cs`, each call to `_pluginRegistryReader.CreateMemberProvider<ISelfServiceProfileSectionProvider>(...)` is wrapped in its own try/catch that catches `TypeLoadException` specifically. The catch block logs at `LogError` level, naming the plugin module ID, the provider type string, and the full exception — then continues to the next contribution. This pattern is applied to both call sites: the one in `GetSelfServiceSectionsAsync` and the one in `ExecuteSelfServiceActionAsync`. The existing `catch (Exception ex)` blocks that wrap the `InvokeAsync` / `ExecuteSelfServiceActionAsync` calls are left unchanged.

The `TypeLoadException` is caught at the `SelfServiceSectionService` level rather than inside `PluginRegistry.CreateMemberProvider` because `PluginRegistry` has no logger and is not the appropriate layer for diagnostic output. The service layer is the correct place to translate infrastructure exceptions into structured operational logs.

### Rationale

`CreateMemberProvider` is invoked outside the existing `try/catch` blocks in both `GetSelfServiceSectionsAsync` and `ExecuteSelfServiceActionAsync`. A `TypeLoadException` thrown by `Activator.CreateInstance` inside `CreateMemberProvider` therefore escapes those catch clauses and propagates to the caller, becoming user-visible. The fix is surgical: a targeted `TypeLoadException` catch at each call site converts an unhandled propagating exception into a `LogError` with structured diagnostic fields. `LogError` (rather than `LogWarning`) is used because a `TypeLoadException` from a provider instantiation unambiguously signals a broken deployment state — it is not a recoverable per-request condition. The message text explicitly calls out "veraltete Contracts-Assembly" to make future triage immediate.
