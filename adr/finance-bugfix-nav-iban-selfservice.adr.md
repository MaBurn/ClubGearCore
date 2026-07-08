# ADR: Finance Bugfix — Nav, IBAN, Self-Service

## Purpose

Persistent architectural decisions for the finance-bugfix-nav-iban-selfservice iteration.

Append new entries. Do not rewrite prior history.

---

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

---

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

---

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

---

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
