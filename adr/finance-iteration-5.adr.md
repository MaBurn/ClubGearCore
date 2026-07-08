# ADR: Finance Iteration 5

## Purpose

Persistent architectural decisions for the finance-iteration-5 iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-24 - New ISelfServiceProfileSectionProvider Contract Interface

### Decision

A new `ISelfServiceProfileSectionProvider` interface is introduced in `Contracts/Plugin/`. It exposes two methods: `GetSectionAsync` (read-only HTML block for the self-service profile page) and `ExecuteSelfServiceActionAsync` (permission-free action dispatch for the submitting member). A companion sealed record `SelfServiceProfileSection(Key, Title, HtmlBody, Order)` carries the rendered content. A new `PluginSelfServiceProfileProviderContribution(ProviderType, Order)` record and a default-body `AddSelfServiceProfileSection` method on `IPluginContributionSink` complete the contract surface. `ContractVersion.Current` is bumped from `1.7.0` to `1.8.0`.

### Rationale

Parallel to all other provider types in the codebase (`IMemberDetailCardProvider`, `IMemberEditTabProvider`, `IMemberActionProvider`, `IPluginPageProvider`). Placing the interface in `Contracts/Plugin/` is mandatory because Finance loads via ZIP into an isolated `AssemblyLoadContext` — it can only reference the shared contracts assembly. The default-body method on `IPluginContributionSink` preserves backwards compatibility: existing plugins that do not call it compile and run unchanged.

---

## 2026-06-24 - Separate Self-Service Dispatch Path (ISelfServiceSectionService)

### Decision

A new `ISelfServiceSectionService` (with implementation `SelfServiceSectionService`) is introduced in `Services/Abstractions/` and `Services/Core/` respectively. It owns two responsibilities: aggregating self-service sections for rendering (`GetSelfServiceSectionsAsync`) and executing self-service actions (`ExecuteSelfServiceActionAsync`). Both methods call `IPluginRuntimeAdapter.InvokeAsync` with `requiredPermissionKey: null`, explicitly bypassing the permission gate that would block a self-service member from executing finance write operations.

A new API endpoint `POST /api/self-service/plugin-self-service-actions` is added to `SelfServiceApiController` and routes exclusively to `ISelfServiceSectionService`. The existing `POST /api/self-service/plugin-actions` endpoint and `IMemberPluginSlotService.ExecuteActionAsync` remain unchanged.

### Rationale

Research finding confirmed that `MemberPluginSlotService.ExecuteActionAsync` calls `bridge.HasPermissionAsync(action.PermissionKey)`, which returns `false` for self-service members who do not hold `finance.member.write`. Three bypass options were evaluated: (a) a separate `ISelfServiceActionProvider` dispatch path, (b) a bypass flag on `MemberActionSlot`, (c) granting a scoped permission at dispatch time. Option (a) was pre-decided as the cleanest: no flag pollution on existing records, no fake permission grants, and the new dispatch path is clearly isolated from the admin action path. The Finance self-service action key `finance.account.selfservice.replace` is intentionally distinct from the admin key `finance.account.replace` to prevent cross-path confusion.

---

## 2026-06-24 - RegisteredPluginRuntime Extended with SelfServiceProfileProviders

### Decision

`RegisteredPluginRuntime` (a positional sealed record in `Services/Abstractions/IPluginRegistryReader.cs`) gains a new final parameter `IReadOnlyList<PluginSelfServiceProfileProviderContribution> SelfServiceProfileProviders`. The private `PluginContributionCollector` class inside `PluginLoader` gains a matching list field, a public property, and the `AddSelfServiceProfileSection` override. The single `RegisteredPluginRuntime` construction site inside `PluginLoader.LoadAsync` is updated to pass `collector.SelfServiceProfileProviders`.

`ISelfServiceSectionService` iterates `runtime.SelfServiceProfileProviders` directly, keeping the new slot type isolated from `MemberPluginSlotSnapshot` and `_PluginSlots.cshtml` (which would require null-guards if extended).

### Rationale

Every prior plugin contribution type (MemberProviders, BackgroundJobs, NavEntries, AuditSinks, IdentityProviders) follows the same pattern: a list on `PluginContributionCollector` collected during `RegisterContributions`, then stored on `RegisteredPluginRuntime` for later lookup. Diverging from this pattern would require a separate lookup table or secondary registry, adding complexity for no gain. Extending the record is the minimal change that fits the established architecture. Keeping self-service sections out of `MemberPluginSlotSnapshot` avoids touching the shared member-admin rendering path.

---

## 2026-06-24 - Finance Plugin Version Bump to 1.4.0

### Decision

The Finance plugin version advances from `1.3.0` to `1.4.0` in both `FinancePluginModule.Manifest` and `plugin.json`. The `extensionPoints` array in `plugin.json` gains `"selfservice.profile"`. A new `FinanceSelfServiceSectionProvider` class (implementing `ISelfServiceProfileSectionProvider`) is added to `FinanceProviders.cgcs`. `FinancePluginModule.RegisterContributions` calls `sink.AddSelfServiceProfileSection(...)` with `Order: 10`.

The `performedBy` value passed to `FinanceDataService.ReplaceAccountAsync` for self-service submissions is `"Mitglied (Self-Service)"` to distinguish it from Kassenwart-initiated replacements in the audit log.

No new permission constants are introduced; no `PermissionSeedTask` changes are required. The self-service section renders unconditionally for any linked member (authorization is already enforced at controller level by `PermissionKeys.SelfServiceAccess`).

### Rationale

`"selfservice.profile"` is already present in `PluginExtensionPoints.All` and passes manifest validation without any validator change (confirmed by research). Version 1.3.0 ZIP already exists in `dist/`; 1.4.0 is the next natural version. The `performedBy` distinction is important for Kassenwart auditability so that self-service changes are clearly attributed in the finance audit log.
