# Organization Structure and Subdivisions

**Priority:** P0  
**Target:** ClubGear Core  
**Estimated effort:** L (13–25 person-days)

## Objective

Provide one authoritative hierarchy for club divisions, departments, chapters, committees, and
similar organizational units. Membership, delegated administration, permissions, events, and
plugins must reference this model instead of defining competing organization structures.

## Functional requirements

1. Administrators can create, edit, archive, restore, reorder, and move organizational units.
2. A unit has a stable ID, name, optional short name, description, type, parent, sort order, and
   active/archived state.
3. The hierarchy rejects cycles, self-parenting, and references to missing or archived parents.
4. Authorized users can assign and remove members with an effective date and optional role.
5. A member can belong to multiple units; duplicate active assignments are rejected.
6. Units support designated contacts and delegated administrators without granting global admin.
7. Members can request to join or leave units when the unit policy permits self-service.
8. Join/leave policies support immediate execution or approval by a delegated unit administrator.
9. Permission checks can be scoped to one unit and optionally inherited by descendants.
10. Core pages provide a searchable tree, unit details, member lists, and administration forms.
11. A stable read API exposes units and authorized memberships to plugins without exposing EF
    entities or unrestricted personal data.
12. Every structural, membership, and delegation change is written to the audit log.

## Authorization requirements

- Global administrators manage the complete hierarchy.
- Delegated administrators act only within their assigned scope.
- Read access, membership management, structural management, and delegation use separate
  permission keys.
- Archived units remain auditable but cannot receive new members or children.

## Dependencies

- Existing member, role/permission, audit, and self-service services.
- Required by Calendar and Events, Inventory 2.0, editorial modules, and unit-scoped notifications.

## Out of scope

- Hard-coded department pages or names.
- Synchronizing Keycloak groups as the local authorization source.
- Calendar, inventory, finance, or editorial business data.

## Acceptance criteria

- Tests cover tree creation, moving, cycle rejection, archival, delegated access, and inheritance.
- A delegated administrator cannot modify a sibling, ancestor, or unrelated unit.
- Plugins can resolve an authorized unit reference through a versioned contract.
- All mutations produce actor, timestamp, target, and before/after audit information.

