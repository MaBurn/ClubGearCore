# ADR: Membership Fees

## Purpose

Persistent architectural decisions for the `membership-fees` iteration (recurring
membership fee demands, dunning, and SEPA pain.008 direct-debit export). This iteration
spans the core (`MembershipType`, plugin contract, plugin-page transport) and the Finance
plugin. It is placed in the repo's existing `ADRs/` directory rather than a lowercase
`adr/` folder, matching project convention.

Append new entries. Do not rewrite prior history.

## 2026-07-13 - Fee amount + billing interval live on the core MembershipType

### Decision
Add nullable `FeeAmount` (decimal) and `BillingInterval` (`Yearly`/`Quarterly`) to the core
`Models/MembershipType`, mapped in `ApplicationDbContext`, edited in the MembershipTypes
admin UI, and applied via a new idempotent `Data/Migrations` ALTER registered in
`ApplicationSeeder`. The Finance plugin does not own the fee schedule.

### Rationale
The fee is intrinsically a property of the membership type, which is core-owned data. A
plugin-local fee table would duplicate core data and drift. Nullable columns keep every
pre-existing type valid and make the SQLite ALTER a cheap, in-place, non-rebuilding
operation.

## 2026-07-13 - Expose membership type + fee/interval through the plugin contract (ContractVersion 1.10.0)

### Decision
Extend `PluginMemberSummary` (add `MembershipTypeId`) and `PluginMemberDetail` (add
`MembershipTypeId`, `MembershipTypeName`, `MembershipFeeAmount`, `MembershipBillingInterval`)
additively, and copy them in the `PluginHostContext` mapper. Bump
`ContractVersion.Current` 1.9.0 → 1.10.0; keep `MinimumSupported` at 1.8.0. Update the
`Slice1IdentityProviderContractTests` assertion. Bump Finance `RequiredCoreVersion` to
`>=1.10.0`.

### Rationale
The plugin can only see member data through the host DTOs, which currently drop the
membership type entirely — so there is no way to reach the fee/interval without a contract
change. Adding fields at the end of the positional records is purely additive, so
consumers lose nothing and the minimum-supported floor stays at 1.8.0. Because the change
is an additive minor, CarInfo and ServiceBook (which sit at/below the 1.8.0 floor and
consume none of the new fields) do not need repackaging; only Finance does.

## 2026-07-13 - Deliver pain.008 as a file payload on the command result

### Decision
Extend `PluginCommandResult` with an optional `PluginCommandFile` (FileName, ContentType,
ContentBase64). `PluginPage_API` returns it inside the existing JSON response;
`_PluginPageActionModal.cshtml` decodes the base64 into a Blob and triggers a browser
download. Chosen over (b) a dedicated host GET download endpoint and (c) returning XML as
copyable text.

### Rationale
The export is already a treasurer command with permission checks and data access in its
handler; reusing that pipeline avoids a second authenticated GET route and a separate
provider export capability. The addition is optional and rides the same 1.10.0 additive
bump, so it costs no extra version event. SEPA collection files are small, so base64 in
JSON is acceptable. Copyable text was rejected as poor UX for a bank-upload file.

## 2026-07-13 - Member-scoped fee_demands table with its own audit; plugin-owned SEPA config

### Decision
Add a plugin-owned `fee_demands` table (member, period, due date, amount, status, snapshot
membership type + mandate reference, optional payment link, collection timestamp) with a
unique `(MemberId, Period)` index and a companion `fee_demand_audit` table following the
established audit shape. Add a single-row `sepa_config` table (creditor id/name/IBAN)
mirroring `club_accounts`. Reuse the existing `bank_accounts.SepaDirectDebitId` as the
mandate reference and add only a nullable `MandateSignatureDate` column. Migrations
`008_finance_fee_demands`, `009_finance_sepa_config`, `010_finance_bank_account_mandate`.

### Rationale
Demands are a distinct lifecycle from payments/bookings and need their own audit trail and
idempotent generation key. There is no general config store, so club-level SEPA settings
follow the existing club-scoped plugin-owned table precedent. Reusing `SepaDirectDebitId`
avoids redundant mandate storage; the single added column is a cheap in-place ALTER.

## 2026-07-13 - Manual treasurer generation and pain.008 via BCL, folded into the single Kassenwart provider

### Decision
No scheduler. Demand generation, mark-paid, dunning refresh, SEPA config edit, and pain.008
export are `RequiresEntityKey:false` commands on `FinanceKassenwartPageProvider` under a new
`finance.feedemand.*` / `finance.sepa.*` prefix branch, all gated by `KassenwartAccess`.
FRST/RCUR is derived from `fee_demands` collection history. pain.008.001.02 is built with
BCL `System.Xml.Linq`, reusing `IbanValidator`/`BicValidator`/`BundesbankBlzService` — no
new NuGet. Finance version 1.8.0 → 1.9.0; repackaging rotates the git-ignored signing key.

### Rationale
Manual generation matches the confirmed scope and avoids introducing background
infrastructure. The club-level command template already exists for club accounts/bookings,
so the new operations fold in without disturbing member/payment/booking sections. The BCL
covers pain.008 fully, keeping the dependency surface unchanged.
