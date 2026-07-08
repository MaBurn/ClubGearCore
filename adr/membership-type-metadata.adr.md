# ADR: Membership Type Metadata

## Purpose

Persistent architectural decisions for the membership-type-metadata iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-07-07 - Replace Static Member Type Flags With a Dynamic MembershipType + Metadata-Field Model

### Decision

`Member.IsCompany`, `CompanyName`, `IsClub`, `ClubName`, `FamilyMembership`, `MainMemberId`, and
`MembershipDiscount` are removed from `Models/Member.cs` and replaced by:
- `MembershipType` (`Id`, `Key`, `Name`, `Description`, `DefaultDiscountPercent`, `IsSystemDefined`,
  `SortOrder`, `IsActive`) - an admin-managed, fully dynamic list of member types (seeded with
  `Standard`, `Verein`, `Firma`, `Familie`).
- `MembershipTypeField` (`MembershipTypeId`, `Key`, `Label`, `FieldType` [`Text`/`Number`/`Boolean`/
  `Date`/`MemberReference`], `IsRequired`, `SortOrder`) - an admin-managed metadata-field definition
  per type.
- `MemberMetadataValue` (`MemberId`, `FieldId`, `Value`) - the per-member value for each field defined
  on the member's assigned type.
- `Member.MembershipTypeId` (FK, `Restrict` delete) replaces the seven removed scalar properties.

`Vereinsname`/`Firmenname` become `Text` fields on `Verein`/`Firma`. `Vereinszeitschrift` becomes a
`Boolean` field. `Mitgliedsbeitrag` becomes a `Number` field. `Hauptmitglied` becomes a
`MemberReference` field on `Familie`. The membership discount becomes primarily a
`MembershipType.DefaultDiscountPercent`, with an optional per-member `Number` override field
(`membership_discount_override`) seeded during migration from each member's prior individual
`MembershipDiscount` value, so no historical per-member discount value is lost even though the
mechanism is now type-driven by default.

### Rationale

The task explicitly asks for the member-type flags to stop being static/hardcoded ("dynamisch werden
und nicht weiter so statisch") and for the discount to be tied to the member type. A single admin-
configurable `MembershipType` + `MembershipTypeField` + `MemberMetadataValue` (EAV) model satisfies
both: any future member type (not just the four seeded ones) and any future per-type field (not just
Vereinsname/Firmenname/Vereinszeitschrift/Mitgliedsbeitrag/Hauptmitglied) can be added through the
admin UI without further schema changes. Modeling *all* per-type extras (including the three
explicitly named "settable per member" items) as typed metadata fields - rather than inventing three
more hardcoded boolean/columns - is the only option that is actually dynamic, per the task's stated
intent, and it reuses the `(EntityId, FieldKey, Value)` shape already established in the codebase by
`IMemberExtensionStore`'s per-plugin `member_ext` tables (research finding), applied here as a
core-owned table instead of a per-plugin one because Mitgliedsart is a core-domain concept, not a
plugin concern.

Keeping `DefaultDiscountPercent` at the type level (per the task's plain statement that the discount
"hängt an der Mitgliedsart") while preserving each member's original individual discount as an
override metadata value resolves the only real tension in the confirmed research inputs: "tied to the
type" vs. "no data loss" are both satisfied by treating the type value as a default and the migrated
per-member value as an explicit override, resolved at read-time by
`MemberDiscountResolver.GetEffectiveDiscountPercent`.

---

## 2026-07-07 - Legacy Members Columns Are Retained Physically But Dropped From the EF Model

### Decision

The seven legacy `Members` columns (`IsCompany`, `CompanyName`, `IsClub`, `ClubName`,
`FamilyMembership`, `MainMemberId`, `MembershipDiscount`) are left in place in the SQLite file (never
`DROP COLUMN`ed) but removed entirely from `Models/Member.cs` and every application code path. A new
idempotent migration class (`Data/Migrations/AddMembershipTypeModel_<timestamp>.cs`), registered in
`ApplicationSeeder.EnsureSqliteSchemaCompatibilityAsync`, creates the three new tables, seeds the four
system types and their field definitions, adds `Members.MembershipTypeId` (nullable), and backfills it
plus the corresponding `MemberMetadataValue` rows from the legacy columns.

### Rationale

This matches the project's existing, documented schema-management philosophy
(`docs/architecture/data-model-and-migrations.md`, `docs/architecture/member-legacy-compatibility.md`):
there is no EF Migrations folder, and prior migrations (e.g. `IsNotVerifyed` -> `IsVerified`,
`MembershipNumber` -> `MemberNumber`) have consistently left the old column physically present as a
passive rollback/forensics safety net rather than performing a destructive `DROP COLUMN`/table rebuild.
`Member.MembershipTypeId` is added as a nullable column (not `NOT NULL DEFAULT`) for the same reason
`MemberNumber`/`JoinedAt` were: the target of the default (the seeded `Standard` type's row) does not
exist yet at `ALTER TABLE` time, so a defensive `WHERE MembershipTypeId IS NULL` backfill `UPDATE`,
run unconditionally and idempotently on every startup, is used instead - identical in shape to the
existing `EnsureMembersLegacyMappingAsync` fallbacks.

---

## 2026-07-07 - No Contracts/Plugin, Self-Service, or Existing Plugin Changes in This Iteration

### Decision

`Contracts/Plugin/*` (`PluginMemberSummary`, `PluginMemberDetail`, `IPluginDataStore`,
`IMemberExtensionStore`, `ContractVersion`), `Models/SelfServiceProfileViewModel.cs`,
`Services/Core/SelfServiceFeatureService.cs`, and the `plugins/Finance`/`plugins/ServiceBook`
workspaces are not touched by this iteration.

### Rationale

Research confirmed none of these surfaces reference any of the seven fields being replaced today, and
neither existing plugin implements its own fee/main-member concept. Per the established project
policy (`adr/member-extension-data.adr.md`), a `ContractVersion` bump is only warranted when the
`Contracts/Plugin` public surface itself gains or changes members; since this iteration does not touch
that assembly, no version bump is made. Exposing `MembershipType`/metadata to plugins or to
self-service is a distinct, additive concern left to a future iteration if requested.

---

## 2026-07-07 - New Admin Screen and Permission Key for Mitgliedsart Management

### Decision

A new `Controllers/Admin/MembershipTypes_Controller.cs` (`Admin/MembershipTypes` route) and
`Views/Admin/MembershipTypes/Index.cshtml` are added, modeled directly on the existing
`RolePermissionsController`/`Views/Admin/RolePermissions/Index.cshtml` pair (list + `[HttpPost]`
mutation actions, `TempData`-based `ActionFeedbackViewModel`, redirect-after-post). A new core
permission key `members.types.manage` is added to `Services/Authorization/PermissionKeys.cs` and
registered in `CorePermissionsInternal`, gating this screen. The existing `members.manage` permission
continues, unchanged, to gate assigning a type and metadata values to an individual member.

### Rationale

The task requires an admin-configurable definition of Mitgliedsarten and their metadata fields; this
is a distinct, less-frequently-used configuration concern from day-to-day member editing, so it
warrants its own permission key rather than overloading `members.manage`, mirroring how
`RolePermissions` already has its own screen and gate (`admin.access`) separate from
`members.manage`. Reusing the `RolePermissionsController` shape keeps the new admin surface
consistent with the codebase's established CRUD-over-lookup-table pattern instead of introducing a
new one.

---

## 2026-07-07 - Members-Index Type Filter and Badges Made Fully Dynamic (Follow-Up Fix)

### Decision

`Views/Members/Index.cshtml`'s type filter `<select>` and its two inline table badges (unverified and
verified) are converted from a hardcoded 3-way `"privat"/"club"/"company"` vocabulary to a fully
dynamic rendering driven by `ViewData["MembershipTypes"]` (active `MembershipType`s only, ordered by
`SortOrder` then `Name`) and per-member `MembershipType.Key`/`Name`:
- `MembersController.Index` (GET) now calls the pre-existing private helper
  `SetMembershipTypesViewDataAsync` (previously wired only into `Create`/`Edit`) to populate
  `ViewData["MembershipTypes"]`.
- The `<select id="typeFilter">` options are server-rendered from that list (`value="@type.Key"`,
  label `@type.Name`), replacing 4 hardcoded `<option>` tags.
- The local `ResolveMemberTypeFilterKey` 3-way mapping function is removed; `data-member-type` is set
  directly from `member.MembershipType?.Key ?? "unassigned"`.
- The duplicated inline badge markup is extracted into a new shared partial,
  `Views/Members/_MembershipTypeBadge.cshtml`, which mirrors the badge-classification pattern already
  established in `Views/Members/Edit.cshtml` (Firma -> bg-secondary; Verein -> bg-primary; any other
  non-null, non-"Standard" type -> bg-info showing `MembershipType.Name`; null/"Standard" -> bg-light
  "Privat"), so every current and future dynamic type renders a correct, non-empty label without
  further code changes.
- `filterMembers()` client-side JS, `Views/Members/Edit.cshtml`'s own badge, and the dropdown's
  inactive-type exclusion behavior (`SetMembershipTypesViewDataAsync` already filters `IsActive`) are
  all left unchanged.

### Rationale

This is a scoped defect fix, not a new capability: all backing data (`MembershipType.Key/Name/
IsActive/SortOrder`) and services (`IMembershipTypeService.GetAllAsync`, already injected into
`MembersController`, already whitelisted in `ControllerThinnessTests.cs`) were established by the
original `membership-type-metadata` iteration but never wired into the Index view, which is why
"Familie" and any admin-created type are invisible in the list filter and mislabeled "Privat" in the
badge today. Reusing `SetMembershipTypesViewDataAsync` (rather than adding a new helper or a new
Index-specific ViewModel) avoids duplicating the already-correct "active types only, `SortOrder`-then-
`Name`" logic used by `Create`/`Edit`. Mirroring `Edit.cshtml`'s existing dynamic-fallback badge
pattern (rather than inventing a new, uniform badge style) keeps the two Members views visually and
behaviorally consistent, per the project's established design-pattern-adherence expectation, while
still satisfying the Confirmed Input that every dynamic type must show a correct label. Switching the
filter's value space from the fixed `all/privat/club/company` set to `all/<Key>` is confirmed
non-breaking because type filtering is pure client-side state — no query string parameter or
persisted URL depends on the old values, and `filterMembers()`'s comparison logic is already a
value-agnostic string equality check.

