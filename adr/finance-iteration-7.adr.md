# ADR: Finance Iteration 7

## Purpose

Persistent architectural decisions for the finance-iteration-7 iteration.
Append new entries. Do not rewrite prior history.

## 2026-06-24 - PerformedByCategory Column Added at Write Time

### Decision

Add a new `PerformedByCategory TEXT NOT NULL DEFAULT 'System'` column to `bank_account_audit` via migration `005_finance_audit_performed_by_category`. The column is populated at write time by a `ClassifyPerformedBy` helper in `FinanceDataService.WriteAuditAsync`. Classification rules: `"Mitglied (Self-Service)"` → `"Mitglied"`; empty string, `"System"`, or null → `"System"`; any other non-empty string → `"Verwaltung"`.

### Rationale

The task spec requires `AusgeführtVon` to distinguish Mitglied / Verwaltung / System. Classification at read time from `PerformedBy` is unreliable for historical rows where `PerformedBy` was stored as `""` (the column default collapses it to `"System"` but it could represent an uncaptured Verwaltung actor). Classifying at write time ensures all new rows are accurate. Historical rows receive `DEFAULT 'System'` on ALTER TABLE — an accepted one-time approximation for pre-migration data. This is the same approach used for `PerformedBy` itself in migration `004`.

## 2026-06-24 - Fix Two Empty-String PerformedBy Call Sites

### Decision

`UpdateSepaIdAsync` gains a `string performedBy` parameter; callers thread the `performedBy` argument through. `FinanceActionProvider` passes `arguments.GetValueOrDefault("performedBy") ?? string.Empty` for the `finance.sepa.update` action. `DeleteMarkedAccountAsync` replaces the hard-coded `string.Empty` argument to `WriteAuditAsync` with `"System"` — the delete action has no actor field in its schema and intentionally records no human actor.

### Rationale

Two call sites in `FinanceDataService` ignored the actor entirely and passed `string.Empty`, which the column default converts to `"System"`. `UpdateSepaIdAsync` is invoked from an action that does have a `performedBy` argument in its schema but the data service never received it. Threading the argument through allows Kassenwart-entered actor names to be captured. `DeleteMarkedAccountAsync` has no actor schema field by design (post-debit cleanup is a system-level step), so `"System"` is the correct explicit value rather than an empty string that happens to fall back to `"System"`.

## 2026-06-24 - BankAccountAuditDisplayRecord as Separate Read Model

### Decision

Introduce `BankAccountAuditDisplayRecord` as a new `internal sealed record` in `FinanceData.cgcs` alongside the existing `BankAccountAuditRecord`. The new record adds `PerformedByCategory`, `OldStatus`, and `NewStatus` properties. `BankAccountAuditRecord` is left unchanged. A new `GetAuditLogDisplayAsync` method returns `IReadOnlyList<BankAccountAuditDisplayRecord>`. `FinanceEditTabProvider` calls `GetAuditLogDisplayAsync`; existing tests that call `GetAuditLogAsync` continue to use `BankAccountAuditRecord` with no changes.

### Rationale

`BankAccountAuditRecord` is referenced directly in `FinanceSlice6AuditLogDisplayTests` (test 6.1 asserts on `.Action` and `.PerformedBy`). Adding properties to the record risks constructor-shape breakage in test construction and semantic confusion in test assertions. A separate display record is the zero-regression path: the data access contract is additive, not mutating, and the two records serve distinct audiences (test verification vs. HTML rendering).

## 2026-06-24 - AlterStatus/NeuerStatus Derived at Read Time (No New Column)

### Decision

`OldStatus` and `NewStatus` are derived at read time by deserialising `BeforeJson`/`AfterJson` via `JsonDocument.Parse`. For single-object JSON the `Status` property is extracted directly. For array JSON (as stored by `BankAccountReplaced`) the distinct `Status` values across all array elements are joined with `", "`. Parse failures or null JSON return `"–"`. No new schema columns (`OldStatus`, `NewStatus`) are added.

### Rationale

The research confirms that `BeforeJson`/`AfterJson` are full `BankAccountRecord` serialisations and always contain a `Status` property. Adding dedicated columns would require a migration and would duplicate data already present in JSON. Read-time derivation keeps the slice count low (one migration instead of two), has no write-path impact, and is lossless for all action types already in the codebase. The slight CPU cost of JSON parsing for up to 50 rows per tab render is negligible.

## 2026-06-24 - Audit Display Column Set

### Decision

The audit log HTML table in `FinanceEditTabProvider` displays six columns: `MemberId`, `Aktion`, `Alter Status`, `Neuer Status`, `Zeitstempel`, `Ausgeführt von`. The `Ausgeführt von` cell renders `PerformedByCategory` (one of `Mitglied`, `Verwaltung`, `System`) — not the raw `PerformedBy` free-text string. The `Konto-ID` column from the prior design is removed.

### Rationale

The task spec explicitly lists `MemberId`, `Aktion`, `AlterStatus`, `NeuerStatus`, `Zeitstempel`, and `AusgeführtVon (Mitglied/Verwaltung/System)` as the required fields. Rendering `PerformedByCategory` rather than raw `PerformedBy` in the `Ausgeführt von` cell is consistent with the requirement to show the category, not the freeform string. The `Konto-ID` column that existed previously is not in the spec for this display and is dropped to match the defined column set.

## 2026-06-24 - Migration Placement and Version Bump

### Decision

The new migration class `FinanceAuditPerformedByCategoryMigration` is placed in `FinanceAuditMigration.cgcs` as a second public class in that file. Version bumps from `1.4.1` to `1.5.0` (minor version) in `FinancePluginModule.cgcs`.

### Rationale

`FinanceSchemaMigration.cgcs` already hosts three migration classes as a precedent for multi-class migration files. Placing the new migration class in `FinanceAuditMigration.cgcs` (which currently holds only `FinanceAuditPerformedByMigration`) keeps audit-related migrations co-located. The version bump is minor (`1.4.x → 1.5.0`) because this iteration adds a new schema column, a new display method, and two call-site behavioural fixes — collectively a user-visible, non-breaking feature increment.
