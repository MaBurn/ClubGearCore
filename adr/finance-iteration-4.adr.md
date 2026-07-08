# ADR: Finance Iteration 4

## Purpose

Persistent architectural decisions for the finance-iteration-4 iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-23 - EntityKey Bug Fix: Parse entityKey as memberId, Add bankAccountId Argument Field

### Decision

The `EntityKeyColumn: "MemberId"` declaration in `FinanceKassenwartPageProvider.GetPageDefinitionAsync` is kept unchanged. The fix is applied in `ExecuteCommandAsync`: the first parse block is renamed so that `entityKey` is parsed as `memberId` (not `bankAccountId`). A new required `bankAccountId` Number argument field (min: 1) is added to both the `finance.kassenwart.verify` and `finance.kassenwart.invalidate` command schemas. The `memberId` Number argument field is removed from both schemas. `ExecuteCommandAsync` reads `bankAccountId` from `arguments["bankAccountId"]`.

### Rationale

Research (Q1) confirmed the bug: `entityKey` carries the row's MemberId value, but the old code parsed it as `bankAccountId`, causing `VerifyAccountAsync` / `InvalidateAccountAsync` to always return `not-found`. Fix option (b) — keep one row per member and add a `bankAccountId` argument — was selected as the pre-confirmed user decision. This approach is identical to the existing `finance.account.delete-marked` pattern in `FinanceActionProvider` and requires no structural change to the list view or `PageColumns`. The operator sources the bank account ID from the Finance edit tab's accounts table (which gains an Id column in Slice 9).

---

## 2026-06-23 - VerifyAccountAsync Status Guard: PendingVerification Only

### Decision

`VerifyAccountAsync` retains its existing guard: only accounts with `Status == PendingVerification` can be verified. `MarkedForDeletion` accounts cannot be directly verified via either surface.

### Rationale

This was the pre-confirmed user decision. Conceptually, a `MarkedForDeletion` account is a deprecated record awaiting cleanup after a new account is active. Allowing verify on it would reactivate a superseded account, which is an unusual and risky operation. The `InvalidateAccountAsync` method already accepts `MarkedForDeletion` and provides the correct disposal path for such records. No data layer change is needed.

---

## 2026-06-23 - MarkedForDeletion Accounts Transition to Invalid on Verify (Soft, Not Hard Delete)

### Decision

When `VerifyAccountAsync` is called successfully, all `MarkedForDeletion` accounts for the same member are transitioned to `Invalid` via `UPDATE ... SET Status = 'Invalid' WHERE MemberId = @memberId AND Status = 'MarkedForDeletion'`. No `DELETE` statement is issued.

### Rationale

This is the existing implementation in `FinanceData.cgcs` (lines 365–373) and matches the task description's "Status Invalid oder gelöscht" clause — `Invalid` is an acceptable outcome. Soft transition preserves audit continuity: `Invalid` rows remain visible in `GetAccountsAsync` and in `FinanceEditTabProvider`, giving the Kassenwart a complete account history. Hard deletes are reserved for the explicit `finance.account.delete-marked` flow which requires a separate intentional action.

---

## 2026-06-23 - New MemberActionSlots for Verify and Invalidate in FinanceActionProvider

### Decision

Two new `MemberActionSlot` entries — `finance.account.verify` and `finance.account.invalidate` — are added to `FinanceActionProvider.GetActionsAsync`. Each requires a `bankAccountId` Number argument and an optional `performedBy` Text argument. The verify slot is shown only when a `PendingVerification` account exists; the invalidate slot is shown when any `PendingVerification` or `MarkedForDeletion` account exists. Both slots use `FinancePermissions.KassenwartAccess`. Dispatch is handled in `ExecuteAsync` via two new private helper methods following the `DeleteMarkedAsync` pattern.

### Rationale

Research (Q4) confirmed that `IMemberEditTabProvider` returns raw HTML only and has no button registration mechanism. `IMemberActionProvider` with `MemberActionSlot` is the correct integration point for operator-triggered actions in the member panel. The Finance plugin is already registered as `PluginMemberSlotKind.Action` (line 41 of `FinancePluginModule.cgcs`), so no module registration change is needed. Adding the two slots mirrors the existing `finance.account.delete-marked` pattern exactly and requires no new contract surfaces.

---

## 2026-06-23 - Add Id Column to FinanceEditTabProvider Main Accounts Table

### Decision

`FinanceEditTabProvider.GetTabsAsync` gains an `Id` column as the first column in the main bank accounts `<table>`. The column header is `<th>ID</th>` and each row emits `<td>{account.Id}</td>` (integer, no HTML encoding needed).

### Rationale

This was the pre-confirmed user decision. With `bankAccountId` now a required argument in both the Kassenwart page commands and the new MemberActionSlots, operators need a convenient place to find the ID. The audit log sub-table already shows `Konto-ID`, but that requires scrolling past the main table. Adding the `Id` column to the top-level table gives the ID immediate visibility alongside the account's status and IBAN.

---

## 2026-06-23 - No Data Layer Changes in This Iteration

### Decision

`FinanceDataService.VerifyAccountAsync` and `FinanceDataService.InvalidateAccountAsync` are not modified. No database migrations are required.

### Rationale

Research confirmed (Q2, Q3) that the data layer logic is correct: `VerifyAccountAsync` properly transitions the target account to `Verified` and all `MarkedForDeletion` rows to `Invalid`; `InvalidateAccountAsync` accepts both `PendingVerification` and `MarkedForDeletion`. The only defects were in the provider layer (incorrect `entityKey` interpretation and absent argument fields). Fixing the provider is sufficient to make the commands functional.

---

## 2026-06-23 - Plugin Version Bump to 1.3.0

### Decision

`Finance.Plugin.csproj` version is bumped from `1.2.0` to `1.3.0`. A new signed distribution archive is produced via `./build-and-export.sh`.

### Rationale

The Finance plugin has produced versioned, signed archives at each iteration. This iteration adds user-visible interface changes (new action slots, updated command schemas, new table column) that constitute a minor version increment under the plugin's established versioning practice.
