# ADR: Finance Iteration 3

## Purpose

Persistent architectural decisions for the finance-iteration-3 iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-22 - ReplaceAccountAsync Orphan Fix: Transition PendingVerification to Invalid

### Decision

`ReplaceAccountAsync` will additionally query all existing `PendingVerification` rows for the member and transition them to `Invalid` (via `UPDATE ... SET Status='Invalid' WHERE MemberId=? AND Status='PendingVerification'`) before inserting the new `PendingVerification` row. The audit's `before` snapshot will include both the previously-Verified and previously-Pending accounts. The audit's `after` snapshot will reflect all transitions: old Verified → MarkedForDeletion, old Pending → Invalid, new → PendingVerification.

### Rationale

Research confirmed the bug (Q1): the existing code filters `activeAccounts` by `Status == Verified` only and never touches existing `PendingVerification` rows. A second `ReplaceAccountAsync` call while a pending row exists leaves an orphan `PendingVerification` row that will never be transitioned. This violates the spec requirement that only one pending row exists at a time and that old pending entries are invalidated on replacement.

---

## 2026-06-22 - Actor Identity via arguments["performedBy"] Convention

### Decision

No contract-level change is made to `IPluginPageProvider.ExecuteCommandAsync` or `PluginMemberActionRequest` in this iteration. Actor identity is passed as `arguments["performedBy"]` — a string field included in `PluginFieldSchema` for each Kassenwart command's `ArgumentSchema`, and read from `arguments.GetValueOrDefault("performedBy") ?? string.Empty` inside the plugin. An empty string is stored as `'System'` in `bank_account_audit.PerformedBy`. This is a new convention, not an existing one.

### Rationale

Research confirmed (Q6) that no actor/user field exists in `PluginMemberActionRequest` or `IPluginPageProvider.ExecuteCommandAsync`, and that the host service layer does not inject any actor value into the `arguments` dictionary. A contract-level change would require modifying core ClubGear service code and is out of scope for a plugin iteration. The workaround of including `performedBy` as a named argument field is consistent with how other optional plugin fields are handled, and is sufficient for audit purposes.

---

## 2026-06-22 - Single IPluginPageProvider Constraint: Integrate Kassenwart Commands into Existing Provider

### Decision

`FinanceKassenwartPageProvider` remains the sole `IPluginPageProvider` registered by the Finance plugin module (Order: 0). New Verify and Invalidate commands are added to its `Commands` list in `GetPageDefinitionAsync` and handled in its `ExecuteCommandAsync` switch. No second `IPluginPageProvider` class is created.

### Rationale

`PluginPageService.ResolveProvider<T>` uses `.OrderBy(c => c.Order).FirstOrDefault()` — only the lowest-order provider is dispatched. Registering a second provider would make it silently unreachable. The existing `FinanceKassenwartPageProvider` already multiplexes row-level detail via `entityKey != null` in `GetRowsAsync`; adding command dispatch to `ExecuteCommandAsync` follows the same pattern with no structural change to the contract.

---

## 2026-06-22 - BLZ Lookup: Embedded Bundesbank CSV over OpenIBAN API

### Decision

BIC and bank name auto-fill on account entry uses an embedded `bundesbank_blz.csv` file (Bundesbank BLZ-Datei) included as an `<EmbeddedResource>` in `Finance.Plugin.csproj`. The `BundesbankBlzService` loads the CSV once via a lazy static dictionary keyed on 8-digit BLZ. Lookup is only performed for DE IBANs (BLZ extracted from IBAN positions 4–11). Non-DE IBANs receive no auto-fill. The CSV is manually updated when Bundesbank publishes a new quarterly release.

### Rationale

Research confirmed (Q4) that `System.Net.Http` is not in `ForbiddenNamespaces` so an HTTP approach would compile, but an embedded CSV is preferable: it is deterministic, requires no network, avoids rate-limit or availability risk, and is auditable. The OpenIBAN API option is deferred indefinitely. The `<EmbeddedResource>` item group is new precedent for plugin projects; it is safe to add because `EnableDefaultCompileItems` is `false` and resources must be declared explicitly.

---

## 2026-06-22 - Migration 004: Add PerformedBy Column to bank_account_audit

### Decision

A new migration class `FinanceAuditPerformedByMigration` with `MigrationId = "004_finance_audit_performedby"` is added in a new file `FinanceAuditMigration.cgcs`. It executes `ALTER TABLE {auditTable} ADD COLUMN PerformedBy TEXT NOT NULL DEFAULT 'System'`. The migration is registered in `FinancePluginModule.GetMigrations()` as the fourth entry.

`WriteAuditAsync` gains a `string performedBy` parameter and includes `PerformedBy` in its INSERT statement. All call sites pass the actor string resolved at the action/command entry point.

### Rationale

Research confirmed (Q3) that `bank_account_audit` has no `PerformedBy` column. The audit table is plugin-owned and queryable, making it the right vehicle for actor-enriched audit history. SQLite `ALTER TABLE ADD COLUMN` with a `DEFAULT` is safe and does not require a data migration. Existing rows will be assigned the default value `'System'`.

---

## 2026-06-22 - IbanValidator and BicValidator: Extracted Static Validators with Country-Length Map

### Decision

`IbanValidator` and `BicValidator` are extracted as `internal static class` types in a new file `FinanceValidators.cgcs`. `IbanValidator.Validate` replaces the existing `IsValidIban` private method in `FinanceDataService` and adds a country-specific length check. `BicValidator.Validate` replaces the existing inline BIC regex in `Validate()` and corrects the pattern from `^[A-Z0-9]{8}([A-Z0-9]{3})?$` to `^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$` (first 6 chars must be letters per SWIFT spec).

### Rationale

Separating validators into their own file makes them independently testable and avoids growing `FinanceData.cgcs` further. The BIC regex correction is a correctness fix — the existing regex permits all-numeric bank codes which are not valid SWIFT BICs. Country-length validation for IBAN provides user-facing feedback (e.g., "DE-IBAN must be 22 characters") rather than only a generic "formal error" from modulo-97 alone.

---

## 2026-06-22 - Audit Log Display: Rendered for All Finance Tab Viewers (No Additional Gate)

### Decision

The audit log HTML section appended in `FinanceEditTabProvider.GetTabsAsync` is visible to any user who can see the Finance Member Edit tab. No additional permission check is applied within the tab provider.

### Rationale

`GetTabsAsync` receives only `IPluginHostContext`, which does not expose a `ClaimsPrincipal` or a `HasPermissionAsync` method directly accessible without a bridge. The Member Edit tab itself is already gated by the host application's permission system before the tab provider is invoked. Adding a secondary gate inside the tab provider would require threading permission context in a way that is not supported by the current contract. The audit log contains operational metadata (action names, timestamps, actor names) that is appropriate for all administrators with Finance tab access.
