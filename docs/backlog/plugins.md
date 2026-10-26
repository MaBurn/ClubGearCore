# Plugin-owned backlog

Audience: Maintainers and plugin authors asking "who owns this, and where is the detail?"
Scope: Every plugin-owned item — service/provider plugins and functional plugins — each with a named owner repository
Last-Validated: 2026-10-20
Source-Commit: 9b527ff

Owner for every item on this page is a **named plugin repository**: the change lands there, and
that repository's maintainer merges it. Nothing here is owned by `core`. The classification rules
R1–R8, the status and priority vocabularies, the ID scheme and the crosswalk from the retired June
2026 ID space are in [`README.md`](README.md).

**Each ID is *defined* exactly once, in the tables below.** Any other occurrence of an ID on this
page is a cross-reference. Core items are **not** defined here; where one is needed — a seam a
plugin plugs into, or a capability it must not duplicate — it is named by its capability and linked
to [`core-platform.md`](core-platform.md) or [`core-capabilities.md`](core-capabilities.md), which
is where those IDs are defined.

## These entries are deliberately thin

Each entry is a one-line summary, a status, a priority, an owner repository and a pointer. It
carries **no acceptance criteria and no line-referenced paths into plugin source**, because that
detail goes stale the moment the plugin ships — this consolidation absorbed a source list that
recorded a Finance version three releases behind reality, which is exactly the failure mode being
avoided. The single exception is the carried `kassenwart` residue at the end of this page, which is
evidence-bearing on purpose because it spans the core and a plugin and neither side can see the
whole of it.

**The `Pointer` column names where the detail lives.** For a plugin that exists, that is a file in
its own repository. For a plugin that does not exist yet there is no repository to point into, so
the pointer names the core seam that must land first and where that seam is defined; the detail
moves into the new repository the moment R7 creates it.

**Owner repositories follow the naming already in `.gitmodules`** — `clubgear/plugin-<name>` — so
the five that exist are `plugin-carinfo`, `plugin-inventar`, `plugin-servicebook`, `plugin-iamoidc`
and `plugin-finance` (`.gitmodules:3,6,9,12,15`). A repository marked *(new)* has not been created;
R7 governs its creation.

**Prefixes introduced here.** `README.md` enumerates the prefixes inherited from the retired source
lists. Five groups of items those lists named but never numbered get prefixes on this page, under
the same one-prefix-per-domain rule: `CHAN-xx` (channel providers), `STOR-xx` (storage providers),
`LIST-01` (the Listmonk adapter), `APIKEY-01` (the extracted API-key plugin) and `CAR-01` / `SB-01`
/ `IAM-01` (residue in shipped repositories).

## Service / provider plugins

Every item here is an R1 vendor binding, and every one of them is a consumer of the single provider
contribution seam defined in [`core-platform.md`](core-platform.md). None of them can be built
before that seam exists; that is the whole reason the seam outranks them on priority.

| ID | Summary | Status | Priority | Owner repository | Pointer |
|---|---|---|---|---|---|
| CHAN-01 | Matrix as an outbound and inbound notification channel. | `open` | P1 | `plugin-matrix` *(new)* | Provider seam and lifecycle in [`core-platform.md`](core-platform.md); the inbox and preference model it delivers into is CORE-04 in [`core-capabilities.md`](core-capabilities.md). |
| CHAN-02 | Telegram as an outbound notification channel. | `open` | P3 | `plugin-telegram` *(new)* | Provider seam in [`core-platform.md`](core-platform.md). |
| CHAN-03 | WhatsApp as an outbound notification channel. | `open` | P3 | `plugin-whatsapp` *(new)* | Provider seam in [`core-platform.md`](core-platform.md). |
| CHAN-04 | SMS as an outbound notification channel for time-critical messages. | `open` | P2 | `plugin-sms` *(new)* | Provider seam in [`core-platform.md`](core-platform.md). |
| STOR-01 | S3-compatible object storage that supersedes and disables the core's local backend. | `open` | P1 | `plugin-s3` *(new)* | The R3 supersede case: provider selection and precedence in [`core-platform.md`](core-platform.md); the capability it replaces is CORE-02 in [`core-capabilities.md`](core-capabilities.md). |
| STOR-02 | Nextcloud/WebDAV as a storage backend, same supersede rule as STOR-01. | `open` | P2 | `plugin-nextcloud-storage` *(new)* | As STOR-01; credentials belong in the plugin settings and secret store defined in [`core-platform.md`](core-platform.md). |
| NC-DOC-01 | Nextcloud document connector: WebDAV connection, path policy, metadata sync, streamed download, and a subject-neutral document reference other plugins consume. | `open` | P2 | `plugin-nc-documents` *(new)* | Distinct from STOR-02: that one is a storage backend, this one is a document source. Provider seam and secret store in [`core-platform.md`](core-platform.md). |
| CALDAV-01 | Nextcloud/CalDAV calendar connector: discovery, mapping to organisational units, incremental sync, conflict and time-zone rules, manual resync. | `open` | P1 | `plugin-caldav` *(new)* | Implements the ports of CORE-07 in [`core-capabilities.md`](core-capabilities.md); blocked on CORE-01 there. |
| POST-01 | Postal provider: single and batch letters, PDF from core templates, postage selection, provider status, history, test mode. | `open` | P2 | `plugin-postal` *(new)* | Renders through the one central template system, CORE-05 in [`core-capabilities.md`](core-capabilities.md) — R2, no second renderer. |
| LIST-01 | Listmonk adapter for list synchronisation and delivery, as an optional provider behind NEWS-01. | `open` | P3 | `plugin-listmonk` *(new)* | Optional: NEWS-01 below must work over core SMTP/IMAP without it. |
| GPS-01 | Traccar connector: tracker types, device assignment, initialisation commands, provisioning. | `open` | P3 | `plugin-traccar` *(new)* | R8's worked example — one consumer, therefore a plugin. May link devices to vehicles by stable reference, never by table coupling to `plugin-carinfo`. |
| APIKEY-01 | The API-key and service-account scheme, extracted from the core into a plugin. | `open` | P2 | `plugin-apikeys` *(new)* | The extraction, its permanent bootstrap ordering and its data decision point are CORE-09 in [`core-capabilities.md`](core-capabilities.md). This entry exists so the receiving repository has an ID; it must not start before CORE-09 releases it. |

Twelve entries. `CHAT-01` is **not** one of them: the chat capability shipped in the core and its
record is corrected in [`core-capabilities.md`](core-capabilities.md). A Matrix binding for it is
CHAN-01 above.

## Functional plugins

| ID | Summary | Status | Priority | Owner repository | Pointer |
|---|---|---|---|---|---|
| INV-01 | Inventar 2.0: warehouses and hierarchical locations, stock and corrections, categories, images, history, barcodes and a reference catalogue, location-scoped rights, mobile API, search and paging, and simple lending. | `partial` | P1 | `plugin-inventar` | `plugins/Inventar/CHANGELOG.md`. The base plugin ships; everything listed is the increment. The old *ToolRental* feature is absorbed here as simple lending, not duplicated as a second plugin. |
| FIN-01 | Finance 2.0: the residue after the 1.9.0 release — see the detail section below. | `delivered` | P1 | `plugin-finance` | `plugins/Finance/CHANGELOG.md` (`2.0.0`). Candidate follow-on work in [`inbox/plugin-finance/next-steps.md`](inbox/plugin-finance/next-steps.md). |
| NEWS-01 | Mass mail and newsletter: recipient selection, preview, scheduled and batched delivery, attachments, delivery status, bounce handling, invalid addresses, opt-in/opt-out and list reconciliation. | `open` | P1 | `plugin-news` *(new)* | Templates come from CORE-05 and delivery from the core channels, both in [`core-capabilities.md`](core-capabilities.md). The "letter to invalid email addresses" case uses POST-01 rather than its own provider code. |
| RENT-01 | Marketplace and rental: listings and availability, requests and orders, owner/renter roles, deposits and payments, liability notice, handovers, photos, damages, deductions and refunds, condition history, order-scoped conversation. | `open` | P2 | `plugin-rental` *(new)* | Built on inventory references from INV-01. **Its workflow is registered as a definition against the generic engine, CORE-06 in [`core-capabilities.md`](core-capabilities.md) — never a rental state machine in the core** (R4). Payments only through a Finance contract or event, never through Finance tables. |
| TREE-01 | Decision trees, technical help and meetings: tree and node editor, activation validation, guided sessions, answers, results, todos, attachments. | `open` | P2 | `plugin-decisiontrees` *(new)* | *TechHelp* and *Treffen* are configurations of this one generic plugin, not separate implementations. Roles such as member and coordinator become plugin permissions. |
| EDIT-01 | Editorial and publication planning: media, categories, submissions, images, issues, ordering, assignment, editorial roles and approvals, notifications. | `open` | P2 | `plugin-editorial` *(new)* | *BuKu* and *Newsletter* are configured media instances, not hard-coded permission prefixes. NC-DOC-01 and NEWS-01 are optional integrations, not prerequisites. |
| DOC-01 | Document compendium: upload, categories and manufacturers, metadata, search and filter, protected PDF viewing, watermarking. | `open` | P2 | `plugin-documents` *(new)* | Document bytes live in the core file service, CORE-02 in [`core-capabilities.md`](core-capabilities.md); only metadata lives in the plugin schema. |
| CMS-01 | Public content: editable content pages and images with an explicit publish/preview rule. | `open` | P3 | `plugin-cms` *(new)* | Sign-in, imprint and privacy routing stay in the core. Freely editable content is the optional part. |
| ORGVIEW-01 | Organigram view: visualisation and ordering of the organisational structure, optional manual presentation nodes, preview. | `open` | P2 | `plugin-orgview` *(new)* | A view only. CRUD of the structure itself is CORE-01 in [`core-capabilities.md`](core-capabilities.md). External group mapping is not taken on here; that is the IAM plugin's job — see IAM-01. |
| BDAY-01 | Birthday and anniversary greeting automation with eligibility rules, preview, idempotent scheduling and dispatch history. | `open` | P2 | `plugin-birthday` *(new)* | Retained by the architecture decision. Uses CORE-05 events/templates and CORE-04 delivery; it does not add a second scheduler or channel stack. |

Ten entries. No ID defined on this page is defined in
[`core-platform.md`](core-platform.md) or [`core-capabilities.md`](core-capabilities.md).

## Residue in the shipped plugin repositories

Five plugins are declared as submodules today: CarInfo, Inventar, ServiceBook, IamOidc and Finance
(`.gitmodules`). Inventar's and Finance's residue is INV-01 and FIN-01 above; the other three carry
this much, and no more.

| ID | Summary | Status | Priority | Owner repository | Pointer |
|---|---|---|---|---|---|
| CAR-01 | Check the remaining vehicle and title fields against actual demand before porting any of them. | `open` | P3 | `plugin-carinfo` | `plugins/CarInfo/CHANGELOG.md`. This is the reference profile plugin and is otherwise considered complete. |
| SB-01 | Bring the service book to API and self-service parity with the rest of the plugin surfaces. | `open` | P2 | `plugin-servicebook` | `plugins/ServiceBook/CHANGELOG.md`. Parity is reached through the host-side surface work, CORE-03 in [`core-platform.md`](core-platform.md); it depends on CarInfo. |
| IAM-01 | External group and role mapping from the identity provider, and the road from `0.1.0` to a first stable release. | `open` | P2 | `plugin-iamoidc` | `plugins/IamOidc/CHANGELOG.md`. Local roles stay the authority; the IdP's groups are mapped onto them, never the reverse. |

## Contract 1.13.0 adoption — one item per shipped plugin

Core contract `1.13.0` (CORE-18) removed the parameterless construction contract. A contribution
now has exactly one public constructor, `T(IPluginHostContext)`, and the host constructs it inside a
scope it owns. `ContractVersion.MinimumSupported` moved to `1.13.0` with `Current`, so **every
plugin below that floor stops activating** and plugin administration shows it as incompatible with
a version message. The rule and the migration are in
[`../plugin-contract.md`](../plugin-contract.md).

Each item below is the same shape of work in a different repository: adopt the canonical
constructor on every contribution type, raise `requiredCoreVersion` in the manifest to `>=1.13.0`,
re-tag and re-package. **The core does not edit any submodule for this**; that is why these are
plugin-owned items rather than a core task.

The module entry point — the `IPluginModule` type named by `entryPoint` — is the one documented
exception and keeps its parameterless constructor.

**This adoption is also why `tests/ClubGear.ArchitectureTests` can stop compiling without any core
commit.** The test project `ProjectReference`s plugin source directly, so the moment a plugin's
*local working tree* — not its tagged release, not core's recorded gitlink — adopts the canonical
constructor or renames a permission, every core test file still written against the old shape
fails to build. Hit in practice 2026-10-03 against `plugins/CarInfo` and `plugins/Finance`; tracked
as [`CORE-28`](core-platform.md#core-28--architecture-tests-coupled-to-a-plugins-current-checkout-not-its-contract-floor)
in `core-platform.md`, not here, because the fix (if any) is core's test project, not either
plugin's adoption work.

| ID | Summary | Status | Priority | Owner repository | Pointer |
|---|---|---|---|---|---|
| CAR-02 | Adopt contract 1.13.0: canonical `T(IPluginHostContext)` constructor on all five provider types, manifest floor to `>=1.13.0`, re-tag and re-package. | `partial` | P1 | `plugin-carinfo` | Source adopted 2026-09-09 (`5387af3`, `9a88dac`, `ca3e7e3`); **release not cut**, so the plugin still does not activate. See the adoption note below. |
| FIN-02 | Adopt contract 1.13.0: canonical constructor on all five provider types, manifest floor to `>=1.13.0`, re-tag and re-package. | `delivered` | P1 | `plugin-finance` | Delivered 2026-10-03 (`plugins/Finance` commit `eae6999`): canonical `T(IPluginHostContext)` constructor on all five provider types, `plugin.json`/`FinancePluginModule.cgcs` both at `requiredCoreVersion >=1.13.0`, version `2.0.0` built to `dist/Finance-2.0.0.zip` (signed). Independent of the FIN-01 feature residue. |
| SB-02 | Adopt contract 1.13.0: canonical constructor on all three provider types, manifest floor to `>=1.13.0`, re-tag and re-package. | `open` | P1 | `plugin-servicebook` | As CAR-02. |
| INV-02 | Adopt contract 1.13.0: canonical constructor on both provider types, manifest floor to `>=1.13.0`, re-tag and re-package. | `delivered` | P1 | `plugin-inventar` | Delivered 2026-10-08 (`plugins/Inventar` commit `0d5f476`, tag `v1.0.0`): canonical `T(IPluginHostContext)` constructor on `InventarPageProvider` and `InventarAdminPanelProvider`, `plugin.json`/`InventarPluginModule.cgcs` both at `requiredCoreVersion >=1.13.0`, built, signed and packaged to `dist/Inventar-1.0.0.zip`. Core's gitlink for `plugins/Inventar` already points at this commit, so the plugin activates against the running core. |
| IAM-02 | Adopt contract 1.13.0: canonical constructor on all three identity providers **and on the shared `OidcIdentityProviderBase` they derive from**, manifest floor to `>=1.13.0`, re-tag and re-package. Its own test project instantiates providers directly and has to move with them. | `open` | P1 | `plugin-iamoidc` | As CAR-02. The base class makes this the largest of the five: the constructor chain changes in one place and every derived provider follows. |

Five entries, one per shipped submodule. They are deliberately *not* folded into CAR-01, SB-01 and
IAM-01: those are feature residue on an open-ended schedule, while these five block activation
outright and are finished the moment the plugin loads again.

### CAR-02 — adopted in source, release outstanding

`partial`, not `delivered`, and the distinction is the point: the item says "re-tag and re-package",
and neither has happened. All five contribution types carry the canonical constructor, both manifest
sites declare `1.0.10` against `>=1.13.0`, and the repository's own `CHANGELOG.md` and `README.md`
record the release and its compatibility break. But **no tag exists and no package was built**, so
from the core's point of view nothing changed — CarInfo still does not activate.

What the first adoption established, for the four that follow:

- **The constructor shape is a public auto-property**, `public IPluginHostContext HostContext { get; }`,
  assigned from `T(IPluginHostContext)`. Method bodies are untouched; the interface members keep
  receiving their own per-call context, which is what makes the adoption behaviour-neutral. The
  shape matters: `docs/plugin-contract.md`'s canonical-constructor example previously showed a
  private field that was assigned and never read, diverging from this shape — see CORE-22 item 5,
  now corrected.
- **Both declarations must move**, `plugin.json` *and* the compiled in-code manifest. Editing only
  the JSON yields a plugin that installs and never activates. See CORE-24.
- **Providers that are not load-gated convert too.** Admin-panel and page providers fail *open* — an
  empty lease at call time rather than a load rejection — so skipping them silently drops the
  surface instead of reporting anything.
- **The release is a deliberate, separate act.** Decide the signing key *before* tagging: tagging
  triggers CI signing, so unless `PLUGIN_SIGNING_PRIVATE_KEY` matches the key that signed the
  previous release, `signer-public.pem` rotates and every consumer pinning it must re-trust.

IAM-02 inherits one question this precedent does not answer: its three identity providers derive
from a shared `OidcIdentityProviderBase`, and a property on a base class plus constructors on the
derived types is a different shape question — the activator permits exactly one public constructor
per type.

The submodule *wiring* defects found while validating this page — one remote pinned to a local
path, and one plugin checked out as a standalone clone instead of a submodule — are **not** listed
here. They are core repository work under R7 and are defined in
[`core-platform.md`](core-platform.md). `plugins/ServiceBook` was checked and is correctly wired.

## FIN-01 — Finance 2.0, with its staleness recorded

**Status `delivered` · Priority P1 · Owner `plugin-finance`.**

The August 2026 list filed "Beitragslauf mit Familien-/Mitgliedstypregeln" and the pain.008 export
as open (`TodoFeatures.md:291-308`). **Both shipped**, in Finance `1.9.0`
(`plugins/Finance/CHANGELOG.md:24`), together with fee-demand lifecycle and dunning and the SEPA
creditor and mandate configuration. They are not carried forward.

The four items that were still open after `1.9.0` shipped in Finance `2.0.0` (2026-10-03,
`plugins/Finance/CHANGELOG.md`):

1. Statement import and matching — CAMT.053 and MT940, hand-rolled parsers, scoring-based automatic
   matching against existing bookings with manual match/unmatch.
2. The treasurer UI and its reports, including a cash-audit (Kassenprüfung) CSV export, reusing the
   file-download transport the pain.008 export established.
3. A chart of accounts with income/expense categories and a simple per-category budget with a
   Soll/Ist comparison.
4. The role split: `treasurer.read`/`treasurer.write` replace the single `kassenwart.access`
   permission in every check the plugin makes. The **core**-side half of this item is tracked
   separately below, under the `kassenwart` → `treasurer` rename.

Candidate follow-on work (not part of this item, not yet prioritized) is collected in
[`inbox/plugin-finance/next-steps.md`](inbox/plugin-finance/next-steps.md).

**Three version records used to disagree; one was corrected.** `plugins/Finance/plugin.json`
says `1.9.0` — that is the plugin, and it wins. `plugins/aktivierung.md:48,50` now agrees (it no
longer publishes a copy of the version at all; it points at `plugin.json` and `dist/` instead), and
the retired `backlog/finance-plugin.md` said `1.6.3`. `aktivierung.md` is *core* documentation, so
correcting it was core work and belongs to the documentation integrity sweep in
[`core-platform.md`](core-platform.md), not to this page. Recorded here only so a reader of FIN-01
knows which number to trust.

## Carried residue from the retired `backlog/finance-plugin.md`

`backlog/finance-plugin.md` (2026-07-10, German, sections A–E) is retired. Only **section A** was
still true at HEAD, and it is carried here in full because it is the one item that spans the core
and a plugin: neither repository's own backlog can see all of it.

### The `kassenwart` → `treasurer` rename is half done

**Status `partial` · Priority P2 · Owner `plugin-finance`, with the remainder in `core`.**

Route, navigation and page keys were renamed to `treasurer` some time ago. As of Finance `2.0.0`
(2026-10-03) the plugin-side permission key is renamed too:

- **Renamed, plugin side.** `FinancePermissions.TreasurerWrite`/`TreasurerRead` replace
  `clubgear.plugin.finance.kassenwart.access` in every real permission check the plugin makes
  (`plugins/Finance/FinanceProviders.cgcs`), and `plugin.json`'s permission list now declares
  `treasurer.write`/`treasurer.read` instead of `kassenwart.access`.
- **Kept as a compile-time-only shim.** `FinancePermissions.KassenwartAccess` still exists, marked
  `[Obsolete]` and referenced by nothing functional, solely because two **core** files reference it
  at compile time (see below). Removing it is part of the core-side follow-up, not optional cleanup.
- **Not renamed, core side.** The permission key is still `clubgear.plugin.finance.kassenwart.access`
  in the core role seed (`Services/Core/SeedTasks/RolePermissionSeedTask.cs:34`) and in core
  architecture tests (`PluginNavEntryServiceTests.cs`, `FinanceSlice7ManualVerifyKassenwartPageTests.cs`).
  This was deliberately kept out of the `plugin-finance` 2.0.0 iteration: it is a **core** change and
  ADR 0001 requires explicit user approval before touching core, which was not sought this round.

The three open questions from the retired note are now decided, pending that core-side work:

1. The split is **not** deliberate design — it is sequencing. The permission id does not need to stay
   stable in the database; a one-time data migration is the intended fix (point 3).
2. No permanent legacy alias: the `KassenwartAccess` shim above is removed the same time the core
   seed task and its tests are updated, as a single core-side change.
3. A migration is needed: any stored role-permission row referencing
   `clubgear.plugin.finance.kassenwart.access` must be rewritten to the new `treasurer.write`/
   `treasurer.read` keys when that core change lands.

Design rationale for the plugin-side half is recorded in `plugins/Finance/adr/finance-v2.adr.md`.

### Sections B, C and D are not carried, because they are provably stale

| Section | Claim | Why it is stale |
|---|---|---|
| B | Version and documentation diverge; `plugin.json` is at `1.6.3`. | `plugins/Finance/plugin.json` is at `1.9.0`. The remaining documentation drift is recorded under FIN-01 above and owned by the core documentation sweep. |
| C | The work is uncommitted and unverified. | It is in history: `1.8.0` and `1.9.0` are released sections of `plugins/Finance/CHANGELOG.md`. |
| D | Seven known failing tests, per `AGENTS.md`. | That note was itself stale and has been removed; the suite is green. |

Section **E** was a pointer rather than content, and survives as exactly that: the FIN-01 pointer to
[`inbox/plugin-finance/next-steps.md`](inbox/plugin-finance/next-steps.md).
