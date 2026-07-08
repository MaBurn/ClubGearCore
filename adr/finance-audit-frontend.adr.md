# ADR: Finance Frontend Audit

## Purpose

Persistent architectural decisions for the finance-audit-frontend iteration.
Append new entries. Do not rewrite prior history.

## 2026-06-24 - Self-Service Form Field Scope is Intentional

### Decision

The self-service account replacement form renders exactly three fields: `accountHolder`, `iban`, and `bic`. The fields `bankName`, `accountNumber`, `bankCode`, and `sepaDirectDebitId` are intentionally absent from the member-facing form. This is confirmed as correct behaviour, not a gap.

### Rationale

`bankName` and `bic` are auto-populated by BLZ lookup in `ReplaceAccountAsync` for supported IBANs; presenting them to members would be redundant and potentially confusing. `accountNumber` and `bankCode` are legacy pre-SEPA fields; modern SEPA accounts are fully identified by IBAN alone. `sepaDirectDebitId` is a club-managed field — SEPA mandates are set by the treasurer after verifying the new account, not by the member. Hard-coding `SepaDirectDebitId: null` on self-service replace and requiring an admin to restore it is the correct workflow.

## 2026-06-24 - Self-Service Validation is Equivalent to Admin Path

### Decision

The `string.IsNullOrWhiteSpace` guards in `ExecuteSelfServiceActionAsync` are a redundant early-exit only. Full IBAN modulo-97 validation (`IbanValidator.Validate`) and BIC format validation (`BicValidator.Validate`) are applied via `ReplaceAccountAsync`, which calls `Validate(input)` unconditionally before any database operation. No additional validation layer needs to be added to the self-service path.

### Rationale

The research confirmed that `ReplaceAccountAsync` is the canonical validation gate for all account replacement paths. Adding a duplicate `Validate` call in `ExecuteSelfServiceActionAsync` would be redundant and create a maintenance burden (two places to update when validation rules change). The current architecture is correct.

## 2026-06-24 - SEPA Direct-Debit ID is Admin-Only (Intentional by Design)

### Decision

`sepaDirectDebitId` has no self-service input surface and never will. The sole write path is `UpdateSepaIdAsync`, exposed via the `finance.sepa.update` admin action. This is a confirmed design boundary, not a gap.

### Rationale

SEPA direct-debit mandates are a club-level contract, not a member preference. A member replacing their bank account data should not be able to set or carry forward a mandate ID — doing so would expose the club to SEPA mandate fraud risk. The mandate must be re-established by an authorised treasurer after reviewing the new account.

## 2026-06-24 - Kassenwart Page bankAccountId Pre-Population is a Genuine UX Gap

### Decision

The `finance.kassenwart.verify` and `finance.kassenwart.invalidate` page commands require the operator to manually type the `bankAccountId` as a plain number input. There is no pre-population from the displayed table row. This is identified as a genuine UX gap, ranked Priority 1 for remediation.

The recommended fix is Option A: render per-row inline action buttons in `FinanceKassenwartPageProvider.GetRowsAsync` with the `bankAccountId` pre-embedded in the HTML (hidden input or data attribute). This avoids framework-level changes to `_PluginPageActionModal.cshtml` and stays within the existing plugin boundary.

### Rationale

The `PluginPageCommand` schema system has no mechanism to inject row-level data into the modal at the time of writing. A framework extension would require changes to the shared `_PluginPageActionModal.cshtml` and the command dispatch API, impacting all plugins. The minimal-impact path is to render inline controls directly in the provider's HTML output, which is consistent with how `FinanceEditTabProvider` renders its read-only table — the provider controls its own HTML entirely. The `bankAccountId` is already available to `GetRowsAsync` from the database query, so no additional data fetching is needed.

## 2026-06-24 - Payments Table Write Path Deferred as Separate Iteration

### Decision

The `payments` table has no write path in the Finance plugin. This is classified as an unimplemented feature area (Priority 2, deferred), not a bug or gap in existing input surfaces. No implementation is proposed in this audit.

### Rationale

The payments import or manual-entry feature is a new functional scope. It would require either a CSV/SEPA XML import action (with a background job parser) or a manual payment-entry admin action schema. Either approach has sufficient complexity to warrant its own iteration with dedicated research, design, and testing phases. Bundling it into an audit pass would scope-creep the current work. The empty table causes no data loss and no incorrect display — it simply shows zero rows, which is accurate.

## 2026-06-24 - Admin Write Actions Fully Accessible to Write-Only Users

### Decision

A user holding only `FinancePermissions.Write` (without `KassenwartAccess`) can reach both `finance.account.replace` and `finance.sepa.update` via the `member.action` slot on the admin member-edit screen. This coverage is confirmed complete. No additional action surface is needed for Write-only users.

### Rationale

The `FinanceActionProvider` always adds `finance.account.replace` (gated by Write) regardless of account state. `finance.sepa.update` is added conditionally when a Verified account exists (also gated by Write). `finance.account.verify` and `finance.account.invalidate` are intentionally unavailable to Write-only users — they require `KassenwartAccess` by design. The Kassenwart page itself is also gated by `KassenwartAccess` at the navigation level, so Write-only users never see it. The permission model is implemented consistently with the role design.
