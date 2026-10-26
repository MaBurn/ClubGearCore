# Backlog

Audience: Maintainers, plugin authors, architects, anyone triaging a new idea
Scope: Classification rules, ID scheme, status and priority vocabulary, source crosswalk
Last-Validated: 2026-09-14
Source-Commit: fbf4249

## Purpose and scope

This directory is the single tracked backlog for ClubGear. It replaces four rival lists that had
drifted apart in date, language and numbering (see *Retired sources* below).

It answers one question first, before any estimate or schedule: **does this belong in the core, or
in a plugin?** The rules in this file are meant to be applied unaided. Someone holding an
unclassified idea should be able to read this page alone and come away with a status, a priority,
an owner and an ID.

Two properties keep the set honest and are worth stating up front:

- **Status is verified against HEAD, never against a source list.** A source list saying an item is
  open is not evidence. The code, the tests and the changelog are.
- **Every item lives in exactly one file.** `domains.md` holds ordered pointers only, so no item can
  drift between two homes.

Plugin-owned items are deliberately thin: a one-line summary, an owner repository, a priority and a
pointer to centrally collected planning material under this directory when detail is needed. They
carry no line-referenced paths into plugin source, because that detail goes stale the moment the
plugin ships. Plugin implementation and release history still belong in each plugin repository;
backlog and sequencing files do not.

## Index

| File | Contents |
|---|---|
| `README.md` | This page: classification rules, ID scheme, vocabularies, crosswalk, retired sources. |
| `core-platform.md` | Core-owned items in the plugin-platform / host layer. |
| `core-capabilities.md` | Core-owned items in the functional capability layer. |
| `plugins.md` | All plugin-owned items, split into service / provider plugins and functional plugins. |
| `domains.md` | Sequencing view: per business domain, the ordered chain of item IDs. Pointers only. |
| `next-runs.md` | Detailed sequencing view: open items grouped into scoped iterations, one repository per run. Pointers only. |
| `ROADMAP.md` | Prioritised graphical programme view with phases, dependencies, parallel work and repository ownership. Pointers only. |
| `inbox/` | Provenance snapshots with final consolidation dispositions; never item authority. |

## Classification rules

Apply R1 through R7 in order. R8 is the derived tie-break and is used only when none of the others
decides.

| # | Rule | Consequence |
|---|---|---|
| R1 | The **capability** belongs to the core; the **vendor binding** belongs to a plugin. | Chat/message service, file service, template system, notification centre → core. Matrix, WhatsApp, Telegram, SMS, S3, Nextcloud, Listmonk, Traccar, a postal carrier → plugins. |
| R2 | There is **exactly one** template system, and it is central. | No plugin ships a second renderer. Plugins register event keys, placeholders and default templates through contracts; they never know core template IDs. |
| R3 | Local file storage stays in the core as the **default**, but a storage plugin must be able to **supersede and disable** it. | Requires an explicit selection mechanism, not implicit DI last-wins. See CORE-14. |
| R4 | The core provides **one generic workflow engine**; every **concrete** workflow is declared by a plugin. | No `Rental`, `Membership` or `Dunning` state machine in the core. |
| R5 | Two plugin kinds: **service/provider plugins** (chat, print, storage — consumable by *other plugins* as well as by the host) and **functional plugins**. | One mechanism, not two: the provider seam is CORE-13. |
| R6 | A security boundary that must exist **before any plugin loads** stays in the core. | Cookie/Identity auth is permanent core. |
| R7 | Any new plugin is its own `plugins/<Name>/` sub-project with its own `.csproj`, `plugin.json` and `CHANGELOG.md`, versioned independently as a submodule. | Never code in the core tree. |
| R8 | *(derived tie-break)* When R1–R7 do not decide, an item is core only if the **host itself** needs it or **two independent consumers** need it. Otherwise it is a plugin. | Applied to e.g. CORE-11 translations (host needs it) vs. GPS-01 (one consumer → plugin). |

### R7 is a standing rule, not a one-off

Every plugin created from here on is a `plugins/<Name>/` sub-project carrying its own `.csproj`,
its own `plugin.json` and its own `CHANGELOG.md`, and is registered as a git submodule versioned
independently under SemVer. No plugin code is written into the core tree, not even temporarily.

### Three versions, never synchronised

| Version | Lives in | Scheme |
|---|---|---|
| Core application version | `ClubGear.csproj` `<Version>` | CalVer, `<year>.<month>.<counter>` |
| Plugin API contract version | `Contracts/Plugin/ContractVersion.cs` | SemVer |
| Plugin version | each `plugins/<Name>/plugin.json` and its submodule tags | SemVer |

These three are unrelated. A backlog item must never propose bumping one because another moved.

## ID scheme

- `CORE-xx` for core-owned items, in both `core-platform.md` and `core-capabilities.md`.
- One prefix per domain or plugin: `INV-01`, `FIN-01`, `NEWS-01`, `CALDAV-01`, `RENT-01`, `TREE-01`,
  `EDIT-01`, `DOC-01`, `NC-DOC-01`, `POST-01`, `ORGVIEW-01`, `GPS-01`, `CMS-01`, `CHAT-01`,
  `BDAY-01`.

`CORE-01…12` keep the meanings they already had. Platform items promoted during consolidation
**continue the same sequence as `CORE-13…27`** rather than opening a second namespace. There is one
`CORE-xx` space and only one.

## Status vocabulary

Every item carries exactly one status, and that status is established by reading HEAD — the code,
the tests, the changelog — never by trusting a source list.

| Status | Meaning |
|---|---|
| `delivered` | Shipped and verifiable at HEAD. |
| `partial` | Shipped in part; the remaining work is named in the item. |
| `open` | Not started, or started with nothing merged. |
| `superseded` | A different decision was taken and shipped; both sides are recorded on the item. |
| `retired` | Dropped on purpose. Kept only so the reasoning stays findable. |

## Priority

Re-anchored during this consolidation, because the platform seams now outrank the older capability
priorities: minimality is only enforceable once the plugin boundary is real and a plugin can
actually supersede a core capability.

| Priority | Meaning |
|---|---|
| **P0** | Unblocks two or more other items, or corrects a false record. |
| **P1** | User-visible capability with a named consumer. |
| **P2** | Valuable, blocks nothing. |
| **P3** | Optional. |

## Owner

`Owner` names the repository that will hold the change, and therefore who merges it:

- `core` — this repository. The item lives in `core-platform.md` or `core-capabilities.md`.
- a named plugin repository, e.g. `plugin-finance` — the item lives in `plugins.md` and the detail
  stays in that repository's own files.

An item has exactly one owner. "Core and plugin" is not an owner; if both are needed, that is two
items with a dependency between them.

## Crosswalk: `CG-BL-xxx` → new ID

`CG-BL-xxx` is **retired as an ID space**. Its items are absorbed into `CORE-xx` and the plugin
prefixes above. This table is the only place `CG-BL` appears anywhere in the repository, so the
June 2026 numbering stays traceable. A dash means the item is closed and has no successor.

The June list numbers sparsely — 001–003, 010–015, 020–024, 030–034, 040–043, 050–052 — so it holds
twenty-six numbered items, not fifty-two.

| Old ID | Item | Status at HEAD | New ID |
|---|---|---|---|
| CG-BL-001 | Plugin categories in the manifest | `partial` | CORE-19 |
| CG-BL-002 | Catalogue and validate extension points | `delivered` | — |
| CG-BL-003 | Plugin detail page for club admins | `delivered` | — |
| CG-BL-010 | Standard data model for extended member properties | `partial` | CORE-03 |
| CG-BL-011 | Vehicles as the reference profile plugin | `delivered` | plugin residue in `plugins.md` (owner `plugin-carinfo`) |
| CG-BL-012 | GPS tracker example plugin | `open` | GPS-01 |
| CG-BL-013 | Plugin fields in member search and segments | `open` | CORE-03 |
| CG-BL-014 | Import/export for plugin-extended member data | `open` | CORE-03 |
| CG-BL-015 | Self-service visibility for profile plugins | `open` | CORE-03 |
| CG-BL-020 | Navigation contribution for general plugins | `delivered` | — |
| CG-BL-021 | Generic plugin pages for lists and details | `partial` | CORE-03 |
| CG-BL-022 | Inventar as the reference general extension | `delivered` | INV-01 (further work only) |
| CG-BL-023 | Club newspaper as the content example | `open` | CMS-01, with its editorial workflow under EDIT-01 |
| CG-BL-024 | Dashboard widgets for general extensions | `open` | CORE-20 |
| CG-BL-030 | Notification channel provider as a plugin contract | `open` | CORE-13 |
| CG-BL-031 | Identity provider plugin interface | `delivered` | — |
| CG-BL-032 | Audit log sink plugins | `delivered` | — |
| CG-BL-033 | Background jobs for technical plugins | `delivered` | CORE-17 (lifecycle residue only) |
| CG-BL-034 | Central configuration store for technical plugins | `open` | CORE-15 |
| CG-BL-040 | Administer plugin permissions | `delivered` | — |
| CG-BL-041 | Activation with a risk check | `open` | CORE-21 |
| CG-BL-042 | Plugin data lifecycle: deactivate, uninstall, delete | `open` | CORE-21 |
| CG-BL-043 | Plugin health and diagnostics | `open` | CORE-21 |
| CG-BL-050 | Restructure the authoring guide by plugin category | `open` | CORE-27 |
| CG-BL-051 | Plugin project template | `open` | CORE-27 |
| CG-BL-052 | Architecture tests for new extension points | `partial` | CORE-16 for the boundary; each new extension point keeps its own tests |

## Retired sources

Four lists were merged into this directory and are retired as of 2026-09-01:

| Source | Date stamp | Language | Note |
|---|---|---|---|
| `tmp/TodoFeatures.md` | 2026-08-27 | German | `CORE-01…12` plus fourteen plugin IDs. The origin of the `CORE-xx` space. |
| `tmp/P0-*.md` and `tmp/P1-*.md` | undated | English | Eleven refinements — five `P0-`, six `P1-` — of the eleven highest-priority `TodoFeatures.md` entries: `P0-NotificationInbox`, `P0-OrganizationStructure`, `P0-PluginSurfacesAndContracts`, `P0-SecureFileStorage`, `P0-VersionedTemplates`, `P1-CalendarAndEvents`, `P1-FamilyRelationships`, `P1-Finance2`, `P1-Inventory2`, `P1-MassMailNewsletter`, `P1-WorkflowEngine`. Not a separate backlog. |
| `.backlog/backlog.md` | 2026-06-09 | German | `CG-BL-001…052` in six epics; see the crosswalk above. |
| `backlog/finance-plugin.md` | 2026-07-10 | German | Sections A–E. Only section A was still true; its residue moved into `plugins.md`. |

**`tmp/` and `.backlog/` are git-ignored** — `tmp/` by `.gitignore:9` and `.backlog/` by the `.*`
pattern at `.gitignore:18`. They were never tracked, so removing the originals produces no deletion
in Git history. Their collected contents and provenance are preserved under [`inbox/`](inbox/).
`backlog/finance-plugin.md` was tracked, so its earlier retirement remains visible in Git history.
