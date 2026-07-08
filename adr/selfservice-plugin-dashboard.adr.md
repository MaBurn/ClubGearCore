# ADR: SelfService Plugin Dashboard

## Purpose

Persistent architectural decisions for the selfservice-plugin-dashboard iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-18 - Plugin DetailCards Wired into SelfService Index Dashboard

### Decision

`SelfServiceController.Index` is extended to call `_memberPluginSlotService.GetSlotsAsync` when a
member is linked, populating `ViewData["MemberPluginSlots"]`, `ViewData["PluginActionEndpoint"]`
(`/api/self-service/plugin-actions`), and `ViewData["PluginSlotMode"]` (`details`) before returning
the view. `Views/SelfService/Index.cshtml` reads the snapshot and renders the existing shared
partial `~/Views/Members/_PluginSlots.cshtml` in `details` mode when `DetailCards.Count > 0`,
inside a new `selfservice-dashboard-plugins` section. A "Profil verwalten" link directs members to
the Profile page for editing. No plugin code, no shared partials, and no architecture tests are
modified.

### Rationale

The SelfService Profile page already provides full plugin editing via `edit-cards` mode. The missing
piece is that the Index dashboard gives no indication that plugin data (vehicles, ServiceBook
entries) exists for the member. The `details` mode of `_PluginSlots.cshtml` renders only
`DetailCards` and `StatusBadges` — the read-only summary surface that is appropriate for a
dashboard. Routing mutations (add/edit/delete) remain on the Profile page where the `edit-cards`
mode and `_PluginActionModal.cshtml` are already wired. This avoids duplicating the action modal
infrastructure on the dashboard and keeps the Index page as a read/navigate-only surface consistent
with its current design intent.

---

## 2026-06-18 - Inline Slot Population in Index, Not via PopulatePluginViewDataAsync

### Decision

The Index action populates plugin ViewData inline rather than reusing the existing private helper
`PopulatePluginViewDataAsync`. The Index action already holds the `SelfServiceDashboardOutcome`
(including `Member`) from its own `GetDashboardAsync` call. Reusing the helper would cause a second
`GetDashboardAsync` call, redundantly fetching the same member record.

### Rationale

`PopulatePluginViewDataAsync` was designed for the Profile GET and POST actions, which do not
perform a prior `GetDashboardAsync` call. The Index action's control flow is different: it calls
`GetDashboardAsync` first and branches on the result. Extracting a version that accepts an already-
resolved `Member` would be premature abstraction for a two-site pattern. Inline population keeps the
code readable and avoids the redundant service call.
