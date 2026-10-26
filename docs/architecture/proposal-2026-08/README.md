# ClubGear — architecture proposal for the open features

Date: 2026-08-30 · Author: coding agent · Status: **for review, nothing implemented**
Revision 3 — D6 revised: calendar entries are stored **in** Nextcloud, not synchronised with it.

Input: the eleven feature specifications and the backlog overview in `ClubGear/tmp/`.
Nothing in the ClubGear repository was changed to produce this.

```
/tmp/clubgear-proposal/
├── README.md                     ← this document: the proposal and the decisions it asks for
├── architecture/
│   ├── ClubGear-Architecture-Proposal.drawio   ← 25 pages: 11 diagrams + 14 embedded mockups
│   ├── png/page-1..11.png                      ← rendered diagrams (generated, git-ignored)
│   ├── dio.py, p1_p2.py, p3_p5.py, p6_p8.py, p9.py, p10.py, p11.py, build.py  ← generators
├── mockups/
│   ├── 01..14-*.svg              ← the GUI mockups, source
│   ├── png/                      ← optional PNG renders (generated, git-ignored)
│   ├── png/                      ← the same at 2× for viewing
│   ├── _kit.py, gen_*.py, render.sh            ← generators (re-run: python3 gen_*.py && ./render.sh)
└── research/
    ├── 01-clubmanager-inventory.md             ← what the legacy system already implements
    └── 02-market-scan.md                       ← external features worth knowing about
```

Open the `.drawio` file in draw.io (installed locally) — the mockups are embedded as pages, so the
single file is enough to review everything.

---

Rendered PNGs are not committed — they are reproducible. Regenerate with
`mockups/render.sh`, `pitches/render.sh` and `architecture/build.py`
(the last one also re-embeds the mockups into the .drawio).
The SVGs and the .drawio open directly in a browser or in draw.io.

## 1. What is being proposed

The backlog contains 26 features. This proposal does **not** propose to build them. It proposes a
boundary and a sequence, so that whichever subset is actually built does not have to be rebuilt.

The boundary in one sentence: **the core owns the platform, plugins own the domains, and no vendor
name appears in the core.**

| Layer | Contents | Rule |
|---|---|---|
| Core | identity, permissions, members, organisation, households, files, templates, notifications, workflow, calendar, audit, config | Never branches on a module id |
| Contract | generic UI, transport, storage, channel and workflow concepts, SemVer | Never contains a product-specific DTO |
| Plugins | inventory, finance, newsletter, rental, editorial, documents, connectors, channel providers | Own schema, own repository, own version |

Diagram pages 1, 2 and 7 carry this.

## 2. The six decisions this proposal actually asks you to take

Everything else follows from these. They are ordered by how expensive they are to reverse.

### D1 — CORE-03 is the first thing built, and it is built demand-driven

The current contract (`ContractVersion 1.10.0`) gives a plugin a table with columns and commands,
one filter string, and untyped string arguments. Inventory 2.0, Finance 2.0 and Newsletter each
need pagination, typed forms, uploads, downloads and a JSON twin. Without CORE-03 those become
special cases in the host, which is exactly what the backlog forbids.

The risk is the opposite one: designing the contract speculatively. **Recommendation:** extend the
contract only for a capability a concrete plugin needs now, with a compatibility test per increment,
and raise `MinimumSupported` only when an old shape is actually removed.

*Alternative considered:* build INV-01 first and let the contract follow. Rejected — the survey shows
the legacy inventory controller is 128 KB; whatever it needs would land in the host as one-off code.

### D2 — Email is the only outbound channel in the core

Matrix, Telegram, SMS and E-Post become installable channel-provider plugins behind one contract:
stable channel key, advertised capabilities, typed destination, configuration schema, health check,
send, and an authenticated status callback. The core dispatches through an outbox and applies an
**explicit** per-event fallback policy — queue and retry, fall back to email, inbox only, or fail.
Never a silent substitution.

Legacy evidence: Clubmanager's inbox and per-product channel preferences already work well, but the
Matrix agent is part of the host. Mockups 04 and 05 and diagram page 4 show the target.

### D3 — One file service, one template system, one state machine — with no legacy twin

The legacy system built templates twice (V1 portal plus V2 controllers), workflow twice (generic
engine plus a hard-coded `RentalStateMachine`) and lending twice (`ToolRental` plus
`MarketplaceRental`). Uploads were implemented at least seven times across seven image tables.

**Recommendation:** accept a slower migration in exchange for never running two systems in parallel.
Concretely: no rental workflow in the core, no second renderer beside CORE-05, and simple lending
inside Inventory rather than a second rental plugin.

### D6 — Files and calendars are pluggable stores: local **or** Nextcloud, chosen by the administrator

*Added after review.* The core does not gain Nextcloud support — it gains two ports:

- `IFileStorageBackend` — `put` / `open` / `delete` / `exists` / `health`, with the storage key
  generated by the core. Implementations: **local filesystem** (ships with the core, default) and
  **Nextcloud WebDAV** (plugin).
- `ICalendarStore` — `readRange(from, to, syncToken)` / `upsert(item, ifMatch)` / `delete` /
  `syncToken` / `reportConflicts`, carrying the **complete** VEVENT including attendees.
  Implementations: **local database** (for installations without Nextcloud) and
  **Nextcloud CalDAV** (plugin).

The selection is administrator configuration: one default backend for files with an optional
per-scope override, and a per-calendar storage mode. Diagram pages 9 and 10, mockups 13 and 14.

**Files and calendar are deliberately asymmetric.** For files, only the bytes move — metadata,
policy and authorisation stay in ClubGear, because they are what the upload gates and download
checks operate on. For calendars, following your instruction, **the whole entry lives in Nextcloud**:

| ClubGear concept | Stored in Nextcloud as |
|---|---|
| Title, description, place | `SUMMARY` · `DESCRIPTION` · `LOCATION` |
| Start, end, time zone, all-day | `DTSTART` / `DTEND` with `TZID` |
| Recurrence and exceptions | `RRULE` · `EXDATE` · `RECURRENCE-ID` |
| Cancelled | `STATUS:CANCELLED` |
| Organiser | `ORGANIZER` |
| **Registration** | `ATTENDEE` with `PARTSTAT` + `RSVP` |
| Waitlist position | `ATTENDEE;ROLE=OPT-PARTICIPANT` + `X-CLUBGEAR-*` |
| Capacity | `X-CLUBGEAR-CAPACITY` |
| Organisation unit, visibility | `X-CLUBGEAR-UNIT` · `CLASS` · `CATEGORIES` |

For calendars in Nextcloud mode ClubGear therefore has **no events table, no registrations table and
no waitlist table**. What it keeps is a discardable read cache (so a query like "all events of unit X
in September where I am registered" does not fetch and parse the collection every time), the
member ↔ `ATTENDEE` link, its own audit entries, and notification delivery records. "Rebuild cache"
drops and refetches; any disagreement resolves in favour of Nextcloud.

**Four consequences that come with this — stated so they are not a surprise later:**

1. *Capacity is enforced optimistically, not transactionally.* There is no database transaction to
   hold a limit. A registration is: read the event and its `ETag`, verify free places, `PUT` with
   `If-Match`, and on `412` re-read and retry within a bound. Correct and serialised per event, but
   two members racing for the last place means one gets a retry and possibly a "place just taken".
2. *Foreign CalDAV clients can damage the extensions.* Capacity, unit and waitlist ride on `X-`
   properties; a phone or desktop client editing the event may drop unknown properties or attendee
   lines. **Recommendation:** share the managed calendars read-only and keep writes in ClubGear or
   the Nextcloud web UI. If members must edit from their own clients, expect a repair path.
3. *Availability and backup move to Nextcloud.* By design there is no local master copy. Nextcloud
   downtime leaves a read-only, stale calendar and failing writes; deleting the collection in
   Nextcloud deletes the data.
4. *Personal data leaves the ClubGear database.* Attendee names and addresses live in the Nextcloud
   instance, so a member deletion request must purge `ATTENDEE` entries across all collections.

**Cost.** CORE-02 grows by ~S (backend interface, backend column on the metadata row, migration job,
integrity check). CORE-07 grows by ~M–L (store port, cache, optimistic write path) — but that work
*replaces* both the local event tables and the separate connector the original backlog called
CALDAV-01, so the
net addition to the plan stays moderate. The Nextcloud plugin itself is M–L.

**Why a port instead of "Nextcloud support" in the core:** Clubmanager wired it in directly —
`NextcloudCalendarService` 42 KB, `NextcloudDocumentService` 26 KB, `WebDavService` 19 KB — and both
config tables store the app password in plain text with a `// TODO: Verschlüsseln` comment. A port
keeps that code in one plugin, keeps the credential in the plugin's namespaced encrypted store, and
keeps a ClubGear installation without Nextcloud fully functional.

### D4 — The organisation unit is the scope for permissions and data, and Keycloak is not

CORE-01 introduces units, memberships, delegated administration and unit-scoped permission checks.
Clubmanager mirrors Keycloak groups into five local tables; ClubGear keeps local roles authoritative
and treats external group mapping as IAM-plugin work. This is a rewrite of that area, not a port.

### D5 — Plugins never touch core tables, and the core never touches plugin tables

Access goes through read models and facades (member summary, unit reference, household snapshot,
permission check). Finance keeps bank data; Rental pays through a Finance contract or event, never
through `fin_*` tables; Newsletter reads an approved recipient projection, not the member entity.

This is what allows the plugins to keep their own release cycle — and what makes an uninstall
possible without leaving foreign keys behind. Diagram page 7.

## 3. Sequence

Four blocks, each ending in something usable. Diagram pages 3 and 8 carry the detail.

| Block | Contents | Rough effort |
|---|---|---|
| A — platform | CORE-03 contract increment → CORE-02 file storage incl. backend port → CORE-01 organisation | 33–50 PT |
| B — communication | CORE-05 templates → CORE-04 inbox + provider contract → CORE-08 households | 32–50 PT |
| C — existing plugins | capability matrix, then INV-01 and FIN-01 in slices | 55–90 PT |
| D — new modules | CORE-06, CORE-07 with the store port, the Nextcloud plugin (NC-01), NEWS-01, then RENT-01 / EDIT-01 / the P3 set | 100+ PT |

Critical path for legacy parity:
`CORE-03 → CORE-02 / CORE-04 / CORE-05 → CORE-06 → INV-01 → RENT-01`, and for anything
organisation-bound `CORE-01 → CORE-07 → NC-01` and `CORE-01 → EDIT-01`.

## 4. The concern I want to state plainly

The backlog as written is roughly 400+ person-days, and the legacy system it is measured against has
115 tables, 58 controllers and several files between 50 KB and 280 KB. Delivered by one person
alongside everything else, a complete port is a multi-year programme.

That does not make the proposal wrong — the boundary is the same whether three modules or twenty get
built. But **the useful question before Block A is not "what is the architecture", it is "which three
or four domains must exist before Clubmanager can be switched off"**. The rest can stay in the legacy
system or be dropped. If you tell me that subset, the sequence above can be cut down to it, and some
of the P0 scope may shrink with it — for example, CORE-06 and CORE-07 are only on the critical path
because rental and calendar are on the list.

## 5. Mockups

Layout and information architecture only, following ADR 0002 (Bootstrap 5.3, brand `#0077BA`,
Bootstrap Icons, the `--cg-*` spacing tokens). They are not a pixel specification.

| # | Screen | Shows the decision |
|---|---|---|
| 01 | Organisation structure | unit tree, delegated administration scope, self-service join state |
| 02 | File and media storage | per-scope upload policies, the shared upload pipeline, rejection |
| 03 | Host-rendered plugin page | filters, pagination, row actions — and the host/plugin split |
| 04 | Notification inbox | per-notification delivery trail incl. an applied fallback policy |
| 05 | Notification preferences | channel matrix with provider plugins, mandatory events, destinations |
| 06 | Template editor | versions, rollback, placeholder schema, blocked publication |
| 07 | Workflow | definition, instance history, rejected transition, outbox side effect |
| 08 | Calendar and events | recurrence, exceptions, capacity, connector attribution |
| 09 | Household relationships | dated relationships, validation rules, what other modules may read |
| 10 | Inventory 2.0 | stock, images via CORE-02, codes, loan, history, mobile API |
| 11 | Finance 2.0 | payment matching with visible reasons, fee-run states, masked IBAN |
| 12 | Newsletter | frozen recipient snapshot, consent exclusions, resumable batches |
| 13 | File storage backend | local vs Nextcloud, per-scope override, online migration, failure policy |
| 14 | Calendar storage | field-by-field mapping into the VEVENT, what ClubGear still holds, the four consequences |

## 6. Research summary

`research/01-clubmanager-inventory.md` — what the legacy system already implements, per topic, with
file sizes and action lists. Headline findings: the legacy inbox and template versioning already
work (so CORE-04/05 are lower-risk than they look); inventory is close to a straight port; uploads,
templates, workflow and lending were each built twice or more; two legacy features are absent from
the backlog entirely (local-unit **radius search**, and **birthday greetings**, the latter only
mentioned in passing).

`research/02-market-scan.md` — external features that will be asked for. Two of them should
influence the P0 design now, while it is cheap:

- **Accessibility (BFSG, since 28 June 2025, WCAG 2.1 AA)** — a direct argument for the CORE-03
  design where the host renders and plugins never return raw HTML, so accessibility is solved once.
- **Digital / hybrid general meetings (§32 BGB since 2023)** — CORE-07 should keep attendance, proxy
  and quorum separable from "registration", which costs nothing now and is awkward later.

E-invoicing (receipt obligation since 1 January 2025, issuing from 2027/2028), donation receipts,
online membership application, digital membership card, course booking and DATEV export are all
addable later as plugins **provided** the boundary in section 2 holds. That is the main practical
test of this proposal.

## 7. Pitch deck

`pitches/index.html` — 19 slides built for one job: setting priorities on comparable information.

- **11 full pitches**, one per specified feature: a one-line case, *Today* vs *After* vs *If it is
  not built*, the mockup thumbnail, and four scores — value, blocks-other-work, effort, risk.
- **15 compact cards** for the rest of the backlog, so the P2/P3 items can be ranked against the
  specified ones instead of being invisible.
- **A value/effort matrix** with bubble size for blocking power, a **do-not-execute slide**, and
  **one recommended cut**.

The scores are my assessment, not measurements — they live in `pitches/data.py` and are meant to be
argued with. Two of them I would defend hardest: CORE-03 has the lowest direct value and the highest
blocking power of anything on the list, and CORE-06 is the item I would cut first unless RENT-01 is
genuinely coming.

## 8. Do not execute

A plan that only lists what to build is half a plan. These are the things that must **not** happen,
each one either written into a specification as *out of scope* or observed in Clubmanager as a
mistake that was already paid for. Diagram page 11 carries the full version; the pitch deck has a
slide, because deciding not to do something is also a priority decision.

### Architecture rules

| # | Rule | Evidence it is needed |
|---|---|---|
| DNX-01 | No provider-specific code in the core — Matrix, Telegram, SMS, E-Post, Nextcloud, Listmonk, Traccar, postal are plugins behind generic contracts | Clubmanager keeps a `MatrixAgent` in the host and a 42 KB `NextcloudCalendarService` in `Services/` |
| DNX-02 | No core branch on a module id, and no plugin-specific DTO in the contract | This is the rule that keeps CORE-03 an investment rather than a growing switch statement |
| DNX-03 | No cross-schema access in either direction — read models and contract facades only | Stated explicitly in the Inventory 2.0 and Finance 2.0 specifications |
| DNX-04 | No silent fallback — a channel or storage backend applies the declared policy and reports it | Applies to CORE-04 and, since D6, to CORE-02 and CORE-07 |
| DNX-05 | No second plugin architecture — no out-of-process/gRPC host, no free Razor assemblies | Clubmanager's out-of-process attempt is explicitly not being ported |
| DNX-06 | No parallel template systems — exactly one versioned system, the simple renderer is replaced | Clubmanager runs a V1 template portal and V2 controllers side by side |
| DNX-07 | No filesystem access from controllers, no credential in an ordinary config value | Both Nextcloud config tables store the app password with a `// TODO: Verschlüsseln` |
| DNX-08 | No hard-coded state machine beside the workflow engine | Clubmanager built 5 workflow tables, then bypassed them with a 14 KB `RentalStateMachine` |

DNX-01, DNX-02, DNX-03 and DNX-06 can be enforced by an automated test rather than by review. The
other four need a reviewer who knows they exist — which is the point of writing them down.

### Do not port, even though it exists and works

- **Thousand-line controllers as a porting unit.** `MembersController` 277 KB, `InventoryController`
  128 KB, `FinanceController` 73 KB. Each domain gets services, commands/queries, thin endpoints and
  its own tests.
- **`ReferateController` as a set of hard-coded view names.** Departments become configurable
  organisation units (CORE-01) plus plugin navigation.
- **Keycloak group management as the local authority.** Local roles stay authoritative; external
  group mapping is IamOidc plugin work.
- **Debug controllers as productive routes.** Token and claims pages are development-only.
- **`ToolRental` as a second plugin.** Simple lending belongs in INV-01, commercial rental in
  RENT-01 — never both.

### Per feature, verbatim from the specifications

| Feature | Out of scope |
|---|---|
| CORE-01 | hard-coded department pages · Keycloak groups as authority · calendar/inventory/finance/editorial business data |
| CORE-02 | domain-specific image galleries · a mandatory cloud vendor or malware scanner · public CMS routing |
| CORE-03 | a second out-of-process plugin architecture · plugin-specific behaviour in core · uncontrolled Razor assemblies |
| CORE-04 | person-to-person chat · shipping any third-party provider in core · marketing campaign management |
| CORE-05 | campaign selection and scheduling · a scripting language in templates · provider-specific SMTP/postal/Listmonk config |
| CORE-06 | a visual BPMN engine or script execution · hard-coded rental/purchase seed workflows · domain forms and calculations |
| CORE-07 | Nextcloud entities or credentials in core · meeting minutes and decision-tree sessions · room/resource booking in release 1 |
| CORE-08 | shared login credentials · automatic legal guardianship assumptions · contribution or discount calculation in core |
| INV-01 | deposits, payments, rental orders, damage settlement, payouts · direct member/finance/org table access · a second Tool Rental |
| FIN-01 | general accounting, tax filing, payroll, banking-provider login · rental deposits before a generic contract · bank data in core member tables |
| NEWS-01 | postal delivery · editing core member records directly · a hard dependency on Listmonk |

### How to use this

Each item is phrased so it can be checked against a diff: *"does this change add a provider name to
the core?"* has a yes/no answer, *"keep the architecture clean"* does not.

Breaking one is allowed. Breaking one silently is not. If the pragmatic choice violates an item,
write down which one, why, and what would undo it — that is the difference between a deliberate
exception and the state Clubmanager arrived at over several years.

## 9. What I need from you

1. Does the boundary in section 2 hold — in particular D2 (no provider in the core), D5 (no
   cross-schema access) and D6 (Nextcloud as a plugin-supplied backend, not core support)?
2. Which domains must exist before Clubmanager can be switched off? (Section 4.)
3. Should CORE-09 API keys move earlier? Mobile inventory and any external client depend on it, and
   it currently sits in Phase 1 as a non-blocking item.
4. Keep or drop the two legacy features that are missing from the backlog — local-unit radius search
   and birthday greetings?
5. On D6: may the managed calendars be shared **read-only** with members in Nextcloud? That is the
   single most effective protection for the `X-CLUBGEAR-*` fields and the attendee list. If members
   need write access from their own CalDAV clients, the design needs an explicit repair path for
   dropped properties and I would plan that work into CORE-07.
