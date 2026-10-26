# ADR 0002: UI Style Guide

Date: 2026-07-09

Status: Accepted

## Purpose

This ADR is the visual and front-end style guide for ClubGear. It documents the design system
that already exists in `wwwroot/css/site.css` and the Razor views, and makes it normative so that
new screens — core pages and host-rendered plugin pages alike — stay visually consistent instead
of drifting into per-page ad-hoc styling.

It is a companion to [ADR 0001](0001-plugin-development-guardrails.md). Where 0001 governs *where*
code may live, this ADR governs *how* the resulting UI looks and is named. On rendering ownership
(host renders, plugins provide data) 0001 GR-008 wins; this ADR only refines the visual result.

## Normative Keywords

The keywords `MUST`, `MUST NOT`, `SHOULD`, `SHOULD NOT`, and `MAY` are normative, as in RFC 2119.

If this ADR conflicts with a task request, the user MAY override it for that task; absent an
explicit override, this ADR applies.

## Scope

Applies to all server-rendered UI: Razor views under `Views/`, shared styles in
`wwwroot/css/site.css`, and the host-rendered plugin surfaces (member cards, edit tabs, generic
plugin pages, dashboards). It does not govern the internal HTML a plugin returns as raw markup,
which is subject to GR-008 and SHOULD be avoided in favour of structured metadata.

## Foundations

### SG-001: Framework Baseline

The UI is built on **Bootstrap 5.3** and **Bootstrap Icons 1.11**, loaded in
`Views/Shared/_Layout.cshtml`. New UI MUST use Bootstrap layout, spacing, and components as the
default toolbox and MUST NOT introduce a second CSS framework or a component library that
duplicates Bootstrap. Icons MUST come from Bootstrap Icons (`<i class="bi bi-...">`); do not embed
icon fonts or inline SVG icon sets from elsewhere.

Custom CSS in `site.css` exists to brand and extend Bootstrap, not to replace it. Prefer a
Bootstrap utility (`d-flex`, `gap-2`, `text-muted`, `mb-3`) over a new custom rule.

### SG-002: Colour Palette

Colours MUST come from this palette. Do not invent new brand hues per page.

| Role | Hex | Usage |
|---|---|---|
| Brand primary | `#0077BA` | Primary buttons, links, headings, navbar, footer |
| Brand primary (hover/active) | `#005b99` | `:hover`/`:focus` of primary controls |
| Accent | `#EF7D00` | Sparing highlights, link hover, hero glow, focus warmth |
| Ink / deep heading | `#16354f` | High-contrast values in stat tiles and data grids |
| Body text | `#222` | Default text |
| Muted text | `#5f6368`, `#6a7178`, `#4d5358` | Subtitles, eyebrows, help text, table meta |
| Panel border | `#e6e8eb` (`--cg-panel-border`) | Card/panel outlines |
| Input border | `#d5dce2` | Form controls |
| Neutral surfaces | `#fff`, `#fbfcfd`, `#f4f7fa`, `#f0f2f4` | Backgrounds, table headers, hairlines |
| Tint — blue | `~rgba(0,119,186,0.05–0.12)` | Hover rows, focus rings, hero wash |
| Success | `#187344` / `#25633a` | Verified/positive states |

Semantic status (success / warning / danger / info) SHOULD use Bootstrap's contextual classes
(`text-bg-success`, `badge bg-warning text-dark`, `alert-danger`) rather than raw hex, so states
stay recognisable across the app.

Focus rings on branded controls MUST use the blue tint, matching existing inputs:
`box-shadow: 0 0 0 0.2rem rgba(0, 119, 186, 0.12)`.

### SG-003: Typography

- Font stack: `"Segoe UI", Arial, sans-serif`. Do not add web-font `@import`s.
- Body: `16px` base, `line-height: 1.6`, colour `#222`.
- Headings are brand blue: `h1` weight `700`; `h2`–`h6` weight `600`.
- An **eyebrow** (small uppercase kicker above a title) MUST be muted grey, `~0.75rem`,
  `font-weight: 700`, `letter-spacing: ~0.08em`, `text-transform: uppercase`.
- Data values that need emphasis (stat tiles, detail grids) use the ink colour `#16354f` and a
  larger size; their labels use muted grey.

### SG-004: Spacing & Layout Tokens

Vertical rhythm MUST use the CSS custom properties declared in `:root` in `site.css`, not
hard-coded magic numbers:

- `--cg-page-gap: 1rem` — gap between stacked page sections
- `--cg-page-top-spacing: 1.5rem` / `--cg-page-bottom-spacing: 2rem` — page padding
- `--cg-action-gap: 0.75rem` — gap between action controls
- `--cg-panel-border: #e6e8eb` — the one panel border colour

A top-level page SHOULD be a `.cg-page` grid container that stacks sections with
`gap: var(--cg-page-gap)`. New spacing tokens SHOULD be added to `:root` rather than repeated as
literals.

### SG-005: Cards & Surfaces

Cards MUST follow the global card treatment: `border-radius: 8px`, `1px solid` hairline border,
optional `shadow-sm`. Panels sit on white or a near-white neutral surface. Avoid heavy drop
shadows and full-black borders.

## Naming Conventions

### SG-006: Class Prefixes

Custom classes MUST be namespaced. Two families exist and MUST be used consistently:

- `cg-*` — **shared/core UI primitives** reusable across the app and by host-rendered plugin
  surfaces: `cg-page`, `cg-page-title`, `cg-page-subtitle`, `cg-panel`, `cg-plugin-page`,
  `cg-plugin-hero`, `cg-plugin-table`, …
- `<feature>-*` — **feature-scoped families** for one area: `members-*`, `selfservice-*`,
  `plugin-admin-*`.

New reusable primitives MUST use the `cg-` prefix. New feature-only styling SHOULD use a
`<feature>-` prefix and MUST NOT leak feature-specific names into `cg-`.

### SG-007: BEM-style Structure

Component classes MUST follow the block / element / modifier pattern already in use:

- Block: `.members-overview-hero`, `.cg-plugin-hero`
- Element: `.members-overview-hero__title`, `.cg-plugin-hero__eyebrow` (double underscore)
- Modifier: `.cg-plugin-page--compact` (double hyphen)

Do not nest deep descendant selectors when an element class expresses intent more clearly.

### SG-008: Bootstrap First, Custom Second

Reach for Bootstrap utilities before writing CSS. Introduce a custom class only when a pattern
repeats, needs brand colour, or expresses a domain concept. Inline `style="..."` attributes
SHOULD be avoided except for genuinely dynamic, one-off values (e.g. a computed avatar size).

## Component Patterns

### SG-009: Page Header (Hero)

A primary page SHOULD open with a hero header rather than a bare `<h2>`. The reference
implementation is `.cg-plugin-hero` / `.members-overview-hero`:

- optional muted **eyebrow**, then the `.cg-page-title`, then an optional muted subtitle;
- a subtle branded wash: `radial-gradient(... accent ...)` over
  `linear-gradient(135deg, rgba(0,119,186,.08), #fff)`;
- primary/section actions aligned to the trailing edge, wrapping on small screens.

### SG-010: Search & Filter

List filters SHOULD use a single rounded input group with a leading `bi-search` icon, a reset
control shown only when a filter is active, and a primary submit button — see `.cg-plugin-search`.
Placeholder text comes from the data provider where the surface is generic.

### SG-011: Data Tables

Tabular data MUST use a bordered card wrapping a `.table-responsive`. Tables SHOULD:

- use a **sticky, uppercase, muted header row** on a neutral surface (`.cg-plugin-table thead th`);
- apply a subtle **blue hover tint** on body rows;
- **right-align a trailing actions column** and keep action buttons small (`btn-sm`) with
  outline styling;
- render a `–` placeholder for empty cells and an **illustrated empty state** (icon + message)
  when there are no rows.

### SG-012: Detail / Key–Value Views

A single record SHOULD render as a responsive key/value grid (`.cg-plugin-detail-grid`): uppercase
muted `dt` labels over ink-coloured `dd` values, wrapping into columns via
`grid-template-columns: repeat(auto-fit, minmax(...))`. Do not use a two-column `<dl class="row">`
that collapses awkwardly on narrow screens.

### SG-013: Buttons & Actions

- Primary action per view: `.btn-primary` (brand blue).
- Secondary / row-level actions: `.btn-outline-secondary` / `.btn-outline-primary`, usually
  `btn-sm` in tables.
- Destructive actions: outline-danger, and confirm before acting.
- Buttons that carry an icon MUST pair it with a text label (`<i class="bi bi-..."></i> Label`),
  except icon-only controls that have an `aria-label`/`title`.

### SG-014: Badges & Status

Status MUST be shown as a Bootstrap badge with a semantic colour, not as coloured free text.
Human-readable labels are preferred over raw enum names in user-facing cells.

## Accessibility

### SG-015: Accessible by Default

- Every form control MUST have a `<label>` (or `aria-label`); required fields marked with a
  visible indicator, not colour alone.
- Icon-only controls MUST carry `aria-label` and, where useful, `title`.
- Interactive elements MUST retain a visible focus state; do not remove focus outlines without
  replacing them with the branded focus ring (SG-002).
- Search regions SHOULD use `role="search"`; decorative icons SHOULD not be announced.
- Colour MUST NOT be the sole carrier of meaning.

## Plugin UI

### SG-016: Host Styling, Generic Surfaces

Host-rendered plugin surfaces (generic plugin pages, member cards, edit tabs, dashboard widgets)
MUST adopt this style guide through the shared `cg-*` classes so every plugin page looks native.
Per GR-008, plugins provide structured data/metadata and the host owns rendering and styling.

Core CSS MUST NOT contain styling keyed to one named plugin (no `.finance-...` rule in `site.css`).
Shared plugin styling MUST be generic (e.g. `.cg-plugin-page`) and scoped under a wrapper class so
it cannot bleed into unrelated pages. If a single plugin genuinely needs bespoke styling, that CSS
belongs to the plugin, not the core.

## Consequences

New screens start from a documented palette, token set, and component vocabulary, so they look
like ClubGear without rediscovery. The cost is discipline: contributors must extend `site.css` and
its tokens rather than paste one-off colours and spacing. The guide is descriptive of the current
system first and prescriptive second — when the system evolves, update this ADR in the same change.
