# ADR: Plugin Tab Grouping

## Purpose

Persistent architectural decisions for the plugin-tab-grouping iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-18 - GroupKey/GroupTitle Added as Non-Positional Init Properties on MemberEditTabSlot

### Decision

Two optional, non-positional `init`-only properties — `GroupKey` (string?) and `GroupTitle`
(string?) — are added to the `MemberEditTabSlot` sealed record in
`Contracts/Plugin/IMemberEditTabProvider.cs`. Corresponding properties are added to the host-side
view model `MemberPluginEditTabView` in `Services/Abstractions/IMemberPluginSlotService.cs` and
are populated by `CollectEditTabsAsync` in `MemberPluginSlotService`.

### Rationale

Adding non-positional init properties to a C# positional record does not change the positional
constructor arity. Plugin assemblies compiled against the previous version of the contract DLL
continue to load and run without modification (binary compatibility is preserved). All existing
call sites using `new MemberEditTabSlot("key", "Title", html)` or the four-argument form continue
to compile and produce correct results; `GroupKey` and `GroupTitle` default to null, which the
partial interprets as "no group" and renders as a solo flat card (unchanged from previous
behaviour). This approach requires no contract versioning, no assembly retargeting, and no test
changes.

---

## 2026-06-18 - Grouping Is Resolved Entirely in _PluginSlots.cshtml (edit-cards Branch)

### Decision

The `edit-cards` branch of `Views/Members/_PluginSlots.cshtml` is the sole site where grouping
logic is applied. `MemberPluginSlotService.GetSlotsAsync` does not group, sort within groups, or
otherwise change its output contract. The partial groups the already-sorted `EditTabs` list using
LINQ `GroupBy` on `GroupKey`, preserving the insertion order of groups (first-occurrence of each
key determines group order).

### Rationale

Keeping grouping in the partial satisfies all existing architecture test constraints without any
test changes:
(a) `Profile.cshtml` does not change, so the assertion that it does not contain
    "Plugin-Erweiterungen" and does contain `ViewData["PluginSlotMode"] = "edit-cards"` passes.
(b) No host file gains a `carinfo`-specific string.
(c) The outer `<div data-plugin-slot="edit-cards">` wrapper asserted by `MemberPluginActionTests`
    is unchanged.
Moving grouping into the service would require adding a new output type or changing
`MemberPluginSlotSnapshot`, which would widen the blast radius to all consumers of the service.

---

## 2026-06-18 - CarInfo and ServiceBook Share GroupKey "fahrzeuge" with GroupTitle "Fahrzeuge"

### Decision

`CarInfoEditTabProvider` sets `GroupKey = "fahrzeuge"` and `GroupTitle = "Fahrzeuge"` on its
`MemberEditTabSlot`. `ServiceBookEditTabProvider` sets the same `GroupKey = "fahrzeuge"` and
`GroupTitle = "Fahrzeuge"`. The CarInfo tab title remains `"Fahrzeuge"` (used as the inner tab
label); the ServiceBook tab title remains `"Serviceheft"` (used as the inner tab label).

### Rationale

A shared, stable `GroupKey` string is the coordination mechanism between two independently loaded
plugins. The key is meaningful, lowercase, and kebab-safe; it does not need to match any database
identifier or route segment. Both plugins set an identical `GroupTitle` because the partial uses
only the first entry's `GroupTitle` as the card heading — if the two differed, the displayed
heading would depend on sort order, which is undesirable. Enforcing agreement is a plugin-author
convention; the host does not validate it.

---

## 2026-06-18 - Grouped Cards Use Bootstrap Nav-Tabs Inside a Single card-body

### Decision

When the partial detects two or more tabs sharing the same `GroupKey`, it renders a single
`<section class="card border-light-subtle mb-3">` with a `<ul class="nav nav-tabs">` strip followed by a
`<div class="tab-content">`. This mirrors the Bootstrap tab structure already used in the `edit`
mode (`_PluginSlots.cshtml` lines 60–101), making the HTML patterns consistent. Solo tabs
(null `GroupKey` or no peer with the same key) continue to render the flat layout unchanged.

### Rationale

Reusing the `edit`-mode Bootstrap tab structure requires no new CSS or JavaScript. Bootstrap's
tab initialisation is data-attribute-driven (`data-bs-toggle="tab"`), so no additional JS wiring
is needed. The `document.querySelector('[data-carinfo-vehicles]')` call in
`ServiceBookProviders.cgcs` queries the full document DOM and is unaffected by tab-pane
visibility toggling (`d-none` / `show active` are CSS-only and do not remove elements from the
DOM tree).
