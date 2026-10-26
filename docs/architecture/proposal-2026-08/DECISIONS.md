# Decisions on the 2026-08 architecture proposal

The proposal in [`README.md`](README.md) put six decisions and five questions to the maintainer.
This page records the answers. Where an answer changes or narrows what the proposal said, that is
called out — the proposal itself is left unedited so the reasoning that led to the question stays
readable.

Answered 2026-08-31 by the maintainer. Open items are listed at the bottom.

---

## D1 — CORE-03 first, built demand-driven — **accepted, and started**

CORE-03 is being implemented. Two conditions attach to it:

- **Every contract change is spelled out in `CHANGELOG.md`.** Not "the page contract gained
  paging", but each added member and what an existing plugin sees.
- **The differences are documented**, in [`../../plugin-contract.md`](../../plugin-contract.md):
  what changed per version, what a plugin built against the previous version experiences without
  recompiling, and what it must do to use the new capability.

Increment 1 (contract `1.11.0`, sorting and paging) is delivered. The remaining increments —
detail views, typed field values, upload, download, and the JSON twin — follow the same shape:
one minor bump, one compatibility test, one documentation section each.

> **Note on "demand-driven".** No shipped plugin implements `IPluginPageProvider` today; the host
> renders it but nothing uses it. So the demand driving each increment comes from the *specified*
> needs of INV-01, FIN-01 and NEWS-01 rather than from code already in the tree. That is a weaker
> form of demand than D1 intends, and it is the main risk of building CORE-03 before INV-01. The
> mitigation is the rule already in D1: nothing enters the contract that cannot be tied to a named
> plugin requirement.

## D2 — Email is the only outbound channel in the core — **accepted**

Email stays in the core. Matrix, Telegram, SMS, E-Post and anything else become plugins.

**New plugin types are to be defined where they are needed.** For D2 that is a channel-provider
type: stable channel key, advertised capabilities, typed destination, configuration schema, health
check, send, and an authenticated status callback. It is not yet written — it belongs with CORE-04,
where there is a concrete consumer for it, per the D1 rule above.

## D3 — One file service, one template system, one state machine — **accepted as recommended**

No rental workflow in the core, no second renderer beside CORE-05, and simple lending inside
Inventory rather than a second rental plugin. A slower migration is accepted in exchange for never
running two systems in parallel.

## D4 — Keycloak overrides local permissions and data — **decided, and this reverses the proposal**

When Keycloak is installed, it is authoritative: it overrides local permissions, local data, and
the local equivalents generally. Local state is the fallback for an installation without it.

> **This contradicts the proposal.** D4 as written argued that the organisation unit is the scope
> for permissions and data and that *Keycloak is not* — that the core owns the model and Keycloak
> mirrors it. The decision inverts the direction of authority.
>
> Consequences that need working through before CORE-01 is built, and which are **not** yet
> resolved:
> - Precedence has to be explicit per concern. "Overrides" is clear for permissions; for member
>   data it raises whether a locally edited field is overwritten on next sync, and if so, whether
>   the local edit is rejected up front rather than silently lost.
> - The legacy system already carries five Keycloak group tables mirroring its local units. Making
>   Keycloak authoritative risks reproducing that mirror rather than removing it.
> - An installation without Keycloak must remain fully functional, so every override path needs a
>   defined local-only behaviour.
>
> I am flagging this rather than treating it as settled, because it is the most expensive decision
> here to reverse later.

## D5 — Plugins never touch core tables — **accepted, stricter than proposed**

A plugin gets **no** direct access to a core table, ever. All access goes through the API and the
contracts. This is the proposal's D5 with the exception surface removed.

Enforcement is a build-time and runtime concern, not a convention: the contract exposes read models
(`MemberReadModels`) and the host mediates every call through `IPluginRuntimeAdapter`. A test
asserting that no plugin assembly references the core's `DbContext` belongs with this decision;
it does not exist yet.

## D6 — Files and calendars are pluggable stores — **accepted as a plugin, with shared settings**

Nextcloud is a plugin. The Nextcloud attachment lives in that plugin, not in the core. The core
gains the ports only.

**Added by this decision:** settings are stored centrally so that several plugins can read one
configuration rather than each carrying its own copy of the same Nextcloud host, credentials and
root path. That is a new core capability the proposal did not describe — a shared, scoped
plugin-settings store with an access rule deciding which plugin may read which section.

Not yet designed. It needs at minimum: who may write a shared section, who may read it, how
secrets in it are protected, and what happens to a plugin's view of it when the owning plugin is
uninstalled.

---

## The domain scope (§9 question 2) — answered, and it does not shrink the programme

Required before Clubmanager can be switched off: **INV-01, FIN-01, RENT-01, POST-01, CMS-01,
GPS-01, CHAT-01, ORGVIEW-01, NC-01**, plus birthday greetings and local-unit radius search.
Dropped: NEWS-01, EDIT-01, DOC-01, TREE-01.

That is 11 of 12 domains, and it pulls in **all eight** CORE building blocks — CORE-06 through
RENT-01, CORE-07 through NC-01's calendar-store half.

| Block | Effort |
|---|---|
| CORE-01…08 | 97–188 PT |
| The 11 domains | 150–306 PT |
| **Total** | **247–494 PT** |

*(T-shirt sizes from the compact list mapped onto the scale the detailed list uses: M = 7–14,
L = 13–25, XL = 26–50+.)*

The proposal put the **entire** backlog at "400+ PT". This selection sits in the same range. It is
recorded as decided, but it should be read alongside §4: it re-ordered the programme rather than
reducing it, and for one person it remains a multi-year effort.

A cheaper cut exists if the switch-off date matters more than completeness: INV-01, FIN-01 and
RENT-01 with the CORE blocks they need are ~150–260 PT. GPS-01, CHAT-01, ORGVIEW-01, CMS-01, NC-01
and the two legacy leftovers total ~60–120 PT, depend on almost nothing, and can follow the
switch-off without touching the architecture.

## CHAT-01 — folded into the core as one message substrate

CHAT-01 does not become a plugin, and it does not become a second messaging system either. Chat
moves into the core **on the same substrate as CORE-04**.

The duplication it avoids: a notification inbox and a chat are the same structure seen from two
directions. Built separately, the core would carry two inboxes, two read states, two unread
counters and two "you have a new message" paths, and a member would have to look in two places.

The shape, as implemented in slice 1:

- One `MessageThread` with a free-form `Kind`. `notification` is a thread the system posts to and
  a member reads; `chat` is a thread its participants post to.
- Participation is the access rule and carries the read state. `CanPost` separates the two kinds:
  a notification recipient reads but cannot reply.
- A plugin attaches its own conversation by starting a thread with its module id and business key —
  RENT-01's rental talk is a thread, not a third system. This is what makes D5 workable here: the
  plugin never touches the message tables, it goes through a host-provided facade.

> **Correction, 2026-09-02.** The last sentence originally read "it calls `IMessageService`". No
> plugin can: `IMessageService` lives in `ClubGear.Services.Abstractions`, which is on
> `PluginRuntimeAdapter`'s forbidden-namespace list, and it is not exposed on
> `Contracts/Plugin/IPluginHostContext.cs`. The facade this paragraph assumes does not exist yet —
> it is backlog item CORE-13 (`docs/backlog/core-platform.md`). Until then, a plugin-owned
> conversation is unimplementable, and the paragraph describes intent rather than a shipped
> capability.

Found while building it: `InAppNotificationChannel` was a stub that logged and returned success, so
every in-app notification was silently discarded while the sender was told it had been delivered.
There was no member-facing inbox at all. The channel now writes into the substrate, and reports
failure when it cannot resolve a recipient.

## CORE-09 — delivered

API keys and service accounts are implemented; see `docs/api-keys.md`. Still missing: the admin UI,
an endpoint that accepts the scheme, and per-key rate limiting.

---

## Still open

From §9 of the proposal, unanswered:

1. ~~Which domains must exist before Clubmanager can be switched off?~~ **Answered above** — but
   the answer keeps the programme at 247–494 PT, so the trade-off §4 raised is unresolved rather
   than settled.
2. ~~Should CORE-09 (API keys) move earlier?~~ **Answered: yes, and it is built.**
3. ~~Keep or drop the two legacy features missing from the backlog?~~ **Answered: keep both.**
4. On D6: may managed calendars be shared **read-only** with members in Nextcloud? Write access
   from members' own CalDAV clients requires an explicit repair path for dropped `X-CLUBGEAR-*`
   properties, and that work would have to be planned into CORE-07.

Raised by the answers above and needing resolution before the affected work starts:

5. D4 precedence rules per concern, and the local-only fallback behaviour.
6. D6 shared-settings design: ownership, read scope, secret handling, uninstall behaviour.
7. D2 channel-provider contract shape — deferred to CORE-04 by the D1 demand-driven rule.
