# ADR: Core Member Management

## Purpose

Persistent architectural decisions for core member management, including dynamic membership types, member metadata, member-number generation and hierarchy-aware sub-member rendering.

## Decision History Summary

- Membership type metadata decisions keep semantic role information in core data while exposing only stable plugin-facing contracts.
- Member number decisions prefer deterministic auto-fill behavior that is visible to administrators and does not silently overwrite intentional input.
- Submember hierarchy decisions define parent-child membership behavior as a core model concern, while plugin contract changes remain limited to cases where the public contract surface actually changes.
- Contract version decisions treat implementation-only model or UI changes differently from shared `ClubGear.Plugin.Contracts` changes, so decision history records when a version bump is required and when it is not.

## Consolidation

This ADR consolidates the previous iteration-scoped ADR files listed below. The dated decision entries are preserved under source sections so the decision history remains append-only and traceable.

Sources:

- `membership-type-metadata.adr.md`
- `member-number-autofill.adr.md`
- `submember-hierarchy.adr.md`

---

## Source: Membership Type Metadata

Former file: `membership-type-metadata.adr.md`


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


## 2026-07-07 - No Contracts/Plugin, Self-Service, or Existing Plugin Changes in This Iteration

### Decision

`Contracts/Plugin/*` (`PluginMemberSummary`, `PluginMemberDetail`, `IPluginDataStore`,
`IMemberExtensionStore`, `ContractVersion`), `Models/SelfServiceProfileViewModel.cs`,
`Services/Core/SelfServiceFeatureService.cs`, and the `plugins/Finance`/`plugins/ServiceBook`
workspaces are not touched by this iteration.

### Rationale

Research confirmed none of these surfaces reference any of the seven fields being replaced today, and
neither existing plugin implements its own fee/main-member concept. Per the established project
policy (`ADRs/consolidated/plugin-platform.adr.md`), a `ContractVersion` bump is only warranted when the
`Contracts/Plugin` public surface itself gains or changes members; since this iteration does not touch
that assembly, no version bump is made. Exposing `MembershipType`/metadata to plugins or to
self-service is a distinct, additive concern left to a future iteration if requested.


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



## Source: Member Number Autofill

Former file: `member-number-autofill.adr.md`


## 2026-07-08 - Remove Model-Level [Required] on Member.MemberNumber; Restore Required-on-Edit Explicitly

### Decision

`[Required]` is removed from `Member.MemberNumber` (`Models/Member.cs`). This is the root cause of the
reported defect: `Create.cshtml`/`_MemberForm.cshtml` render `asp-for="MemberNumber"`, which pulls
`data-val-required` from that DataAnnotation, so jquery.validate.unobtrusive blocks a blank Create
submission client-side before the already-implemented server-side auto-numbering
(`MemberFeatureService.GenerateNextMemberNumberAsync`, called from `CreateAsync` when the field is blank)
ever gets a chance to run.

Because `Views/Members/Edit.cshtml` renders `asp-for="MemberNumber"` against the same model (it does not
reuse `_MemberForm.cshtml`), removing the annotation would also silently make Edit's Mitgliedsnummer
optional, both client- and server-side, with `MemberFeatureService.UpdateAsync` performing an unconditional
overwrite (`tracked.MemberNumber = member.MemberNumber;`) and no auto-generation fallback. Required-on-Edit
behavior is therefore restored by hand at every layer that previously relied on the DataAnnotation instead
of removing the model-wide annotation:
- `Views/Members/Edit.cshtml`: hand-authored `data-val="true" data-val-required="..."` attributes added
  directly on the `MemberNumber` `<input>`, preserving client-side blocking on Edit.
- `Controllers/Member/Member_Controller.cs`, `Edit` (POST): explicit `ModelState.AddModelError` when the
  posted value is blank, restoring server-side enforcement.
- `Services/Core/MemberFeatureService.cs`, `UpdateAsync`: the previously-unconditional assignment becomes
  conditional (only overwrite when the incoming value is non-blank), as a defense-in-depth guard at the
  service boundary against any caller that bypasses the controller's check.

The now-dead `ModelState.Remove(nameof(Member.MemberNumber))` workaround in `Create` (POST) is removed,
since `ModelState.IsValid` is already true for a blank value once `[Required]` is gone.

### Rationale

`[Required]` is annotation on a single shared model consumed by two different views with two different
business rules (optional-with-autofill on Create, mandatory on Edit). Keeping the annotation would have
required either duplicating the model for two nearly-identical forms, or accepting the reported bug as a
permanent limitation. Removing the annotation and re-establishing "required" explicitly, per-surface,
exactly where it is actually needed (Edit's view markup, Edit's controller action, and the service's write
path) is the smallest change that fixes Create without silently relaxing Edit — every place that could let
a blank number reach persistence via Edit was enumerated in research and is closed here. EF Core's column
nullability is driven by the C# nullable-reference-type annotation on the property, not by `[Required]`, so
no database migration is needed: the column remains `NOT NULL`, and `CreateAsync` already guarantees a
non-blank value (typed or generated) before every `SaveChangesAsync`.


## 2026-07-08 - Bounded Retry-on-Conflict for Concurrent Auto-Number Generation

### Decision

`MemberFeatureService.CreateAsync` wraps its auto-generate-and-insert sequence in a bounded retry loop
(`MaxMemberNumberGenerationAttempts = 3`) that catches `Microsoft.EntityFrameworkCore.DbUpdateException`
around `SaveChangesAsync`, re-invokes `GenerateNextMemberNumberAsync` for a fresh candidate, and retries.
If all attempts are exhausted, a `ClubGear.Services.BusinessLogicException` is thrown with a German,
user-facing message, which `GlobalExceptionMiddleware` already renders as a friendly feedback banner.

### Rationale

Research confirmed `GenerateNextMemberNumberAsync`'s existing collision check (`AnyAsync` against
`_db.Members`) and the counter increment (`UpsertSystemConfigAsync`, staged in memory only) are not wrapped
in any transaction or lock; the only durable commit point is the single `SaveChangesAsync` at the end of
`CreateAsync`, which is protected by the pre-existing unique index on `Members.MemberNumber`
(`Data/ApplicationDbContext.cs` lines 58-60). Before this iteration, a blank `MemberNumber` almost never
reached the server (client-side validation blocked it), so this race was latent and effectively unreachable
in practice. Making blank-and-autofill the normal, supported Create flow (this iteration's whole purpose)
makes concurrent double-submits of blank Mitgliedsnummer a realistic, not merely theoretical, scenario, so
shipping the autofill feature without also closing this gap would trade one bug (client-side block) for
another (occasional `DbUpdateException` surfaced as a raw 500 to the admin). A bounded catch-and-retry is
correct here without introducing locks or transactions: a `DbUpdateException` on this exact insert can only
mean a concurrent request committed the same candidate first, so a fresh `GenerateNextMemberNumberAsync`
call — which re-queries `_db.Members` from the database — is guaranteed to see that newly-committed number
and skip past it on the very next attempt.

### Explicitly Out of Scope

`AdminController.SaveConfigEntry` (`Controllers/Admin/Admin_Controller.cs` lines 102-115) is a single
generic save action shared by every `SystemConfig` section (Club data, Members numbering, and any future
section) and performs no validation of individual values (e.g. empty `MemberNumberPrefix`, non-numeric
`MemberNumberNextNumber`, negative `MemberNumberPadding`). Tightening that endpoint is a cross-cutting
change with a blast radius far beyond "Mitgliedsnummer auto-fill on Create" and is not made in this
iteration. The existing `GetConfiguredNumber`/`TryExtractNumber` fallback-and-clamp behavior in
`MemberFeatureService` already prevents any of those bad values from throwing or corrupting data — at worst
they silently reset numbering to a safe default, a separate admin-configuration-hygiene concern left for a
future iteration if requested.


## Source: Sub-Member Hierarchy

Former file: `submember-hierarchy.adr.md`


## 2026-07-08 - Derive the Hierarchy Generically From MemberReference Values Gated by a Container Type Flag

### Decision

The parent/sub-member relationship shown in `/Members` is derived at read time from the existing
`MemberReference` metadata values (no new relationship table). A member X is a sub-member of member
Y when X carries a `MemberReference` value targeting Y **and** `Y.MembershipType.AllowsSubMembers`
is `true`. When X has several such values, it nests under the target with the lowest `Field.SortOrder`.
Two new properties are added to `MembershipType`: `AllowsSubMembers` (bool) and `SubMemberLabel`
(string?), configured on the container/parent type via the existing Mitgliedsart admin screen.

### Rationale

Research confirmed all `MemberReference` processing is already generic (not keyed on
`main_member`/`Familie`) and that the whole eager-loaded member graph is available in memory, so a
parent link can be resolved without hardcoding or extra queries. The confirmed user decision places
the sub-member label on the container/parent type ("mark Firma as allowing sub-members, set label
'Mitarbeiter' there"), which the `AllowsSubMembers` + `SubMemberLabel` pair models directly. Gating
"who can be a parent" on a type-level flag (rather than on the presence of any reference) keeps
unrelated `MemberReference` fields from accidentally creating a hierarchy and makes the container
role a deliberate, admin-configurable choice. Adding a dedicated join table was rejected as it would
duplicate the association the seeded `main_member` field already expresses and would break the
task/Research commitment to reuse the existing mechanism.


## 2026-07-08 - Single-Level Depth Enforced by Server-Side Reference-Integrity Validation

### Decision

`MemberMetadataService.ValidateAndEncode` gains one optional `MemberReferenceIntegrityContext`
parameter (self id, existing sub-member ids, existing parent ids). For each posted
`MemberReference` target P from saving member S it rejects: `P == S` (self-reference), `P` is itself
a sub-member (would create a grandchild), and `S` already being a parent (reverse grandchild / cycle).
`MemberFeatureService.ValidateMetadataAsync` computes the context via lightweight in-memory
projections over `MemberMetadataValues`, only when the type has a `MemberReference` field. Errors
surface through the existing `ValidationException` path into the edit form.

### Rationale

Research found no existing self/cycle/depth guards anywhere, so single-level enforcement is net-new
and the user explicitly chose hard server-side validation. Keeping the checks in the already-stateless
`MemberMetadataService` (pure, id-only context) preserves that service's role and lets the existing
`MemberMetadataServiceTests` keep calling the 3-argument overload unchanged, because the new parameter
is optional. The three checks are the minimal complete set that guarantees a single-level DAG: with no
existing grandchildren, forbidding "point at an existing child" and "reassign an existing parent"
prevents every deeper chain and every 2-cycle without needing a full graph traversal on each save.


## 2026-07-08 - Index Renders a Server-Built Hierarchy and Filters Group-Aware on the Client; No Server Search Narrowing of the Index List

### Decision

`MembersController.Index` loads the full member set (`GetListAsync(search:null)`), applies the
status filter, then a new `IMemberFeatureService.BuildHierarchy` default-interface method produces an
ordered flat row list (each parent immediately followed by its sub-members; sub-members removed from
the top level; orphaned sub-members whose parent is absent are emitted at depth 0). The submitted
`search` value is used only to pre-fill the client search box; it no longer narrows the Index list on
the server. The client `filterMembers()` is rewritten to be group-aware: a group is shown iff any of
its members matches the search term, AND the type filter matches the container type or any member's
own type — keeping the whole group (parent + indented sub-members) visible together.

### Rationale

The confirmed user decision requires group-aware search/filter that keeps a parent and all its
sub-members together and keeps a container's sub-members visible when filtering by the container type.
Server-side SQL narrowing (the current `GetListAsync(search)` path) can drop a parent whose only match
is a child, which is fundamentally incompatible with group cohesion; the app already materializes the
full member set in memory elsewhere (e.g. `SearchForReferenceAsync`), so loading all rows and
filtering group-aware on the client is consistent with existing patterns and removes the
parent-dropped-before-render failure mode entirely. `BuildHierarchy` is a default-interface method
mirroring the existing `BuildListSegments`, so no new `MembersController` constructor dependency is
introduced and the `ControllerThinnessTests` service whitelist stays satisfied.


## 2026-07-08 - Container Flag and Label Added as Idempotent Columns With Seeded Backfill; Existing Layout Preserved

### Decision

`AllowsSubMembers` (INTEGER NOT NULL DEFAULT 0) and `SubMemberLabel` (TEXT NULL) are added to
`MembershipTypes` via a new idempotent migration registered in
`ApplicationSeeder.EnsureSqliteSchemaCompatibilityAsync`, with a startup backfill seeding
`Familie -> 'Familienmitglied'`, `Firma -> 'Mitarbeiter'`, `Verein -> 'Mitglied'` (all
`AllowsSubMembers = 1`) and leaving `Standard` at `0`. No column or table is dropped. All new code
stays in the existing responsibility-based folders (`Models/`, `Services/Abstractions|Core/`,
`Data/Migrations/`, `Views/`); the feature is not repackaged into a feature-folder layout.

### Rationale

This matches the project's documented no-EF-Migrations, never-`DROP COLUMN`, idempotent-startup-
backfill philosophy already codified in the membership-type-metadata ADRs, and the seeded defaults
make the feature immediately usable for the exact Firma/Verein/Familie framing in the task. Preserving
the existing layered layout avoids fighting the enforced architecture tests (controller thinness,
Mitgliedsart CRUD pattern, schema tests) that already govern these surfaces.
