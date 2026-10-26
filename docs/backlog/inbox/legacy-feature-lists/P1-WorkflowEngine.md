# Workflow and State Machine Foundation

**Priority:** P1  
**Target:** ClubGear Core  
**Estimated effort:** L (13–25 person-days)

## Objective

Provide a reusable state-machine foundation for approval and lifecycle processes without embedding
rental, purchasing, editorial, or other domain-specific workflows in Core.

## Functional requirements

1. A workflow definition has a stable namespaced key, owner, version, states, initial state, terminal
   states, and allowed transitions.
2. Each transition declares source/target states, required permission, validation hook, and optional
   side-effect events.
3. Definitions are immutable after use; modifications create a new version.
4. Workflow instances reference a definition version, owner resource, current state, row version,
   creation time, and completion/cancellation time.
5. Commands reject invalid transitions and stale concurrent updates.
6. Every attempted and successful transition records actor, time, source, target, reason, and
   correlation/idempotency key.
7. Plugins register namespaced definitions and execute transitions through a contract facade.
8. Side effects use an outbox or equivalent retry-safe mechanism and do not partially commit state.
9. Background transitions support cancellation, retry limits, and visible failure state.
10. Administrators can inspect definitions and histories but cannot bypass domain validation unless
    an explicit audited override capability exists.
11. Deactivating a plugin prevents new commands but retains readable instance history.
12. Queries support owner resource, workflow key, state, assignee where applicable, and date range.

## Dependencies

- Permissions, audit, plugin migrations/contracts, background jobs, and notifications.
- Consumers include Rental Marketplace and potentially editorial or membership approvals.

## Out of scope

- A visual BPMN engine or arbitrary script execution.
- Hard-coded rental or purchase seed workflows in Core.
- Domain-specific forms, calculations, or assignment policies.

## Acceptance criteria

- Tests cover invalid transitions, authorization, version pinning, concurrency, idempotency, and
  retry-safe side effects.
- A reference plugin registers and runs a workflow without Core domain changes.
- Historical instances remain interpretable after a definition upgrade or plugin deactivation.
- No side effect is emitted for a rolled-back transition.

