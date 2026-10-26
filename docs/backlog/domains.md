# Backlog by domain — the sequencing view

Audience: Anyone deciding what to work on next, and in what order
Scope: Ordered chains of item IDs per business domain, plus the re-cut critical path — pointers only
Last-Validated: 2026-09-14
Source-Commit: fbf4249

This page answers **"in what order?"**. It answers nothing else.

**It defines no item.** Every ID below is a pointer to the single place that item is defined; not
one line of item text, status, priority, owner or acceptance criterion lives here. That is
deliberate: an item that could be described in two files would eventually be described differently
in two files, and this page is where that drift would start. Status and priority are properties of
the item, so read them at the definition — never here.

The classification rules R1–R8, the ID scheme and the vocabularies are in [`README.md`](README.md).

## How to read a chain

`A → B` means **B cannot honestly start before A lands.** It is a dependency, not a schedule; there
are no dates on this page. `A + B → C` means C needs both. `X / Y` means X and Y are siblings at
the same position and may run in either order or in parallel.

Where an ID is defined follows one rule, with no exceptions:

| ID | Defined in |
|---|---|
| `CORE-03`, `CORE-13` … `CORE-25` and `CORE-27` | [`core-platform.md`](core-platform.md) |
| `CORE-01`, `CORE-02`, `CORE-04` … `CORE-12`, `CORE-26` and `CHAT-01` | [`core-capabilities.md`](core-capabilities.md) |
| every other prefix — `CHAN-`, `STOR-`, `NC-DOC-`, `CALDAV-`, `POST-`, `LIST-`, `GPS-`, `APIKEY-`, `BDAY-`, `INV-`, `FIN-`, `NEWS-`, `RENT-`, `TREE-`, `EDIT-`, `DOC-`, `CMS-`, `ORGVIEW-`, `CAR-`, `SB-`, `IAM-` | [`plugins.md`](plugins.md) |

An ID appearing in more than one chain below is the same item seen from two domains, not two items.
It is still defined exactly once.

## The re-cut critical path

Struck-through IDs are `delivered`; they stay on the page because removing them would make the
chain unreadable — a reader needs to see *why* the next item is now free to start.

```
~~CORE-16~~ + ~~CORE-17~~ + ~~CORE-18~~ → CORE-13 → CORE-14 → CORE-02 → storage / channel / document plugins
CORE-18 → CAR-02 / FIN-02 / SB-02 / INV-02 / IAM-02   (the break's cost, one run per repository)
CORE-13 + CORE-15 → CORE-09
CORE-01 → CORE-07 → CALDAV-01
CORE-01 → EDIT-01 / CORE-26
CORE-05 → NEWS-01 / POST-01 / BDAY-01
CORE-19 + CORE-24 → CORE-27
CORE-03 → CORE-06 → INV-01 → RENT-01
```

**The head of the path moved on 2026-09-05.** The three seams that blocked CORE-13 are delivered, so
CORE-13 increment 2 is the new head and CORE-14 sits directly behind it rather than beside it: the
duplicate-key collision CORE-13 must fix is the same defect CORE-14's selection rule resolves, so
they are one unit of work, not two.

**The second line is new and is not optional.** CORE-18 was a deliberate contract break, so every
shipped plugin fell below the floor and stopped activating. Those five adoptions are the price, and
until they land the system runs with no plugins at all — which makes them urgent without making them
difficult. CAR-02 is `partial`: its source is adopted, its release is not cut.

### The order these actually want to be done in

Dependency order permits many schedules; this is the one that keeps the system usable while it is
being worked on.

```
1. record correction        CORE-22 / CORE-23        blocked by nothing, corrects a false record
2. the five adoptions       CAR-02 … IAM-02          restores a system that currently loads no plugin
3. the seam, finished       CORE-13 + CORE-14        unblocks ~10 downstream plugin items
4. settings and secrets     CORE-15                  needed by the host and by every credentialed connector
5. manifest guards          CORE-19 / CORE-24        cheap once the seam's shape is settled
6. capabilities             CORE-01 / CORE-05        then CORE-02, which waits on CORE-14
```

1 before 2 only because it is minutes of work and it is what stops the record lying about 2. Nothing
else in that list is order-sensitive beyond what the chains above already require.

### Why this differs from the retired list

The retired August 2026 source list put CORE-01, CORE-02 and CORE-03 first — its dependency order
at `TodoFeatures.md:486-503` gave the critical path as
`CORE-03 → CORE-02/04/05 → CORE-06 → INV-01 → RENT-01`. That list is git-ignored and retired; see
*Retired sources* in [`README.md`](README.md). Here the platform seams now lead instead, because
minimality is only enforceable once the boundary is real (CORE-16) and a plugin can actually
supersede a core capability (CORE-13/14).

That single change is what re-anchored the priorities: three seams that unblock a dozen plugin items
outrank a capability that unblocks one.

## Domains

### 1. Plugin platform — the seams everything else is built on

```
~~CORE-16~~ + ~~CORE-17~~ + ~~CORE-18~~ → CORE-13 → CORE-14 → CORE-19 → CORE-20 / CORE-21
CORE-15  (parallel to CORE-13, joined at CORE-14)
CORE-24  (parallel; needs no seam work, only the two manifest read paths that already exist)
CORE-03  (independent of the seam chain; blocks the surface work of INV-01, SB-01 and TREE-01)
```

This domain owns the critical path's head. Nothing in domains 2–8 whose owner is a plugin repository
can be built before CORE-13 exists — and, since 2026-09-05, nothing owned by a plugin repository can
be built at all until that repository adopts contract 1.13.0.

### 2. Files, storage and documents

```
CORE-02 → STOR-01 / STOR-02
CORE-02 → DOC-01
CORE-13 + CORE-14 + CORE-15 → NC-DOC-01
```

CORE-14 is the reason STOR-01 and STOR-02 appear here at all: without explicit selection, a storage
plugin cannot supersede and disable the core default, which is what R3 demands. NC-DOC-01 is a
document source rather than a storage backend and additionally needs the secret store, CORE-15.

### 3. Communication — inbox, templates and channels

```
CORE-04 + CORE-05 → CHAN-01 / CHAN-02 / CHAN-03 / CHAN-04
CORE-05 → NEWS-01 / POST-01 / BDAY-01
NEWS-01 → LIST-01
CORE-13 → every CHAN- item
```

LIST-01 sits behind NEWS-01 and never in front of it. CHAT-01 schedules nothing and appears in no
chain: it is `superseded`, and its record is kept only so the reasoning stays findable.

### 4. Organisation, membership and identity

```
CORE-01 → CORE-08
CORE-01 → CORE-26
CORE-01 → ORGVIEW-01
CORE-01 → CORE-10 → CORE-11
IAM-01  (independent; local roles stay the authority, so it waits on nothing here)
```

CORE-01 is the widest fan-out in the core: four domains below start at it.

### 5. Calendar and events

```
CORE-01 → CORE-07 → CALDAV-01
```

The shortest chain on this page, and one of the two organisation-facing paths carried over verbatim
from the retired list.

### 6. Workflows, inventory, rental and money

```
CORE-03 → CORE-06 → INV-01 → RENT-01
CORE-06 → RENT-01   (its workflow is a definition registered against the engine, per R4)
FIN-01  (independent; RENT-01 reaches it through a contract or event, never through its tables)
```

RENT-01 is the only item that depends on two chains at once, and it is the reason CORE-06 must land
before either of them completes.

### 7. Editorial and public content

```
CORE-01 → EDIT-01
CORE-05 → EDIT-01
CMS-01  (independent; no core item blocks it)
NC-DOC-01 / NEWS-01 → EDIT-01   (optional integrations, not prerequisites)
```

The last line is the only optional dependency on this page. EDIT-01 ships without either.

### 8. API access, operations and repository integrity

```
CORE-13 + CORE-15 → CORE-09 → APIKEY-01
CORE-12 / CORE-25  (independent; CORE-25 requires its bootstrap-security decisions first)
CORE-19 + CORE-24 → CORE-27
CORE-22 / CORE-23  (delivered record and repository corrections)
```

CORE-09 is an extraction, not a feature: it releases the work into APIKEY-01, which must not start
before it does. CORE-22 and CORE-23 block nothing and are blocked by nothing, which is why they can
be done at any point — and, being P0 record corrections, preferably early.

### 9. Residue in the shipped plugin repositories

```
CORE-03 → SB-01
CAR-01 / GPS-01 / TREE-01  (independent of every core chain)
```

GPS-01 may link devices to vehicles by stable reference, so it does not wait on CAR-01.

## What is not on this page

- **No dates and no estimates.** Chains are dependency order only.
- **No item text.** Follow the ID to its definition.
- **No new IDs.** Every ID above is defined in `core-platform.md`, `core-capabilities.md` or
  `plugins.md`; this page introduces none of its own.
