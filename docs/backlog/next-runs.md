# Next runs — scoped iterations

Last-Validated: 2026-10-02
Source-Commit: fbf4249

A sequencing view for **iterations**, not for items. [`domains.md`](domains.md) gives the dependency
chains and the critical path; this page groups what those chains permit into the units of work a
single CRISPY run can actually finish, and says why each cut falls where it does.

One rule governs every cut: **a run owns exactly one repository.** `plugins/` are git submodules with
their own release cycle, so a core run may never edit them and a plugin run may never edit the core.
Where a change needs both sides, it is two runs with a stated hand-off, never one.

Each run below carries a ready-to-paste `/crispy:start` line. Paste it verbatim — the Questioner
reads it as the task statement.

## What changed since the last revision of this page

The first three runs on the previous version are done, and that moved the head of the critical path.

| Was | Now |
|---|---|
| CORE-16, CORE-17, CORE-18 — the three blockers of the provider seam | `delivered` (`bba6cf2`, `ffe5622`, `087db19`, `9bd0197`) |
| CAR-02 — first of the five plugin adoptions | `partial` — source adopted (`5387af3`, `9a88dac`, `ca3e7e3`), **release not cut** |
| The seam CORE-13 was blocked | **unblocked**; CORE-13 + CORE-14 is now the head |
| — | two new core items filed from the CarInfo adoption: CORE-22 item 5, and CORE-24 |
| Run 2 — CORE-22 items 4/5 and CORE-23 | **run complete** (`294013e`, `30fd068`, plus the local submodule repair); both now `delivered` |

## Order at a glance

| # | Run | Items | Owner | Ready? |
|---|---|---|---|---|
| 1 | Cut the CarInfo release | CAR-02 residue | `plugin-carinfo` | yes — not a CRISPY run, see below |
| 2 | Record correction and submodule hygiene | CORE-22, CORE-23 | `core` | **done** |
| 3 | The four remaining adoptions | FIN-02, SB-02, INV-02, IAM-02 | 4 plugin repos | yes — four runs |
| 4 | The provider seam, finished | CORE-13 increment 2, CORE-14 | `core` | yes — blockers cleared |
| 5 | Plugin settings and secret store | CORE-15 | `core` | yes |
| 6 | Manifest guards | CORE-19, CORE-24 | `core` | after 4 |
| 7 | Organisation structure and sub-units | CORE-01 | `core` | yes |
| 8 | Versioned templates and communication events | CORE-05 | `core` | yes |
| 9 | Secure file and media storage | CORE-02 | `core` | after 4 |
| 10 | Secure first-run installer | CORE-25 | `core` | after bootstrap security decisions |
| 11 | Plugin authoring guide and template | CORE-27 | `core` | after 6 |

Runs 1–4 are the critical path. Everything from 5 on is re-orderable by appetite, subject to the
stated dependency and decision gates.

---

## Run 1 — Cut the CarInfo release

**Items:** the CAR-02 residue. **Owner:** `plugin-carinfo`. **Size:** minutes.

**This is not a CRISPY run.** It is one decision and two commands, and it is listed here only because
the work in front of it is worthless until it happens: CarInfo's source is adopted, but no tag and no
package exist, so from the core's point of view nothing changed and the plugin still does not
activate.

**Decide the signing key before tagging.** Tagging triggers CI signing with
`PLUGIN_SIGNING_PRIVATE_KEY`. Unless that variable holds the key that signed 1.0.9, `signer-public.pem`
rotates and every consumer pinning it must re-trust. Either sync the CI variable to the local
`.signing/signer-private.pem`, or sign locally. Then:

```
git tag v1.0.10 && git push origin v1.0.10
```

Afterwards CAR-02 becomes `delivered` in [`plugins.md`](plugins.md), and the core's submodule pointer
for `plugins/CarInfo` wants a deliberate commit.

## Run 2 — Record correction and submodule hygiene

**Items:** CORE-22, CORE-23. **Owner:** `core`. **Status: done, 2026-10-02.**

Both are *the repository lying about itself*, and neither touches application behaviour. P0 by the
priority rule: correcting a false record outranks new capability. It was placed second rather than
first only because run 1 is shorter and unblocks more.

CORE-22 now carries **six** items. Items 1–3 were corrected on 2026-09-02; items 4 and 5 are
corrected now:

- **Item 4** — `plugins/aktivierung.md` documented Finance `1.8.0` while its manifest declared
  `1.9.0`. Fixed by rewriting the file as a values-free procedure that derives values from
  `plugin.json` and `dist/` instead of publishing them (`294013e`). The June 2026 list's separate
  ask to restructure the authoring and activation guides by plugin category is **not** part of this
  fix and is dropped from this run — it stays filed as `CG-BL-050` in [`README.md`](README.md) and
  the next place it fits is run 6 below.
- **Item 5 (new, from CAR-02)** — the canonical-constructor example at
  `docs/plugin-contract.md:89-92` stored the host context in a `private readonly` field. Unread,
  that is escalated to an error by the `/warnaserror` every plugin pipeline uses, so an author
  following the documented example could ship a plugin that builds locally and fails in CI.
  Corrected to the public auto-property shape the adoption actually used (`30fd068`).
- **Item 6** — points at CORE-24, which is run 6. Not an action of this run.

CORE-23's two defects were local git state on this machine, not tracked content: the Finance
submodule URL pinned to a local path, and `plugins/IamOidc` as an embedded `.git` directory instead
of a standard submodule. Both are repaired locally, and the drift is now guarded permanently by a
committed architecture test and a mirrored CI shell check
(`tests/ClubGear.ArchitectureTests/PluginBoundaryTests.cs`, `.gitlab-ci.yml`) so the same defect
fails the build on any machine, not only this one. `Readme.md` carried a false claim from the same
defect class (said Finance was not a submodule) and is corrected too (`30a2b90`).

```
/crispy:start CORE-22 items 4 and 5, and CORE-23: fix the activation-guide version drift; correct the canonical-constructor example in docs/plugin-contract.md to the public auto-property shape actually adopted in CAR-02, because the documented private-readonly field is unread and fails CI under /warnaserror; repair the Finance submodule URL and convert plugins/IamOidc to a standard submodule.
```

The command above is kept for the historical record; this run already executed it.

## Run 3 — The four remaining adoptions

**Items:** FIN-02, SB-02, INV-02, IAM-02. **Owner:** four plugin repositories.

**This is four runs, not one**, by the one-repository rule. They share a scope statement and nothing
couples them to each other, so they can proceed in any order or in parallel.

CAR-02 settled the shape, and [`plugins.md`](plugins.md) records what it established — the public
auto-property constructor, both declarations moving, non-load-gated providers converting too, and the
release being a separate deliberate act. FIN-02, SB-02 and INV-02 are largely mechanical against that
precedent.

**IAM-02 is not.** Its three identity providers derive from a shared `OidcIdentityProviderBase`, and
a property on a base class plus constructors on the derived types is a different question — the
activator permits exactly one public constructor per type. Its own design must answer that rather
than assume the CarInfo precedent covers it. Its test project also instantiates providers directly
and moves with them. Do it last, or first if you would rather meet the hard case early.

```
/crispy:start Adopt plugin contract 1.13.0 in this repository: give every contribution type the canonical T(IPluginHostContext) constructor as a public IPluginHostContext HostContext { get; } auto-property, move both the plugin.json contract floor and the compiled in-code manifest to >=1.13.0, bump the version at both sites, and record the release and its compatibility break in this repository's CHANGELOG. See the CAR-02 adoption note in the core's docs/backlog/plugins.md for what the first adoption established.
```

## Run 4 — The provider seam, finished

**Items:** CORE-13 increment 2, CORE-14. Both P0. **Owner:** `core`. **Size:** large.

**This is the run everything else was waiting for.** CORE-13 named CORE-16, CORE-17 and CORE-18 as
the three reasons it could not ship; all three are delivered. It is the head of the critical path and
stands in front of roughly ten downstream plugin items.

CORE-13 and CORE-14 are **one** run because of a single shared defect. `NotificationService` builds a
name-keyed dictionary in its constructor and `ToDictionary` throws on a duplicate key, so two plugins
claiming `"email"` is a scope-construction failure rather than an override. CORE-13 must replace that
dictionary; CORE-14 defines what replaces it. Split them and you get either a collision-tolerant
lookup nobody selects from, or a selection mechanism over a lookup that still throws.

What lands: `Resolve<T>` and `ResolveAll<T>` reachable from a plugin through `IPluginHostContext`, so
one plugin's provider is consumable by another; admin-selected precedence per capability key, where
choosing a plugin provider **supersedes and disables** the core default; registration that never
throws on a duplicate and surfaces unresolved collisions instead of picking arbitrarily; and the
`NotificationService` swap, which belongs here and not earlier.

```
/crispy:start CORE-13 increment 2 and CORE-14: finish the provider contribution seam with Resolve/ResolveAll reachable from a plugin through IPluginHostContext, and add admin-selected precedence per capability key where a plugin provider supersedes and disables the core default. Registration must tolerate duplicate keys and surface unresolved collisions instead of throwing, which includes replacing the constructor-time ToDictionary in NotificationService.
```

## Run 5 — Plugin settings and secret store

**Items:** CORE-15, P0. **Owner:** `core`.

Two independent consumers make it P0: the host needs it for itself, and every credentialed connector
needs it. Nothing blocks it, so it can run in parallel with run 4 if you are willing to have two open
core runs — but it is the safer one to defer, since no shipped code is waiting on it.

```
/crispy:start CORE-15: a plugin settings and secret store in the core, usable both by the host itself and by credentialed plugin connectors.
```

## Run 6 — Manifest guards

**Items:** CORE-19 (`partial`, P1), CORE-24 (P1, new). **Owner:** `core`.

Both are about the manifest, and both are cheap once the seam's shape is settled — hence after run 4.

- **CORE-19** formalises the manifest category allowlist and the service/functional plugin kind. The
  split is only observable once service plugins have a seam to contribute through.
- **CORE-24** is new, filed from CAR-02. A plugin declares its version and contract floor twice and
  nothing compares them. The failure is asymmetric: a stale floor in `plugin.json` fails closed and
  reports itself, while a stale in-code floor fails **open** — the plugin activates keeping its old
  claim, and the tag check only reads `plugin.json`. CarInfo had shipped in exactly that state. The
  adoption held the two together by hand, which is a guarantee that lasts until the first person who
  has not read the plan.

**CORE-27** follows this run: it combines CG-BL-050's category-oriented authoring guidance with
CG-BL-051's project template. It waits for CORE-19 to settle the vocabulary and CORE-24 to settle
manifest consistency, but remains a separate run so documentation/template work cannot block those
guards.

```
/crispy:start CORE-19 and CORE-24: the manifest category allowlist plus the service/functional plugin kind, and a host-side consistency check that a loaded module's compiled manifest agrees with the plugin.json it shipped beside, on both version and contract floor, reported against the owning module id.
```

## Runs 7–9 — The capability layer

Three P0 capability items. Two are free to start now; the third is not.

- **Run 7 — CORE-01, organisation structure and sub-units.** The widest fan-out in the core: members,
  roles, events, CORE-08, ORGVIEW-01, CORE-10 and EDIT-01 all hang off it. No platform dependency.
- **Run 8 — CORE-05, versioned templates and communication events** (`partial`). Precondition for
  NEWS-01 and POST-01.
- **Run 9 — CORE-02, secure file and media storage.** **After run 4.** R3 requires a storage plugin to
  supersede *and disable* the core's local backend, and that mechanism is CORE-14. Building the core
  backend first without it invites a second, incompatible override path. Direct precondition for
  STOR-01 and STOR-02.

```
/crispy:start CORE-01: organisation structure and sub-units.
/crispy:start CORE-05: versioned templates and communication events.
/crispy:start CORE-02: secure file and media storage in the core, as the default backend a storage plugin can supersede and disable per CORE-14.
```

## The tail

Not scoped into runs, because each is P2/P3 or waits on the runs above. Kept so the sequencing
question is not re-asked.

| Item | Waiting on | Note |
|---|---|---|
| CORE-03 | — | Remaining plugin-surface increments; blocks INV-01, SB-01, TREE-01. |
| CORE-20, CORE-21 | run 4 | Dashboard widgets; plugin health and data lifecycle. |
| CORE-25 | bootstrap-security decisions | Secure first-run installer; independent of plugin work. |
| CORE-26 | CORE-01 | Local-unit radius search and export. |
| CORE-27 | run 6 | Authoring guidance and project template after manifest vocabulary/guards. |
| CORE-09 / APIKEY-01 | runs 4 + 5 | A two-repository hand-off: core extraction first, plugin second. |
| CORE-04, CORE-06, CORE-07, CORE-08 | CORE-01 for 07/08 | Inbox, workflow engine, calendar, households. |
| CORE-10, CORE-11, CORE-12 | CORE-01 for 10 | Branding, translations, operations views. P2/P3. |
| BDAY-01 | CORE-05 | Birthday/anniversary automation in its own plugin repository. |
| CHAN-01…04, STOR-01/02, CALDAV-01, POST-01, LIST-01, GPS-01 | run 4, plus CORE-02 for storage | All provider plugins, therefore all downstream of the seam. |
| INV-01, FIN-01, NEWS-01, RENT-01, TREE-01, EDIT-01, DOC-01, CMS-01, ORGVIEW-01, CAR-01, SB-01, IAM-01 | run 3 | Functional plugin work; each needs its plugin to load again first. |

## Keeping this page honest

This page is a view, not a source. When an item's status changes, change it in
[`core-platform.md`](core-platform.md), [`core-capabilities.md`](core-capabilities.md) or
[`plugins.md`](plugins.md) first, then update the rows here. If the two disagree, the item file wins
and this page is the defect — the rule CORE-22 applies to the rest of the record.

Dependency order is [`domains.md`](domains.md)'s job, not this page's. Where the two appear to
disagree, `domains.md` is the authority on *what may start*; this page only chooses among the orders
it already permits.

Do not put this file in `.crispy/`. `/crispy:start` deletes `.crispy/*.md` as its first step.
