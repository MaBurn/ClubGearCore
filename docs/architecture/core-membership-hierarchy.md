# Core Membership Hierarchy

Audience: developers, maintainers, operators
Scope: membership types, member-reference metadata, sub-member grouping, migration and UI behavior
Last-Validated: 2026-07-09

## Purpose

This document describes the July 2026 core change that turns ClubGear's member model from a mostly flat list into a typed, hierarchy-aware member overview.

The hierarchy is generic core behavior. It is not tied to one plugin or one hard-coded business case. Administrators configure which membership types may act as containers, and the member overview derives parent/sub-member rows from existing `MemberReference` metadata values.

## Summary

The core now supports:

- Configurable membership types that can allow sub-members.
- A per-container label for nested rows, for example `Firma` -> `Mitarbeiter`.
- A hierarchy-aware `/Members` overview where a parent is followed by its sub-members.
- Group-aware client filtering so search and type filters keep parent and sub-members visible together.
- Server-side integrity checks for `MemberReference` metadata to prevent self-references, grandchildren and short cycles.
- Search-as-you-type member reference fields instead of raw member-id inputs.

## Data Model

`MembershipType` gained two properties:

| Property | Type | Purpose |
|---|---|---|
| `AllowsSubMembers` | `bool` | Marks a membership type as a valid parent/container type. |
| `SubMemberLabel` | `string?` | Label shown on nested rows, such as `Mitarbeiter`, `Familienmitglied` or `Mitglied`. |

The SQLite schema is upgraded by `Data/Migrations/202607080101_AddSubMemberHierarchy.cs`.

The migration:

- Adds `AllowsSubMembers INTEGER NOT NULL DEFAULT 0`.
- Adds `SubMemberLabel TEXT NULL`.
- Backfills seeded system types idempotently:
  - `Familie` -> `Familienmitglied`
  - `Firma` -> `Mitarbeiter`
  - `Verein` -> `Mitglied`
- Leaves `Standard` as a non-container type.
- Does not drop or rebuild existing tables.

The migration is registered in `ApplicationSeeder.EnsureSqliteSchemaCompatibilityAsync`, matching the project's startup-patching approach rather than EF migration files.

## Hierarchy Derivation

No new relationship table was introduced.

A member is treated as a sub-member when:

1. It has a metadata value whose field type is `MemberReference`.
2. The referenced target member exists in the currently loaded member set.
3. The target member's `MembershipType.AllowsSubMembers` is `true`.

If a member has multiple member-reference metadata values, the first valid container target by `MembershipTypeField.SortOrder` wins.

The hierarchy is built by `MemberHierarchyViewModel.FromMembers` as a flattened row list:

- Parent/container rows are emitted at depth `0`.
- Sub-member rows are emitted at depth `1`.
- Every member is rendered exactly once.
- Orphaned sub-members whose parent is absent from the current status-filtered set are emitted as depth `0` rows instead of disappearing.

## Members Overview

`MembersController.Index` now loads the full active member set with `GetListAsync(search: null)` and applies only the status filter server-side.

The search term is passed back to the view for prefilling the search box. Actual search and type narrowing happen in `wwwroot/js/member-index-filter.js`.

This keeps groups coherent:

- A search hit on a child shows the parent and all members in that group.
- A search hit on a parent shows its sub-members too.
- Filtering by a container type shows the container and its sub-members, even if the sub-members have a different own type.
- Filtering by a sub-member's own type shows the whole group containing that sub-member.

The rendered rows and mobile cards expose:

- `data-group-id`
- `data-group-type`
- `data-member-type`
- `data-depth`

These attributes are the stable contract for the group-aware client filter.

## Reference Integrity

`MemberMetadataService.ValidateAndEncode` accepts an optional `MemberReferenceIntegrityContext`.

When present, `MemberReference` fields reject:

- A member referencing itself.
- A member referencing a target that is already a sub-member.
- A member that already has sub-members being assigned to a parent.

Together, these checks enforce a single hierarchy level and prevent 2-cycles without requiring a full graph traversal for each save.

Validation errors use the existing `ValidationException` path and are surfaced back to the member edit UI.

## Reference Picker

Member-reference metadata fields no longer require users to type raw database ids.

The core provides:

- `GET /api/member/reference-search?q=...`
- `IMemberFeatureService.SearchForReferenceAsync`
- `IMemberFeatureService.GetReferenceLabelsAsync`

The shared `wwwroot/js/site.js` behavior turns `MemberReference` fields into search-as-you-type pickers. Existing saved references are rendered as human-readable labels in member details and edit forms.

## Operational Notes

The feature is startup-migrated and safe to apply repeatedly. Existing databases receive new nullable/defaulted columns and seeded default container settings without destructive schema changes.

Because the member overview now loads the full active member set before client-side group filtering, operators should keep an eye on very large clubs. The current design is appropriate for typical club sizes and preserves the group behavior that server-side narrowing could not provide.

## Test Coverage

Focused coverage exists for:

- Schema migration and default values.
- Membership type create/update persistence.
- Hierarchy ordering, grouping, labels and orphan safety.
- Reference-integrity validation.
- Members index rendering attributes.
- Group-aware filter behavior through the extracted JavaScript decision core.

Relevant test files include:

- `tests/ClubGear.ArchitectureTests/MemberHierarchyTests.cs`
- `tests/ClubGear.ArchitectureTests/MemberIndexGroupFilterTests.cs`
- `tests/ClubGear.ArchitectureTests/MemberMetadataServiceTests.cs`
- `tests/ClubGear.ArchitectureTests/MembershipTypeSchemaTests.cs`
- `tests/ClubGear.ArchitectureTests/MembershipTypeServiceTests.cs`
