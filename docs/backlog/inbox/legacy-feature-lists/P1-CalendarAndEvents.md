# Calendar and Events

**Priority:** P1  
**Target:** Core event model plus optional connector plugin  
**Estimated effort:** L for Core; M for the CalDAV connector

## Objective

Provide a central club event model for events, recurring meetings, exceptions, and registrations,
while keeping Nextcloud and CalDAV synchronization outside Core.

## Core functional requirements

1. Authorized users can create, edit, cancel, archive, and view events.
2. Events include title, description, location, start/end, time zone, all-day status, organizer,
   organization unit, visibility, capacity, and registration policy.
3. Recurring meetings use an explicit recurrence model and support per-instance cancellation,
   rescheduling, or overrides.
4. Date calculations preserve the source time zone and behave correctly across daylight-saving
   transitions.
5. Members can register, cancel, and view their status when event policy permits.
6. Capacity limits and waitlists are updated transactionally under concurrent requests.
7. Event and registration changes can emit semantic notification events.
8. Permissions support global, unit-scoped, organizer, member, and public visibility rules.
9. Calendar queries support date range, unit, event type, and registration-state filters.
10. Core exposes provider-neutral import/export ports with stable external IDs and sync metadata.

## Connector requirements

1. A separate CalDAV/Nextcloud plugin manages credentials, discovery, calendar mappings, and sync.
2. Sync is incremental, retryable, idempotent, and records per-item failures.
3. Conflict policy and source-of-truth direction are explicit and configurable.
4. Manual connection test, resync, and mapping diagnostics are available to administrators.

## Dependencies

- Organization Structure, Notification Inbox, permissions, audit, and background jobs.
- Secure secret storage is required before production connector use.

## Out of scope

- Nextcloud-specific entities or credentials in Core.
- Meeting minutes or decision-tree sessions.
- Generic room/resource booking in the first release.

## Acceptance criteria

- Tests cover recurrence, exceptions, DST boundaries, capacity races, waitlists, and unit access.
- Deactivating the connector leaves Core events usable.
- Repeating the same external sync does not duplicate events.
- A connector failure cannot corrupt unrelated Core events.

