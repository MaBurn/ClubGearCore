# ADR: Sub-Member Hierarchy

## Purpose

Persistent architectural decisions for the submember-hierarchy iteration.

Append new entries. Do not rewrite prior history.

---

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

---

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

---

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

---

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
