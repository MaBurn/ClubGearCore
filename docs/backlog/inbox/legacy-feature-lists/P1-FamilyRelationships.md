# Family and Household Relationships

**Priority:** P1  
**Target:** ClubGear Core  
**Estimated effort:** M (6–12 person-days)

## Objective

Represent explicit, validated relationships between primary members and related family or household
members for administration, self-service, communication, and plugin consumption.

## Functional requirements

1. Authorized administrators can create, change, end, and view member relationships.
2. A relationship has a primary member, related member, type, effective dates, status, and optional
   administrative note.
3. Supported relationship types are configured centrally and have clear directionality rules.
4. The model rejects self-links, duplicate active links, cycles where a hierarchy is required, and
   impossible effective-date ranges.
5. A member may have at most one active primary household relationship when policy requires it.
6. Member details and lists show the relationship hierarchy without duplicating member records.
7. Membership-type changes validate any family constraints before saving.
8. Self-service exposes only relationships the current user is authorized to see and manage.
9. Plugins receive a minimal read model for fee or communication decisions, not unrestricted member
   entities.
10. Relationship changes emit audit events and optional semantic notifications.
11. Ending or deleting a member does not silently delete relationship history.
12. Import/migration tooling reports ambiguous or cyclic Clubmanager family data for manual review.

## Privacy and authorization requirements

- Relationship visibility does not automatically grant access to another adult's private profile.
- Child/dependent access requires an explicit policy and identity link.
- Notes and relationship data follow member-data retention rules.

## Dependencies

- Existing member, membership-type, self-service, permission, and audit services.
- Finance consumes a read contract but remains responsible for fee calculations.

## Out of scope

- Shared login credentials.
- Automatic legal guardianship assumptions.
- Contribution or discount calculations in Core.

## Acceptance criteria

- Tests cover cycles, duplicate links, effective dates, privacy, and membership-type validation.
- Finance can read an authorized relationship snapshot without querying Core tables.
- Removing a current relationship preserves a dated, auditable history.
- Existing member list/detail behavior remains correct for members without relationships.

