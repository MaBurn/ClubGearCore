# Architecture Decision Records

This directory contains ClubGear's architecture decision records.

## Structure

- `0001-plugin-development-guardrails.md` is the standing execution policy for plugin-related work.
- `0002-ui-style-guide.md` is the standing visual/front-end style guide (palette, tokens, naming, component patterns).
- `consolidated/` contains topic-level ADR histories. These files preserve the dated decision entries that previously lived in the former lowercase `adr/` directory. That directory no longer exists: its last three files were migrated on 2026-09-02, so `ADRs/` is now the only ADR tree.
- `0003-plugin-submodule-hygiene.md` is the standing constraint on how plugin submodules are tracked: no embedded `.git` directories, and effective submodule URLs must resolve to the `.gitmodules` declaration under host+path normalisation.

## Consolidated ADRs

| Topic | File | Former files |
|---|---|---|
| Core member management | `consolidated/core-member-management.adr.md` | `membership-type-metadata`, `member-number-autofill`, `submember-hierarchy` |
| Finance plugin | `consolidated/finance-plugin.adr.md` | `finance-iteration-*`, `finance-audit-frontend`, `finance-bugfix-*`, `payment-capture`, `club-accounts-bookings`, `finance-plugin-packaging` |
| Plugin platform | `consolidated/plugin-platform.adr.md` | plugin categories, extension points, permissions, admin lifecycle, member extension data, navigation, generic pages, filters, grouping, self-service dashboard, technical provider contracts |
| Plugin implementations | `consolidated/plugin-implementations.adr.md` | CarInfo, GPS tracker, Inventar, ServiceBook, dashboard widgets and Vereinszeitung |

## Rule

New decisions should be appended to the matching topic-level ADR in `consolidated/`. Create a new topic-level ADR only when the decision does not fit an existing topic.
