# ADR 0001: AI-Readable Plugin Development Guardrails

Date: 2026-06-09

Status: Accepted

## Purpose

This ADR is an execution policy for AI coding agents and human contributors working on ClubGear plugins.

The policy exists to keep ClubGear as a stable club-management core with a controlled plugin ecosystem. Plugins may extend ClubGear, but plugin-specific behavior must not leak into the core application.

## Normative Keywords

The keywords `MUST`, `MUST NOT`, `SHOULD`, `SHOULD NOT`, and `MAY` are normative.

If this ADR conflicts with a task request, this ADR wins unless the user explicitly overrides it for that task.

## Definitions

`Core code` means application code owned by ClubGear itself, including controllers, views, services, data models, migrations, Identity, permissions, audit, notifications, plugin runtime, plugin contracts, host facades, and shared UI rendering.

`Plugin code` means code, manifest files, migrations, assets, tests, and documentation belonging to one concrete plugin.

`Plugin-specific behavior` means behavior that exists only for one named plugin or one concrete business case, such as `CarInfo`, `Inventory`, `GpsTracker`, or `ClubMagazine`.

`Generic plugin capability` means a reusable host feature that can be used by multiple plugins or by a clearly defined plugin category.

`Plugin category` means one of:

- `member-profile`
- `general-extension`
- `technical`

`User approval` means an explicit confirmation from the user before implementing a core-code change.

## Mandatory AI Workflow

For every plugin-related task, an AI agent MUST follow this workflow before editing files.

### Step 1: Classify The Task

Classify the task as exactly one primary type:

- `plugin-only`: changes stay inside one concrete plugin
- `generic-core`: changes create reusable plugin infrastructure in the core
- `mixed`: changes affect both plugin code and core code
- `unclear`: the required change location is not yet clear

If the task is `unclear`, the AI agent MUST inspect the repository before deciding.

### Step 2: Identify The Plugin Category

Classify the affected plugin capability as one primary category:

- `member-profile`
- `general-extension`
- `technical`
- `cross-category`
- `unknown`

If the category is `unknown`, the AI agent MUST avoid core edits until the category is clear.

### Step 3: Detect Core-Code Changes

Before editing, the AI agent MUST decide whether the task requires changing core code.

If no core-code change is required, continue with plugin-only implementation.

If any core-code change is required, continue to Step 4.

### Step 4: Ask For Approval Before Core Edits

The AI agent MUST ask the user for approval before implementing any core-code change.

The AI agent MUST NOT make the core-code edit first and explain it afterward.

Use this approval template:

```text
I need approval before changing ClubGear core code.

Proposed core change:
- Files/areas:
- Why this cannot stay inside the plugin:
- Generic capability created:
- Reused by plugin categories:
- Risks/migrations:
- Tests I will add/update:

Do you approve this core change?
```

If the user approves, continue.

If the user does not approve, the AI agent MUST either keep the implementation plugin-only or stop and report the blocker.

### Step 5: Implement With Boundary Checks

During implementation, the AI agent MUST keep plugin-specific behavior inside plugin code.

After implementation, the AI agent MUST verify the relevant checklist in this ADR.

## Hard Rules

### GR-001: No Plugin-Specific Core Logic

Core code MUST NOT contain logic for one named plugin.

Forbidden examples:

- `if plugin.Name == "CarInfo"`
- `if moduleId == "Inventory"`
- adding `CarPlateNumber` to a core member table
- rendering a `GpsTracker` section from a core member view
- adding a core permission that only makes sense for one concrete plugin

Allowed only after user approval:

- generic member-extension data APIs
- generic plugin navigation contributions
- generic plugin list/detail rendering
- generic dashboard widget contributions
- generic technical-provider contracts
- generic plugin configuration storage

### GR-002: Core Changes Must Be Generic And Approved

Any core-code change for plugin work MUST be both:

- generic for multiple plugins or a defined plugin category
- approved by the user before implementation

This includes changes to:

- plugin contracts
- manifest parsing or validation
- plugin lifecycle behavior
- host facades
- permissions
- audit
- notifications
- persistence
- migrations
- Identity or login flows
- shared plugin UI rendering
- architecture tests for plugin boundaries

### GR-003: Plugins Use Contracts And Facades Only

Plugins MUST interact with ClubGear through public plugin contracts and host facades.

Plugins MUST NOT:

- reference internal core services directly
- depend on undocumented core database tables
- bypass permission checks
- write directly to core audit storage
- call core notification infrastructure directly
- rely on MVC internals that are not part of the plugin contract

### GR-004: Plugin Data Is Isolated

Plugin-owned data MUST stay isolated from core-owned data.

Plugin data MUST use:

- plugin-owned tables, or
- an approved generic plugin data abstraction

Plugin tables SHOULD use clear plugin-specific prefixes or namespaces.

Member-related plugin data MUST link to members through approved generic member-extension mechanisms.

Plugin migrations MUST be tracked separately from core migrations.

Plugin work MUST NOT add plugin-specific fields to core member, role, user, or settings tables.

### GR-005: Disable, Uninstall, And Delete Are Separate

Disabling a plugin MUST keep plugin data.

Disabling a plugin SHOULD remove or deactivate:

- plugin UI
- plugin routes
- plugin jobs
- plugin technical providers

Uninstalling a plugin MUST NOT silently delete plugin data.

Deleting plugin data MUST be explicit and auditable.

### GR-006: Permissions Are Mandatory

Every plugin contribution exposing data or actions MUST declare and enforce permissions.

Required permission boundaries:

- member-profile read
- member-profile write
- admin actions
- navigation visibility
- list/detail access
- import/export
- technical provider configuration
- technical provider execution or testing

Self-service read and write permissions MUST be separate.

### GR-007: Technical Plugins Require Stronger Controls

Technical plugins MUST be treated as higher risk.

Technical plugins MUST declare:

- provider type
- required permissions
- configuration schema
- secret fields, if any
- operational risks, if any

Secrets MUST be stored through an approved configuration facade.

Secrets MUST be masked in UI output.

Technical plugin failures MUST NOT block:

- core audit logging
- login fallback
- unrelated notification channels
- unrelated audit sinks
- unrelated background jobs

### GR-008: Host Controls Plugin UI Rendering

Plugins SHOULD provide structured metadata, schemas, commands, and data.

The host SHOULD render:

- member cards
- edit tabs
- navigation entries
- list views
- detail views
- dashboard widgets
- admin panels

Raw plugin HTML MUST NOT be trusted by default.

Validation errors SHOULD be displayed per field.

### GR-009: Backward Compatibility Is Explicit

Plugin contract changes SHOULD be additive.

If compatibility is broken, the AI agent MUST document:

- what breaks
- why it is necessary
- migration path
- affected plugins
- tests updated

Existing plugin manifests SHOULD remain loadable unless the user approves a breaking change.

Signature and hash verification MUST remain intact.

### GR-010: Tests Protect Plugin Boundaries

Approved generic plugin capabilities MUST include tests.

Expected test areas:

- manifest parsing
- manifest validation
- permission enforcement
- plugin isolation from core internals
- lifecycle behavior
- migration behavior
- UI contribution rendering
- technical provider failure handling
- backward compatibility

Architecture tests SHOULD block accidental direct references from plugin code into core internals.

## Decision Tree

Use this decision tree for every plugin-related change:

```text
1. Does the requested behavior apply to exactly one concrete plugin?
   - yes: implement inside that plugin. Do not change core.
   - no: continue.

2. Does the requested behavior require host/runtime/contracts/shared UI changes?
   - no: implement inside plugin code.
   - yes: continue.

3. Is the required host change reusable by multiple plugins or a defined plugin category?
   - no: do not implement as a core change.
   - yes: continue.

4. Has the user explicitly approved the core change?
   - no: ask for approval using the approval template.
   - yes: implement the generic core change with tests.
```

## Pre-Edit Checklist

Before editing files for plugin work, answer these questions internally:

- Task classification: `plugin-only`, `generic-core`, `mixed`, or `unclear`
- Plugin category: `member-profile`, `general-extension`, `technical`, `cross-category`, or `unknown`
- Will core code change?
- If core code changes, has the user approved it?
- Which guardrail IDs apply?
- Which tests are needed?

If core code changes and approval is missing, stop before editing core code.

## Post-Implementation Checklist

After implementation, verify:

- no plugin-specific core branches were added
- plugin data remains isolated
- permissions are declared and enforced
- lifecycle behavior is safe
- technical plugin failures are isolated, if relevant
- backward compatibility is documented, if relevant
- tests were added or updated for changed boundaries

## Final Response Requirements For AI Agents

When reporting completion of plugin-related work, the AI agent SHOULD include:

- task classification
- whether core code was changed
- whether user approval was required and obtained
- guardrails applied
- tests run or not run

Keep the response concise.

## Examples

### Example A: Add Vehicles To Member Profile

Classification: `plugin-only` if implemented entirely in `CarInfo`.

Core change: not allowed unless a generic member-extension API is needed.

Required action if generic API is needed: ask user approval before changing core.

### Example B: Add Navigation For Inventory Plugin

Classification: `generic-core` or `mixed`.

Core change: generic plugin navigation contribution.

Required action: ask user approval before changing contracts, runtime collection, layout rendering, or tests.

### Example C: Add Matrix Notification Plugin

Classification: `mixed`.

Category: `technical`.

Core change: generic notification-channel provider contract and configuration facade.

Required action: ask user approval before core changes. Enforce secrets, audit, permissions, and failure isolation.

## Consequences

This policy intentionally slows down plugin work that touches the core.

The benefit is that ClubGear avoids plugin-specific shortcuts and keeps a stable, reusable plugin platform.
