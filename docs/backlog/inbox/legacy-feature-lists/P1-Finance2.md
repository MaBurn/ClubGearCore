# Finance 2.0 and Membership Fee Runs

**Priority:** P1  
**Target:** `clubgear.plugin.finance`  
**Estimated effort:** XL (26–50+ person-days)

## Objective

Complete the existing Finance plugin with auditable fee calculation, payment import and matching,
SEPA collection, and treasurer workflows while preserving plugin-owned financial data.

## Functional requirements

1. Preserve the existing versioned bank-account and SEPA-mandate lifecycle and immutable audit.
2. Define membership fee rules by membership type, effective period, optional family relationship,
   discounts, and explicitly documented rounding behavior.
3. Generate reproducible fee runs with draft, reviewed, approved, exported, booked, and cancelled
   states as applicable.
4. Store the exact rule inputs and calculation result used for each demand.
5. Import supported payment files with validation, duplicate detection, preview, and rejection log.
6. Suggest deterministic member/demand matches and show the reasons and confidence inputs.
7. Treasurers can match, unmatch, rematch, and bulk-confirm payments with a complete audit trail.
8. Export matched/unmatched results and reconciliation views without exposing bank data to users
   lacking the relevant permission.
9. Generate standards-compliant pain.008 files only from eligible approved demands and mandates.
10. Record export batches and prevent accidental duplicate collection.
11. Notify members using semantic Core template events without embedding Core template IDs.
12. Provide treasurer dashboards for outstanding, paid, failed, and exceptional items.

## Security and compliance requirements

- Mask IBAN/BIC by default and require an explicit permission for unmasking.
- Encrypt sensitive values at rest using a managed key strategy; never log plaintext bank data.
- Every mutation records actor category, before/after state, timestamp, and reason.
- Financial records use explicit retention and deletion rules.

## Dependencies

- Versioned Templates, Family/Household Relationships, audit, notifications, and plugin UI contracts.
- Existing Finance plugin migrations/tests must be treated as the starting point.

## Out of scope

- General accounting, tax filing, payroll, or banking-provider login.
- Rental-specific deposits and payouts until a generic integration contract exists.
- Moving bank data into Core member tables.

## Acceptance criteria

- Re-running an unchanged fee calculation produces identical demands and totals.
- Importing the same payment source twice does not create duplicate payments.
- Match/unmatch and SEPA export operations are fully auditable and permission tested.
- Generated pain.008 files pass schema and representative bank-validation tests.
- Existing Finance data upgrades through versioned plugin migrations.

