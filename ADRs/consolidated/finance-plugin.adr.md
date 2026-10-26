# ADR: Finance Plugin

## Purpose

Persistent architectural decisions for the Finance plugin, including account lifecycle, audit history, self-service surfaces, Kassenwart workflows, validation, contract compatibility and bugfix decisions.

## Decision History Summary

- Account lifecycle decisions define a single active or pending bank account path per member, explicit invalidation of superseded pending rows, and audit snapshots for replacement, verification and invalidation workflows.
- Kassenwart workflows live in the existing Finance page provider and use host-provided permission infrastructure where available; later bugfix decisions tighten navigation visibility, IBAN unmasking and DB-backed role checks.
- Self-service decisions keep member-facing Finance output compact and host-compatible while avoiding extra contract changes unless the shared plugin contract truly changes.
- Import, validation and lookup decisions prefer deterministic local behavior, including embedded BLZ data and explicit schema/version steps, over external network dependencies.

## Consolidation

This ADR consolidates the previous iteration-scoped ADR files listed below. The dated decision entries are preserved under source sections so the decision history remains append-only and traceable.

Sources:

- `finance-iteration-3.adr.md`
- `finance-iteration-4.adr.md`
- `finance-iteration-5.adr.md`
- `finance-iteration-6.adr.md`
- `finance-iteration-7.adr.md`
- `finance-audit-frontend.adr.md`
- `finance-bugfix-nav-iban-selfservice.adr.md`
- `finance-bugfix-typeload.adr.md`

---

## Source: Finance Iteration 3

Former file: `finance-iteration-3.adr.md`


## 2026-06-22 - ReplaceAccountAsync Orphan Fix: Transition PendingVerification to Invalid

### Decision

`ReplaceAccountAsync` will additionally query all existing `PendingVerification` rows for the member and transition them to `Invalid` (via `UPDATE ... SET Status='Invalid' WHERE MemberId=? AND Status='PendingVerification'`) before inserting the new `PendingVerification` row. The audit's `before` snapshot will include both the previously-Verified and previously-Pending accounts. The audit's `after` snapshot will reflect all transitions: old Verified → MarkedForDeletion, old Pending → Invalid, new → PendingVerification.

### Rationale

Research confirmed the bug (Q1): the existing code filters `activeAccounts` by `Status == Verified` only and never touches existing `PendingVerification` rows. A second `ReplaceAccountAsync` call while a pending row exists leaves an orphan `PendingVerification` row that will never be transitioned. This violates the spec requirement that only one pending row exists at a time and that old pending entries are invalidated on replacement.


## 2026-06-22 - Actor Identity via arguments["performedBy"] Convention

### Decision

No contract-level change is made to `IPluginPageProvider.ExecuteCommandAsync` or `PluginMemberActionRequest` in this iteration. Actor identity is passed as `arguments["performedBy"]` — a string field included in `PluginFieldSchema` for each Kassenwart command's `ArgumentSchema`, and read from `arguments.GetValueOrDefault("performedBy") ?? string.Empty` inside the plugin. An empty string is stored as `'System'` in `bank_account_audit.PerformedBy`. This is a new convention, not an existing one.

### Rationale

Research confirmed (Q6) that no actor/user field exists in `PluginMemberActionRequest` or `IPluginPageProvider.ExecuteCommandAsync`, and that the host service layer does not inject any actor value into the `arguments` dictionary. A contract-level change would require modifying core ClubGear service code and is out of scope for a plugin iteration. The workaround of including `performedBy` as a named argument field is consistent with how other optional plugin fields are handled, and is sufficient for audit purposes.


## 2026-06-22 - Single IPluginPageProvider Constraint: Integrate Kassenwart Commands into Existing Provider

### Decision

`FinanceKassenwartPageProvider` remains the sole `IPluginPageProvider` registered by the Finance plugin module (Order: 0). New Verify and Invalidate commands are added to its `Commands` list in `GetPageDefinitionAsync` and handled in its `ExecuteCommandAsync` switch. No second `IPluginPageProvider` class is created.

### Rationale

`PluginPageService.ResolveProvider<T>` uses `.OrderBy(c => c.Order).FirstOrDefault()` — only the lowest-order provider is dispatched. Registering a second provider would make it silently unreachable. The existing `FinanceKassenwartPageProvider` already multiplexes row-level detail via `entityKey != null` in `GetRowsAsync`; adding command dispatch to `ExecuteCommandAsync` follows the same pattern with no structural change to the contract.


## 2026-06-22 - BLZ Lookup: Embedded Bundesbank CSV over OpenIBAN API

### Decision

BIC and bank name auto-fill on account entry uses an embedded `bundesbank_blz.csv` file (Bundesbank BLZ-Datei) included as an `<EmbeddedResource>` in `Finance.Plugin.csproj`. The `BundesbankBlzService` loads the CSV once via a lazy static dictionary keyed on 8-digit BLZ. Lookup is only performed for DE IBANs (BLZ extracted from IBAN positions 4–11). Non-DE IBANs receive no auto-fill. The CSV is manually updated when Bundesbank publishes a new quarterly release.

### Rationale

Research confirmed (Q4) that `System.Net.Http` is not in `ForbiddenNamespaces` so an HTTP approach would compile, but an embedded CSV is preferable: it is deterministic, requires no network, avoids rate-limit or availability risk, and is auditable. The OpenIBAN API option is deferred indefinitely. The `<EmbeddedResource>` item group is new precedent for plugin projects; it is safe to add because `EnableDefaultCompileItems` is `false` and resources must be declared explicitly.


## 2026-06-22 - Migration 004: Add PerformedBy Column to bank_account_audit

### Decision

A new migration class `FinanceAuditPerformedByMigration` with `MigrationId = "004_finance_audit_performedby"` is added in a new file `FinanceAuditMigration.cgcs`. It executes `ALTER TABLE {auditTable} ADD COLUMN PerformedBy TEXT NOT NULL DEFAULT 'System'`. The migration is registered in `FinancePluginModule.GetMigrations()` as the fourth entry.

`WriteAuditAsync` gains a `string performedBy` parameter and includes `PerformedBy` in its INSERT statement. All call sites pass the actor string resolved at the action/command entry point.

### Rationale

Research confirmed (Q3) that `bank_account_audit` has no `PerformedBy` column. The audit table is plugin-owned and queryable, making it the right vehicle for actor-enriched audit history. SQLite `ALTER TABLE ADD COLUMN` with a `DEFAULT` is safe and does not require a data migration. Existing rows will be assigned the default value `'System'`.


## 2026-06-22 - IbanValidator and BicValidator: Extracted Static Validators with Country-Length Map

### Decision

`IbanValidator` and `BicValidator` are extracted as `internal static class` types in a new file `FinanceValidators.cgcs`. `IbanValidator.Validate` replaces the existing `IsValidIban` private method in `FinanceDataService` and adds a country-specific length check. `BicValidator.Validate` replaces the existing inline BIC regex in `Validate()` and corrects the pattern from `^[A-Z0-9]{8}([A-Z0-9]{3})?$` to `^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$` (first 6 chars must be letters per SWIFT spec).

### Rationale

Separating validators into their own file makes them independently testable and avoids growing `FinanceData.cgcs` further. The BIC regex correction is a correctness fix — the existing regex permits all-numeric bank codes which are not valid SWIFT BICs. Country-length validation for IBAN provides user-facing feedback (e.g., "DE-IBAN must be 22 characters") rather than only a generic "formal error" from modulo-97 alone.


## 2026-06-22 - Audit Log Display: Rendered for All Finance Tab Viewers (No Additional Gate)

### Decision

The audit log HTML section appended in `FinanceEditTabProvider.GetTabsAsync` is visible to any user who can see the Finance Member Edit tab. No additional permission check is applied within the tab provider.

### Rationale

`GetTabsAsync` receives only `IPluginHostContext`, which does not expose a `ClaimsPrincipal` or a `HasPermissionAsync` method directly accessible without a bridge. The Member Edit tab itself is already gated by the host application's permission system before the tab provider is invoked. Adding a secondary gate inside the tab provider would require threading permission context in a way that is not supported by the current contract. The audit log contains operational metadata (action names, timestamps, actor names) that is appropriate for all administrators with Finance tab access.


## Source: Finance Iteration 4

Former file: `finance-iteration-4.adr.md`


## 2026-06-23 - EntityKey Bug Fix: Parse entityKey as memberId, Add bankAccountId Argument Field

### Decision

The `EntityKeyColumn: "MemberId"` declaration in `FinanceKassenwartPageProvider.GetPageDefinitionAsync` is kept unchanged. The fix is applied in `ExecuteCommandAsync`: the first parse block is renamed so that `entityKey` is parsed as `memberId` (not `bankAccountId`). A new required `bankAccountId` Number argument field (min: 1) is added to both the `finance.kassenwart.verify` and `finance.kassenwart.invalidate` command schemas. The `memberId` Number argument field is removed from both schemas. `ExecuteCommandAsync` reads `bankAccountId` from `arguments["bankAccountId"]`.

### Rationale

Research (Q1) confirmed the bug: `entityKey` carries the row's MemberId value, but the old code parsed it as `bankAccountId`, causing `VerifyAccountAsync` / `InvalidateAccountAsync` to always return `not-found`. Fix option (b) — keep one row per member and add a `bankAccountId` argument — was selected as the pre-confirmed user decision. This approach is identical to the existing `finance.account.delete-marked` pattern in `FinanceActionProvider` and requires no structural change to the list view or `PageColumns`. The operator sources the bank account ID from the Finance edit tab's accounts table (which gains an Id column in Slice 9).


## 2026-06-23 - VerifyAccountAsync Status Guard: PendingVerification Only

### Decision

`VerifyAccountAsync` retains its existing guard: only accounts with `Status == PendingVerification` can be verified. `MarkedForDeletion` accounts cannot be directly verified via either surface.

### Rationale

This was the pre-confirmed user decision. Conceptually, a `MarkedForDeletion` account is a deprecated record awaiting cleanup after a new account is active. Allowing verify on it would reactivate a superseded account, which is an unusual and risky operation. The `InvalidateAccountAsync` method already accepts `MarkedForDeletion` and provides the correct disposal path for such records. No data layer change is needed.


## 2026-06-23 - MarkedForDeletion Accounts Transition to Invalid on Verify (Soft, Not Hard Delete)

### Decision

When `VerifyAccountAsync` is called successfully, all `MarkedForDeletion` accounts for the same member are transitioned to `Invalid` via `UPDATE ... SET Status = 'Invalid' WHERE MemberId = @memberId AND Status = 'MarkedForDeletion'`. No `DELETE` statement is issued.

### Rationale

This is the existing implementation in `FinanceData.cgcs` (lines 365–373) and matches the task description's "Status Invalid oder gelöscht" clause — `Invalid` is an acceptable outcome. Soft transition preserves audit continuity: `Invalid` rows remain visible in `GetAccountsAsync` and in `FinanceEditTabProvider`, giving the Kassenwart a complete account history. Hard deletes are reserved for the explicit `finance.account.delete-marked` flow which requires a separate intentional action.


## 2026-06-23 - New MemberActionSlots for Verify and Invalidate in FinanceActionProvider

### Decision

Two new `MemberActionSlot` entries — `finance.account.verify` and `finance.account.invalidate` — are added to `FinanceActionProvider.GetActionsAsync`. Each requires a `bankAccountId` Number argument and an optional `performedBy` Text argument. The verify slot is shown only when a `PendingVerification` account exists; the invalidate slot is shown when any `PendingVerification` or `MarkedForDeletion` account exists. Both slots use `FinancePermissions.KassenwartAccess`. Dispatch is handled in `ExecuteAsync` via two new private helper methods following the `DeleteMarkedAsync` pattern.

### Rationale

Research (Q4) confirmed that `IMemberEditTabProvider` returns raw HTML only and has no button registration mechanism. `IMemberActionProvider` with `MemberActionSlot` is the correct integration point for operator-triggered actions in the member panel. The Finance plugin is already registered as `PluginMemberSlotKind.Action` (line 41 of `FinancePluginModule.cgcs`), so no module registration change is needed. Adding the two slots mirrors the existing `finance.account.delete-marked` pattern exactly and requires no new contract surfaces.


## 2026-06-23 - Add Id Column to FinanceEditTabProvider Main Accounts Table

### Decision

`FinanceEditTabProvider.GetTabsAsync` gains an `Id` column as the first column in the main bank accounts `<table>`. The column header is `<th>ID</th>` and each row emits `<td>{account.Id}</td>` (integer, no HTML encoding needed).

### Rationale

This was the pre-confirmed user decision. With `bankAccountId` now a required argument in both the Kassenwart page commands and the new MemberActionSlots, operators need a convenient place to find the ID. The audit log sub-table already shows `Konto-ID`, but that requires scrolling past the main table. Adding the `Id` column to the top-level table gives the ID immediate visibility alongside the account's status and IBAN.


## 2026-06-23 - No Data Layer Changes in This Iteration

### Decision

`FinanceDataService.VerifyAccountAsync` and `FinanceDataService.InvalidateAccountAsync` are not modified. No database migrations are required.

### Rationale

Research confirmed (Q2, Q3) that the data layer logic is correct: `VerifyAccountAsync` properly transitions the target account to `Verified` and all `MarkedForDeletion` rows to `Invalid`; `InvalidateAccountAsync` accepts both `PendingVerification` and `MarkedForDeletion`. The only defects were in the provider layer (incorrect `entityKey` interpretation and absent argument fields). Fixing the provider is sufficient to make the commands functional.


## 2026-06-23 - Plugin Version Bump to 1.3.0

### Decision

`Finance.Plugin.csproj` version is bumped from `1.2.0` to `1.3.0`. A new signed distribution archive is produced via `./build-and-export.sh`.

### Rationale

The Finance plugin has produced versioned, signed archives at each iteration. This iteration adds user-visible interface changes (new action slots, updated command schemas, new table column) that constitute a minor version increment under the plugin's established versioning practice.


## Source: Finance Iteration 5

Former file: `finance-iteration-5.adr.md`


## 2026-06-24 - New ISelfServiceProfileSectionProvider Contract Interface

### Decision

A new `ISelfServiceProfileSectionProvider` interface is introduced in `Contracts/Plugin/`. It exposes two methods: `GetSectionAsync` (read-only HTML block for the self-service profile page) and `ExecuteSelfServiceActionAsync` (permission-free action dispatch for the submitting member). A companion sealed record `SelfServiceProfileSection(Key, Title, HtmlBody, Order)` carries the rendered content. A new `PluginSelfServiceProfileProviderContribution(ProviderType, Order)` record and a default-body `AddSelfServiceProfileSection` method on `IPluginContributionSink` complete the contract surface. `ContractVersion.Current` is bumped from `1.7.0` to `1.8.0`.

### Rationale

Parallel to all other provider types in the codebase (`IMemberDetailCardProvider`, `IMemberEditTabProvider`, `IMemberActionProvider`, `IPluginPageProvider`). Placing the interface in `Contracts/Plugin/` is mandatory because Finance loads via ZIP into an isolated `AssemblyLoadContext` — it can only reference the shared contracts assembly. The default-body method on `IPluginContributionSink` preserves backwards compatibility: existing plugins that do not call it compile and run unchanged.


## 2026-06-24 - Separate Self-Service Dispatch Path (ISelfServiceSectionService)

### Decision

A new `ISelfServiceSectionService` (with implementation `SelfServiceSectionService`) is introduced in `Services/Abstractions/` and `Services/Core/` respectively. It owns two responsibilities: aggregating self-service sections for rendering (`GetSelfServiceSectionsAsync`) and executing self-service actions (`ExecuteSelfServiceActionAsync`). Both methods call `IPluginRuntimeAdapter.InvokeAsync` with `requiredPermissionKey: null`, explicitly bypassing the permission gate that would block a self-service member from executing finance write operations.

A new API endpoint `POST /api/self-service/plugin-self-service-actions` is added to `SelfServiceApiController` and routes exclusively to `ISelfServiceSectionService`. The existing `POST /api/self-service/plugin-actions` endpoint and `IMemberPluginSlotService.ExecuteActionAsync` remain unchanged.

### Rationale

Research finding confirmed that `MemberPluginSlotService.ExecuteActionAsync` calls `bridge.HasPermissionAsync(action.PermissionKey)`, which returns `false` for self-service members who do not hold `finance.member.write`. Three bypass options were evaluated: (a) a separate `ISelfServiceActionProvider` dispatch path, (b) a bypass flag on `MemberActionSlot`, (c) granting a scoped permission at dispatch time. Option (a) was pre-decided as the cleanest: no flag pollution on existing records, no fake permission grants, and the new dispatch path is clearly isolated from the admin action path. The Finance self-service action key `finance.account.selfservice.replace` is intentionally distinct from the admin key `finance.account.replace` to prevent cross-path confusion.


## 2026-06-24 - RegisteredPluginRuntime Extended with SelfServiceProfileProviders

### Decision

`RegisteredPluginRuntime` (a positional sealed record in `Services/Abstractions/IPluginRegistryReader.cs`) gains a new final parameter `IReadOnlyList<PluginSelfServiceProfileProviderContribution> SelfServiceProfileProviders`. The private `PluginContributionCollector` class inside `PluginLoader` gains a matching list field, a public property, and the `AddSelfServiceProfileSection` override. The single `RegisteredPluginRuntime` construction site inside `PluginLoader.LoadAsync` is updated to pass `collector.SelfServiceProfileProviders`.

`ISelfServiceSectionService` iterates `runtime.SelfServiceProfileProviders` directly, keeping the new slot type isolated from `MemberPluginSlotSnapshot` and `_PluginSlots.cshtml` (which would require null-guards if extended).

### Rationale

Every prior plugin contribution type (MemberProviders, BackgroundJobs, NavEntries, AuditSinks, IdentityProviders) follows the same pattern: a list on `PluginContributionCollector` collected during `RegisterContributions`, then stored on `RegisteredPluginRuntime` for later lookup. Diverging from this pattern would require a separate lookup table or secondary registry, adding complexity for no gain. Extending the record is the minimal change that fits the established architecture. Keeping self-service sections out of `MemberPluginSlotSnapshot` avoids touching the shared member-admin rendering path.


## 2026-06-24 - Finance Plugin Version Bump to 1.4.0

### Decision

The Finance plugin version advances from `1.3.0` to `1.4.0` in both `FinancePluginModule.Manifest` and `plugin.json`. The `extensionPoints` array in `plugin.json` gains `"selfservice.profile"`. A new `FinanceSelfServiceSectionProvider` class (implementing `ISelfServiceProfileSectionProvider`) is added to `FinanceProviders.cgcs`. `FinancePluginModule.RegisterContributions` calls `sink.AddSelfServiceProfileSection(...)` with `Order: 10`.

The `performedBy` value passed to `FinanceDataService.ReplaceAccountAsync` for self-service submissions is `"Mitglied (Self-Service)"` to distinguish it from Kassenwart-initiated replacements in the audit log.

No new permission constants are introduced; no `PermissionSeedTask` changes are required. The self-service section renders unconditionally for any linked member (authorization is already enforced at controller level by `PermissionKeys.SelfServiceAccess`).

### Rationale

`"selfservice.profile"` is already present in `PluginExtensionPoints.All` and passes manifest validation without any validator change (confirmed by research). Version 1.3.0 ZIP already exists in `dist/`; 1.4.0 is the next natural version. The `performedBy` distinction is important for Kassenwart auditability so that self-service changes are clearly attributed in the finance audit log.


## Source: Finance Iteration 6

Former file: `finance-iteration-6.adr.md`



## Source: Finance Iteration 7

Former file: `finance-iteration-7.adr.md`



## Source: Finance Frontend Audit

Former file: `finance-audit-frontend.adr.md`



## Source: Finance Bugfix — Nav, IBAN, Self-Service

Former file: `finance-bugfix-nav-iban-selfservice.adr.md`


## 2026-06-25 - Bug 1: PluginNavEntryService DB Fallback via IPermissionService

### Decision

`PluginNavEntryService` is refactored to inject `IPermissionService` and call
`await _permissionService.HasPermissionAsync(user, entry.RequiredPermission)` for
each nav entry that declares a `RequiredPermission`. The method implementation
becomes `async`. The previous claim-only predicate (`user.Claims.Any(c => c.Type
== "permission" && ...)`) is removed entirely.

### Rationale

The existing claim-only check never resolves permissions that are stored only in
the `RolePermissions` DB table (e.g. `clubgear.plugin.finance.kassenwart.access`
for the Kassenwart role). No `"permission"` claims are written to the
authentication cookie at sign-in for any role-based permission. The wildcard
check already present handles master-admin only. `DatabasePermissionService`
(the registered `IPermissionService` implementation) performs claims-first then
DB role lookup, making it the correct and already-existing service for this
purpose. Injecting it into `PluginNavEntryService` is the minimal, correct fix
that requires no new infrastructure and no changes to the plugin contract.


## 2026-06-25 - Bug 2: Self-Service HTML Restructured to Single Visual Section

### Decision

`FinanceSelfServiceSectionProvider.BuildSelfServiceHtml` is restructured to
remove the `<hr/>` separator and the inner `<h6>Neue Bankverbindung einreichen</h6>`
heading. The status block (Kontoinhaber / IBAN / Status) is compacted to a
single-line `<p class="text-muted small mb-2">` element instead of three
separate `<p>` tags. The form fields follow directly without a visual break. The
submit button label is contextualised: "Bankverbindung aktualisieren" when an
existing account is present, "Bankverbindung hinterlegen" when none exists.

### Rationale

The card title "Meine Bankverbindung" (rendered by the host's Profile.cshtml
card wrapper) already names the section. An inner heading and `<hr/>` divider
create the impression of two independent sub-sections, which the user reported
as confusing. The detailed multi-`<p>` status block adds visual weight
disproportionate to self-service context: a member only needs to see their
current status at a glance, not an administrative summary. Compacting it to one
line and removing the internal divider eliminates the "two sections" appearance
without removing any functionality.


## 2026-06-25 - Bug 3: IBAN Unmasking via IPluginPermissionFacade on IPluginHostContext

### Decision

A new `IPluginPermissionFacade` interface is added to `IPluginHostContext`
in the contracts assembly (`Contracts/Plugin/IPluginHostContext.cs`):

```csharp
public interface IPluginHostContext
{
    // existing members unchanged
    IPluginPermissionFacade Permissions { get; }   // added
}

public interface IPluginPermissionFacade
{
    Task<bool> HasPermissionAsync(string permissionKey,
        CancellationToken cancellationToken = default);
}
```

`PluginHostContext` is updated to accept a
`Func<string, CancellationToken, Task<bool>> permissionResolver` constructor
parameter and expose it through a private `PluginPermissionFacade` nested class.
`PluginRuntimeAdapter.CreateBridge` wires `bridge.HasPermissionAsync` as the
resolver (using a captured variable to break the circular construction
dependency between bridge and context).

`FinanceEditTabProvider.GetTabsAsync`, `FinanceDetailCardProvider.GetCardsAsync`,
and `FinanceKassenwartPageProvider.GetRowsAsync` each call
`await hostContext.Permissions.HasPermissionAsync(FinancePermissions.KassenwartAccess)`
once before building their HTML output. If the check returns true, the raw IBAN
is used; otherwise `FinanceDataService.MaskIban` is applied.

The `ContractVersion.Current` remains `1.8.0`. The Finance plugin version
advances from 1.5.0 to 1.6.0. CarInfo and ServiceBook must be rebuilt against
the updated contracts DLL and repackaged, but their source code is unchanged.

### Rationale

`IPluginHostContext` is exclusively a host-provided interface. No plugin
implements it; plugins only consume it. Adding a new property therefore cannot
break existing plugin source code. The concrete implementation lives in
`PluginHostContext` (host code, not contracts assembly), so the compiled plugin
DLLs do not reference the new type directly and will not break at load time.

The alternative of passing `bool hasFullAccess` via a changed
`IMemberEditTabProvider.GetTabsAsync` signature was rejected: it would require
modifying the contract interface and potentially rewriting the host dispatch
path in `MemberPluginSlotService.CollectEditTabsAsync`, impacting all plugins
that implement `IMemberEditTabProvider`.

The alternative of doing the permission check inside `CollectEditTabsAsync` and
passing a pre-resolved result to the plugin was also rejected: it would require
either changing the contract signature or embedding business logic (which
permission to check) inside generic host infrastructure, creating a coupling
between the host slot service and Finance-specific permission constants.

Exposing `IPluginPermissionFacade` on `IPluginHostContext` is the correct
architectural boundary: it gives plugins a first-class, sanctioned path to
resolve permissions at render time without any forbidden namespace references,
and mirrors the pattern already established by `bridge.HasPermissionAsync` at
the adapter level.


## 2026-06-25 - Repackaging Scope: All Three Plugins Must Be Rebuilt

### Decision

CarInfo and ServiceBook must be rebuilt against the updated
`ClubGear.Plugin.Contracts.dll` and repackaged (new `.zip`, `.sha256`,
`.signature`). Their plugin version numbers do not change. Finance advances to
1.6.0.

### Rationale

The contracts assembly gains a new interface member. Even though CarInfo and
ServiceBook do not use `IPluginPermissionFacade`, they ship their own copy of
`ClubGear.Plugin.Contracts.dll` inside their ZIP packages. If only the Finance
package is updated, the host would load three different versions of the contracts
DLL in the same process, which can cause type-identity mismatches. Rebuilding
and repackaging all plugins against the same contracts DLL version ensures
runtime consistency.


## Source: Finance Bugfix Typeload

Former file: `finance-bugfix-typeload.adr.md`


## 2026-06-25 - Root Cause: Deployment Gap, Not Code Bug

### Decision

The `TypeLoadException` on `FinanceSelfServiceSectionProvider` is caused by a deployment gap: `ISelfServiceProfileSectionProvider` was added to `Contracts/Plugin/` in iteration 5, but the host container image was not rebuilt after that change. The plugin ZIP contains only `Finance.Plugin.dll` and `plugin.json`; `PluginAssemblyLoadContext.Load` unconditionally delegates the contracts assembly to `AssemblyLoadContext.Default`, so the plugin always sees the host-loaded binary. The CLR assembly version is always `1.0.0.0` (no `<Version>` property in the csproj), so the CLR cannot detect the stale binary at load time. The JIT throws when materializing the vtable for `FinanceSelfServiceSectionProvider` inside `PluginRegistry.CreateMemberProvider<ISelfServiceProfileSectionProvider>` at `Activator.CreateInstance(providerClrType)`. The `SelfServiceSectionService` catch block swallows the exception as a `LogWarning`.

The primary fix is a container rebuild and redeploy. No code change in `PluginLoader`, `PluginRegistry`, or `SelfServiceSectionService` is required.

### Rationale

The architecture test suite and source tree are in the post-iteration-5 state; all required types exist. The problem is exclusively a runtime deployment state mismatch. Treating this as a code bug requiring defensive code in `CreateMemberProvider` would add complexity without addressing the underlying cause, and would mask future deployment gaps of the same class.


## 2026-06-25 - Install-Time Guard: Raise MinimumSupported to 1.8.0 and Finance requiredCoreVersion to >=1.8.0

### Decision

`ContractVersion.MinimumSupported` is raised from `1.0.0` to `1.8.0` in `Contracts/Plugin/ContractVersion.cs`. Finance's `"requiredCoreVersion"` in `plugins/Finance/plugin.json` is updated from `">=1.0.0"` to `">=1.8.0"`. No change is made to `ContractCompatibilityService.Validate` or `PluginManifestParser`.

After this change, installing Finance 1.5.0 on a stale host (where `ContractVersion.Current < 1.8.0`) will be rejected at install time with `"Plugin-Vertrag ist nicht kompatibel."` rather than silently degrading at runtime. All currently-installed plugins that declare `">=1.0.0"` continue to pass validation on the current (iteration-5+) host because `Validate` compares the plugin's declared `RequiredContractVersion` against `MinimumSupported`, and plugins pass `Version(1,0,0)` which is below the new minimum — meaning they will also be rejected on a stale host. This is the desired outcome: any plugin compiled against the iteration-5 contracts surface should not be installable on a host that predates iteration 5.

The existing `ContractCompatibilityServiceTests` "below minimum" test uses `new Version(MinimumSupported.Major, MinimumSupported.Minor)` which resolves to `Version(1, 8)`. In .NET, `Version(1, 8)` is less than `Version(1, 8, 0)` because `Build` is `-1` vs `0`. The test continues to pass without modification.

### Rationale

The `requiredCoreVersion` mechanism exists precisely to express a minimum host contract version a plugin requires. Finance 1.5.0 requires `ISelfServiceProfileSectionProvider`, which was introduced in `ContractVersion.Current = 1.8.0`. Declaring `>=1.8.0` is the semantically correct declaration. Raising `MinimumSupported` to `1.8.0` ensures the rejection is both at the contract level (individual plugin declares `>=1.8.0`) and at the floor level (the host rejects any plugin that would require a contracts surface older than the current minimum). This is a one-time ratchet: `MinimumSupported` only moves forward.


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


## 2026-06-25 - Defensive Guard: TypeLoadException Catch in SelfServiceSectionService

### Decision

In `Services/Core/SelfServiceSectionService.cs`, each call to `_pluginRegistryReader.CreateMemberProvider<ISelfServiceProfileSectionProvider>(...)` is wrapped in its own try/catch that catches `TypeLoadException` specifically. The catch block logs at `LogError` level, naming the plugin module ID, the provider type string, and the full exception — then continues to the next contribution. This pattern is applied to both call sites: the one in `GetSelfServiceSectionsAsync` and the one in `ExecuteSelfServiceActionAsync`. The existing `catch (Exception ex)` blocks that wrap the `InvokeAsync` / `ExecuteSelfServiceActionAsync` calls are left unchanged.

The `TypeLoadException` is caught at the `SelfServiceSectionService` level rather than inside `PluginRegistry.CreateMemberProvider` because `PluginRegistry` has no logger and is not the appropriate layer for diagnostic output. The service layer is the correct place to translate infrastructure exceptions into structured operational logs.

> **Superseded in part (CORE-17, 2026-09):** the premise that `PluginRegistry` has no logger and no DI dependencies no longer holds. `PluginRegistry` now takes `IPluginActivityTracker`, `IOptions<PluginRuntimeOptions>` and `ILogger<PluginRegistry>` so that deactivation can drain in-flight plugin work and report an overrun. Where the `TypeLoadException` is caught is unchanged; only the stated reason is out of date.

### Rationale

`CreateMemberProvider` is invoked outside the existing `try/catch` blocks in both `GetSelfServiceSectionsAsync` and `ExecuteSelfServiceActionAsync`. A `TypeLoadException` thrown by `Activator.CreateInstance` inside `CreateMemberProvider` therefore escapes those catch clauses and propagates to the caller, becoming user-visible. The fix is surgical: a targeted `TypeLoadException` catch at each call site converts an unhandled propagating exception into a `LogError` with structured diagnostic fields. `LogError` (rather than `LogWarning`) is used because a `TypeLoadException` from a provider instantiation unambiguously signals a broken deployment state — it is not a recoverable per-request condition. The message text explicitly calls out "veraltete Contracts-Assembly" to make future triage immediate.


## Source: Payment Capture

Former file: `payment-capture.adr.md`

## 2026-07-11 - Category is a host datalist combo, not a plugin-only degrade

### Decision
Add a new `PluginSchemaFieldType.Combo` to `Contracts/Plugin/PluginSchemaContracts.cs` (reusing
the existing `Options` list) and a `<datalist>`-backed `<input>` render branch in both schema
renderers (`Views/PluginPage/_PluginPageActionModal.cshtml` and `wwwroot/js/site.js`). This is a
host change: it recompiles `ClubGear.Plugin.Contracts`, bumps `ContractVersion.Current`
1.8.0 → 1.9.0 (additive enum = SemVer minor), leaves `MinimumSupported` at 1.8.0, raises Finance's
`RequiredCoreVersion` to `>=1.9.0`, and requires a root `CHANGELOG.md` entry.

### Rationale
The user explicitly requires "Auswahlliste + Freitext" on the create/edit form on BOTH surfaces.
Those forms are the schema-driven action/command modals, which today offer only a fixed `Select`
(no free text) or plain `Text` (no suggestions). A `<datalist>` in a raw-HTML edit-tab surface
would not reach the create modal, so the plugin-only alternative cannot satisfy the requirement.
`Combo` is a reusable, additive UI primitive that belongs in the shared contracts, not duplicated
per plugin. The additive enum is backward compatible, so the compatibility floor does not move.

## 2026-07-11 - Delete is a hard delete preceded by an audit before-image

### Decision
`finance.payment.delete` physically removes the row (`DELETE FROM {payments} WHERE Id=@id AND
MemberId=@memberId`) after first writing the complete before-image to the payment audit trail.
No soft-delete status is introduced; reads are not filtered by a cancelled status.

### Rationale
Mirrors the established `DeleteMarkedAccountAsync` precedent (audit-before-physical-DELETE). It
keeps every payment read/list query simple (no status filter) and still preserves the deleted
record for accountability in the audit log. A soft-delete status would diverge from precedent and
add a filter condition to every read for no confirmed requirement.

## 2026-07-11 - Dedicated payment_audit table with its own display path

### Decision
Create a new `payment_audit` table via migration `006_finance_payment_audit` with its own
record/display types, a `DeserializePaymentSummary` mapper, and a separate "Zahlungs-
Änderungsprotokoll" render block in the Finanzen edit tab. `bank_account_audit` is untouched.

### Rationale
`bank_account_audit` has no `PaymentId` and its display path (`DeserializeStatus`) extracts only a
`Status` property, silently dropping amount/category/purpose/date and intermingling payment rows
with bank-account rows in the member's log. A dedicated table represents payment CRUD losslessly,
keeps the bank-account "Alter/Neuer Status" view clean, and avoids changing the shared
`WriteAuditAsync` signature. It is fully plugin-internal.

## 2026-07-11 - Fixed default status on create; status not user-editable

### Decision
On create, `Status` is set to the constant `DefaultPaymentStatus = "Erfasst"`. There is no status
picker on the create or edit form, and edit preserves the existing `Status`.

### Rationale
`payments.Status` is `NOT NULL` with no DB default, so every insert must supply a value. The
confirmed scope removes user status selection; a single captured-state default is the simplest
correct choice and keeps the create form focused on the data the treasurer actually enters.

## 2026-07-11 - Per-payment addressing: visible Id column plus per-row command arguments

### Decision
Make individual payments addressable by `PaymentId`. On the member surface, `FinanceEditTabProvider`
renders a payments table (raw HTML) whose visible `Id` column feeds a numeric `paymentId` field in
the `finance.payment.edit`/`finance.payment.delete` action schemas. On the treasurer page,
`FinanceKassenwartPageProvider` emits a per-payment sub-list with a `PaymentId` column and carries
`{"paymentId":"<id>"}` in each row's `CommandArguments`; `ExecuteCommandAsync` keeps `entityKey` on
`MemberId` and reads `paymentId` from the arguments.

### Rationale
The page contract already supports arbitrary columns, per-row commands, and per-row
`CommandArguments` (exactly how `bankAccountId` rides today), and the member action modal already
uses visible-id + typed-id fields (`delete-marked`, `verify`). Reusing both mechanisms adds
per-payment targeting on both surfaces with no host change beyond the `Combo` field type.

## 2026-07-11 - Version bumps and repackaging scope

### Decision
Finance → 1.7.0 (`requiredCoreVersion >=1.9.0`), rebuilt and repackaged via
`scripts/package-plugin.sh plugins/Finance` with the existing signing key. `ContractVersion.Current`
→ 1.9.0, `MinimumSupported` stays 1.8.0. CarInfo and ServiceBook are NOT repackaged and their
versions and `requiredCoreVersion` are unchanged.

### Rationale
Finance is a new feature (minor bump) and genuinely references the new `Combo` member, so it must
rebuild. Per `adr/finance-plugin-packaging.adr.md`, the contracts DLL is not bundled in plugin
ZIPs and rebuilt plugin DLLs are byte-identical unless they reference new contract types; CarInfo
and ServiceBook reference nothing new. Because the host change is an additive minor that keeps the
compatibility floor at 1.8.0, their existing `>=1.8.0` remains valid — so the "Contracts changed ⇒
repackage all contracts-sharing plugins" premise does not apply and repackaging them would be a
compatibility no-op.


## Source: Club Accounts and Bookings

Former file: `club-accounts-bookings.adr.md`

## 2026-07-12 - Additive `club_accounts` + `bookings` tables (Option B), not nullable `payments.MemberId` (Option A)

### Decision
Model member-less club finance as two new additive tables — `club_accounts`
(Id, Name, Type, Iban NULL, CreatedAtUtc) and `bookings` (Id, ClubAccountId,
Amount, BookingDate, Category NULL, Purpose NULL, CounterpartyIban NULL, Status,
CreatedAtUtc) — created via a single additive migration
`007_finance_club_accounts_bookings`. Leave `payments` and `payment_audit`
completely untouched. Reject Option A (rebuild `payments` to make `MemberId`
nullable and add `ClubAccountId`).

### Rationale
Option A requires the first-ever SQLite 12-step table rebuild in the codebase,
over live production `payments` rows, plus a second forced rebuild of
`payment_audit` (its `MemberId` is `NOT NULL`), plus member-less branches across
every int-`memberId`-keyed helper in `FinanceData.cgcs`, plus review of the
tests that assert the current `payments` shape. Option B is byte-for-byte the
established additive `CREATE TABLE IF NOT EXISTS` pattern (`bank_accounts`,
`payments`, `payment_audit`), preserves live data trivially, touches no existing
query or test, and avoids the unprecedented rebuild. A club booking (a Kassenbuch
line tied to a club account, amount sign = direction, no double-entry) is also a
genuinely different domain concept from a member-scoped payment; a dedicated
table keeps both read paths simple and self-isolating.

## 2026-07-12 - Single-account cash-book booking model; amount sign encodes direction

### Decision
Each `bookings` row references exactly ONE `club_accounts` row via `ClubAccountId`
and stores a signed `Amount` (negative = outflow, positive = inflow). No
counter-account, no transfer, no double-entry. `Status` defaults to `Erfasst` in
code on create (no user status picker), mirroring the payment default.

### Rationale
Confirmed scope is a Kassenbuch. A single signed amount per account is the
simplest correct representation and matches the payment precedent for the fixed
default status.

## 2026-07-12 - App-enforced ClubAccountId reference; no DB foreign keys

### Decision
`ClubAccountId → club_accounts.Id` integrity is enforced in application code
(validate the account exists before insert/update; guard account deletion against
referencing bookings). No `FOREIGN KEY` clause and no `PRAGMA foreign_keys`.

### Rationale
No plugin `CREATE TABLE` in the codebase declares a foreign key and there is no
runtime foreign-key pragma; SQLite defaults `foreign_keys` to OFF. The existing
`MemberId↔member` linkage is already a bare `INTEGER` enforced only in app code.
Following that precedent keeps the new tables consistent with the platform.

## 2026-07-12 - Dedicated `booking_audit` table mirroring `payment_audit`

### Decision
Add a `booking_audit` table keyed by `ClubAccountId` (with nullable `BookingId`)
alongside `bookings`, written via `WriteBookingAuditAsync` using the same
audit-before-write / audit-before-physical-delete pattern as payments.
`payment_audit` is untouched.

### Rationale
`payment_audit.MemberId` is `NOT NULL` and its whole write/display path is
int-`memberId`-keyed. Reusing it for member-less bookings would force another live
rebuild. A parallel audit table represents booking CRUD losslessly, keeps the
member audit view clean, and stays fully additive.

## 2026-07-12 - Fold treasurer UI into the single Kassenwart page; no core host change

### Decision
Surface club-account and booking management inside the existing
`FinanceKassenwartPageProvider`: prepend a club section to the list rows, add
list-level (`RequiresEntityKey:false`) `finance.clubaccount.*` /
`finance.booking.*` commands, carry row identity via per-row `CommandArguments`
(the `bankAccountId`/`paymentId` mechanism), and branch `ExecuteCommandAsync` on
the command prefix before the member-id parse. Do NOT change the core host to
dispatch page providers by `pageKey`.

### Rationale
`PluginPageService.ResolveProvider` selects exactly one provider per module and
ignores the `pageKey` route segment, so a second separately-routed page is
unreachable today. Folding in is fully plugin-internal. A host change to dispatch
by `pageKey` is a core change (root `CHANGELOG.md`, cross-plugin routing
regression surface for CarInfo/ServiceBook, repackaging implications) that buys
only cosmetic separation. Reuse `KassenwartAccess` for all new commands so no new
permission string, and thus no manifest/`plugin.json` permission change, is
needed.

## 2026-07-12 - Version bump and repackaging scope

### Decision
Finance `Version` 1.7.0 → 1.8.0 (new feature = SemVer minor), rebuilt and
repackaged via `scripts/package-plugin.sh plugins/Finance`. No host/contract
change: `ContractVersion.Current` stays 1.9.0, `MinimumSupported` stays 1.8.0,
Finance `requiredCoreVersion` stays `>=1.9.0`. No root `CHANGELOG.md` entry (work
is confined to the `plugins/Finance` submodule). CarInfo and ServiceBook are NOT
repackaged and their versions / `requiredCoreVersion` are unchanged.

### Rationale
The change is a self-contained new feature that touches no shared contract, no
host code, and no other plugin. Per the packaging ADR, plugins that reference
nothing new need no rebuild, and plugin-internal changes stay out of the core
changelog per `AGENTS.md`.


## Source: Finance Plugin Packaging

Former file: `finance-plugin-packaging.adr.md`

## 2026-07-10 - Scope is package-and-verify current source, not re-implementation

### Decision
Treat this iteration as packaging + verification of the already-implemented working tree
(Finance 1.6.3). The three fixes (DB-backed nav permission check, self-service HTML cleanup,
Kassenwart IBAN unmasking) are confirmed present and verified via the test suite, not rewritten.
Produce fresh signed artifacts for Finance 1.6.3, CarInfo 1.0.9, and ServiceBook 1.0.4.

### Rationale
Phase R static inspection confirmed all three changes exist in source with matching tests. The
task's "1.6.0" target is stale; `plugin.json` and the Finance manifest already declare 1.6.3.
Re-implementing would risk regressing verified behavior for no benefit.

## 2026-07-10 - Honest rationale for repackaging CarInfo and ServiceBook

### Decision
Repackage CarInfo and ServiceBook, but record that the substantive reason is the manifest
compatibility correction, not a DLL change.

### Rationale
The contracts assembly is not bundled in the plugin ZIP (`scripts/package-plugin.sh` zips only
`plugin.json` + `<Assembly>.Plugin.dll`; `PluginLoader` resolves contracts from the host default
ALC). Rebuilt DLLs are functionally identical (deterministic build, no newly-referenced 1.8.0
contract types, reference metadata stays `Version=1.0.0.0`). The only inherent byte change is the
freshly-timestamped ZIP → new hash/signature. Repackaging is worthwhile only because it ships the
`requiredCoreVersion` fix under a valid signature; the original "shared DLL" premise is incorrect
and is recorded here to prevent it recurring.

## 2026-07-10 - Raise CarInfo/ServiceBook requiredCoreVersion to >=1.8.0 at unchanged versions

### Decision
Change `requiredCoreVersion` from `">=1.0.0"` to `">=1.8.0"` in both `plugins/CarInfo/plugin.json`
and `plugins/ServiceBook/plugin.json`, keeping plugin versions at 1.0.9 and 1.0.4.

### Rationale
The installer parses the value after `>=` as a contract version and requires
`>= ContractVersion.MinimumSupported (1.8.0)`. `1.0.0 < 1.8.0` → the plugins would be rejected as
`incompatible` at install, defeating the point of repackaging. `">=1.8.0"` passes and matches
Finance. Lowering the core `MinimumSupported` was rejected because it weakens the compatibility
floor for all plugins. Versions stay unchanged per the confirmed input; same-version re-install is
accepted by `PluginInstallerService` (upsert, `already-installed`). The metadata-within-same-version
deviation is documented in each submodule changelog.

## 2026-07-10 - Reconcile Finance C# manifest RequiredCoreVersion to >=1.8.0

### Decision
Change `FinancePluginModule.Manifest.RequiredCoreVersion` from `">=1.0.0"` to `">=1.8.0"`.

### Rationale
The C# constant is inert at install/load (the host reads `plugin.json`, which is already
`">=1.8.0"`); its only reader is `PluginAdminQueryService` as a display-only fallback. Reconciling
removes a stale display inconsistency at zero runtime risk. Finance is rebuilt for 1.6.3 regardless,
so there is no extra cost.

## 2026-07-10 - Canonical packaging and full artifact set

### Decision
Package all three plugins with the shared `scripts/package-plugin.sh <plugin-dir>`, reusing the
existing local `.signing/signer-private.pem` keys (no regeneration). Emit the full artifact set per
plugin (`.zip`, `.sha256`, `.signature.b64`, `.signature.bin`, `signer-public.pem`), update
`PLUGIN-SHA256SUMS`, and regenerate the activation values in `plugins/aktivierung.md`.

### Rationale
The shared script is the established, per-plugin-agnostic tool (locates the single `*.Plugin.csproj`,
builds Release, zips, signs). Reusing the existing keys keeps every published `signerPublicKeyPem`
stable so already-installed plugins are unaffected. The installer's `TryBuildVerificationRequest`
requires hash + base64 signature + public-key PEM, so the full set is mandatory. Re-signing changes
each package's hash and signature, so `plugins/aktivierung.md` must be regenerated to keep published
descriptors installable.

## 2026-09-14 - Activation values are derived, not published

### Decision
The instructions at `:740` and `:747` to "regenerate the activation values in
`plugins/aktivierung.md`" no longer apply. `plugins/aktivierung.md` no longer carries hashes,
signatures or signer keys. After rebuilding and re-signing a Finance package, update
`PLUGIN-SHA256SUMS` and run `scripts/generate-plugin-activation-values.py plugins/Finance`, which
writes `plugins/Finance/aktivierungswerte.md` from `plugins/Finance/dist/`.

### Rationale
The published copies in `plugins/aktivierung.md` had drifted from the artifacts they described —
the CarInfo and Finance blocks there named versions (`1.0.9`, `1.8.0`) older than the shipped
manifests (`1.0.10`, `1.9.0`), and a hand-maintained copy of a derived value is a second place for
that drift to happen. The per-package `aktivierungswerte.md`, generated on demand and verified
against the recorded `.sha256` before it is trusted, is the only copy that cannot drift silently.
(CORE-22 item 4.)
