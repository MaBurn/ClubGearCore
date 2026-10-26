# Backlog consolidation inbox

Audience: Maintainers consolidating ClubGear planning material
Scope: Provenance snapshots of backlog-like sources consolidated into the canonical backlog
Collected: 2026-10-02
Consolidated: 2026-10-02

## Purpose

This directory preserves the distributed planning sources that were reviewed during consolidation.
Files below are immutable provenance snapshots, not a second authoritative backlog. Every source row
now has a final disposition; open work is defined only by the canonical item files in the parent
directory:

- [`../core-platform.md`](../core-platform.md)
- [`../core-capabilities.md`](../core-capabilities.md)
- [`../plugins.md`](../plugins.md)

Sequencing remains in [`../domains.md`](../domains.md), [`../next-runs.md`](../next-runs.md) and the
prioritised graphical [`../ROADMAP.md`](../ROADMAP.md).

The collected backlog and TODO sources have been removed from their former locations so backlog material has one physical home under `docs/backlog/`. The table preserves each former path for provenance. Architecture, security and CRISPY workflow records are not backlog files and remain in their purpose-specific locations.

## Inventory

| Area | Snapshot | Original source | State / consolidation note |
|---|---|---|---|
| Legacy plugin backlog | [`legacy-feature-lists/2026-06-plugin-backlog.md`](legacy-feature-lists/2026-06-plugin-backlog.md) | `.backlog/backlog.md` | Explicitly retired by [`../README.md`](../README.md); retain for cross-checking only. |
| Legacy plugin run list | [`legacy-feature-lists/2026-06-plugin-crispy-runs.md`](legacy-feature-lists/2026-06-plugin-crispy-runs.md) | `.backlog/crispyCommand.md` | Derived from the retired June backlog; likely stale. |
| Legacy target vision | [`legacy-feature-lists/2026-06-target-vision.md`](legacy-feature-lists/2026-06-target-vision.md) | `.backlog/zusammenfassung.md` | Context rather than item authority. |
| Install assistant | [`install/2026-07-install-assistant.md`](install/2026-07-install-assistant.md) | `.backlog/install.md` | **Merged into CORE-25**; snapshot retained for acceptance detail. |
| Clubmanager feature gap | [`legacy-feature-lists/2026-08-clubmanager-feature-gap.md`](legacy-feature-lists/2026-08-clubmanager-feature-gap.md) | `tmp/TodoFeatures.md` | Explicitly retired; mapped in the canonical backlog. |
| Prioritized feature specifications | [`legacy-feature-lists/`](legacy-feature-lists/) (`P0-*`, `P1-*`) | `tmp/P0-*.md`, `tmp/P1-*.md` | **Merged** into their canonical CORE/INV/FIN/NEWS items; retained as acceptance-detail snapshots. |
| Finance next steps | [`plugin-finance/next-steps.md`](plugin-finance/next-steps.md) | formerly `plugins/Finance/nextSteps.md` | **Partly stale, residue merged into FIN-01**: payment capture, club accounts, fee demands and pain.008 shipped; imports/matching, reports/budget and role split remain. |
| Finance CRISPY runs | [`plugin-finance/crispy-runs.md`](plugin-finance/crispy-runs.md) | formerly `plugins/Finance/nextStepsCrispy.md` | **Superseded derived plan**; only FIN-01's canonical residue may be scheduled. |
| CarInfo task | [`plugin-carinfo/task.md`](plugin-carinfo/task.md) | `plugins/CarInfo/.crispy/01_task.md` | **Implemented; release residue merged into CAR-02.** |
| CarInfo implementation plan | [`plugin-carinfo/plan.md`](plugin-carinfo/plan.md) | `plugins/CarInfo/.crispy/06_plan.md` | **Superseded workflow plan**; unchecked boxes are not open work. |
| ServiceBook task | [`plugin-servicebook/task.md`](plugin-servicebook/task.md) | `plugins/ServiceBook/.crispy/01_task.md` | **Delivered**: grouped edit card and versioned CarInfo dependency are present at HEAD. |
| ServiceBook implementation plan | [`plugin-servicebook/plan.md`](plugin-servicebook/plan.md) | `plugins/ServiceBook/.crispy/06_plan.md` | **Superseded workflow plan**; code, tests and ServiceBook history verify delivery. |
| Last core workflow task | [`workflow-artifacts/last-core-task.md`](workflow-artifacts/last-core-task.md) | `.crispy/01_task.md` | Already marked complete in the canonical backlog; retained only to expose the distributed source. |
| Last core workflow plan | [`workflow-artifacts/last-core-plan.md`](workflow-artifacts/last-core-plan.md) | `.crispy/06_plan.md` | Derived plan for the completed core task; unchecked boxes are not open-item evidence. |

## Sources indexed, not copied

These files contain open work or findings but are durable architecture/security records rather than standalone todo lists. They remain at their original paths and should be reviewed during consolidation:

- [`../../architecture/proposal-2026-08/DECISIONS.md`](../../architecture/proposal-2026-08/DECISIONS.md), especially **Still open**.
- [`../../architecture/proposal-2026-08/research/01-clubmanager-inventory.md`](../../architecture/proposal-2026-08/research/01-clubmanager-inventory.md); its retained missing features are now CORE-26 and BDAY-01.
- `0_SecReview/ClubGear_2026-08-05_174559/00_SUMMARY.md` at the repository root. It is a historical scan report with unresolved-looking findings and possible sensitive local-path metadata; classify and sanitize findings rather than copying the report into tracked documentation.
- Inline TODO text under `docs/architecture/proposal-2026-08/` describes legacy Clubmanager code and is research evidence, not an implementation TODO in ClubGear.

## Consolidation result

The review is complete. It added CORE-25 (first-run installer), CORE-26 (local-unit radius search),
CORE-27 (authoring guide and project template) and BDAY-01 (birthday/anniversary automation), and it
reconciled the Finance, CarInfo and ServiceBook snapshots against current code and releases. The
remaining product order is rendered in [`../ROADMAP.md`](../ROADMAP.md).

Keep this directory as provenance until the consolidation commit is safely in history. A later
cleanup may remove the snapshots without losing open work; no canonical item depends on a checkbox
or status inside them.
