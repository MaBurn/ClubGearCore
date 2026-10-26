# Core capabilities backlog

Audience: Maintainers and architects working on the functional core — members, messages, files, templates, workflows
Scope: Core-owned items in the functional capability layer, plus the two records this consolidation had to correct
Last-Validated: 2026-09-01
Source-Commit: d89d29b

Owner for every item on this page is `core`: the change lands in this repository. The
classification rules R1–R8, the status and priority vocabularies and the crosswalk from the retired
June 2026 ID space are in [`README.md`](README.md).

**Each ID is *defined* exactly once, in the item table below and its matching detail section.**
Any other occurrence of an ID on this page is a cross-reference to that single definition.
Platform and host items are **not** defined here; where one is needed, it is named by its capability
and linked to [`core-platform.md`](core-platform.md), which is where those IDs are defined.

This page holds what the core keeps *as a capability*, in the R1 sense: the inbox, the file service,
the template system, the workflow engine and the member-facing domain model. Every vendor binding
that plugs into one of them — Matrix, WhatsApp, Telegram, SMS, S3, Nextcloud, Listmonk, a postal
carrier — is a plugin and lives in [`plugins.md`](plugins.md).

Two records had to be corrected rather than quietly carried forward, and both corrections are on
this page with both sides recorded: **CHAT-01**, which the August 2026 list filed as a plugin while
the opposite shipped, and **CORE-09**, whose original "authentication is a security boundary of the
host" reasoning no longer decides the question.

## Items

| ID | Title | Status | Priority | Owner | Source | Classification rationale | Evidence |
|---|---|---|---|---|---|---|---|
| CORE-01 | Organisation structure and sub-units | `open` | **P0** | `core` | TodoFeatures, `P0-OrganizationStructure` | R8: members, roles, events and inventory all need assigning to a unit — the host needs it and so do several plugins. | `Data/ApplicationDbContext.cs:14-30`, `Models/MembershipType.cs`, `Models/AppPermission.cs` |
| CORE-02 | Secure file and media storage | `open` | **P0** | `core` | TodoFeatures, `P0-SecureFileStorage` | R3: local storage is the core **default**, and a storage plugin must be able to supersede and disable it. | `Services/Abstractions/IProfileImageStorageService.cs:3,5,11`, `Services/Core/ProfileImageStorageService.cs` |
| CORE-04 | Notification inbox, status and preferences | `partial` | P1 | `core` | TodoFeatures, `P0-NotificationInbox`, proposal-2026-08 | R1: the inbox, recipient resolution and delivery are the capability; every vendor channel is a plugin. | `Models/Messaging.cs:30,53,74`, `Services/Abstractions/IMessageService.cs:13`, `58c7988`, `d89d29b` |
| CORE-05 | Versioned templates and communication events | `partial` | **P0** | `core` | TodoFeatures, `P0-VersionedTemplates` | R2: there is exactly one template system and it is central; no plugin ships a second renderer. | `Services/Abstractions/ITemplateRenderer.cs:3,5`, `Services/Core/SimpleTemplateRenderer.cs:5,17-19`, `Services/Abstractions/IMessageComposer.cs:3,5` |
| CORE-06 | Generic workflow and state-machine foundation | `open` | P1 | `core` | TodoFeatures, `P1-WorkflowEngine` | R4: one generic engine in the core; every concrete workflow is declared by a plugin. | `Data/ApplicationDbContext.cs:14-30`, `Contracts/Plugin/IPluginMigration.cs:3,7,11,16,33` |
| CORE-07 | Calendar and event base model | `open` | P1 | `core` | TodoFeatures, `P1-CalendarAndEvents` | R1: events, recurrence and participation are the capability; CalDAV/Nextcloud synchronisation is the vendor binding. | `Data/ApplicationDbContext.cs:14-30` |
| CORE-08 | Family and household relationships | `partial` | P1 | `core` | TodoFeatures, `P1-FamilyRelationships` | R8: membership, self-service and more than one plugin read the relationship — the host needs it and two independent consumers need it. | `Models/Member/MemberHierarchyViewModel.cs:13-19,25`, `Models/MemberMetadataValue.cs` |
| CORE-09 | API keys and service accounts — extract into a plugin | `partial` | P2 | `core` | TodoFeatures, `7c3f802`, user decision 2026-09-01 | R1 + R6: a token scheme for external clients is a binding a plugin can own; the cookie boundary it boots behind cannot be. | `Services/Authorization/ApiKeyAuthenticationHandler.cs:45`, `Program.cs:61,82`, `docs/api-keys.md:72,77,81` |
| CORE-10 | Branding and navigation | `open` | P2 | `core` | TodoFeatures | R8: only the host can carry the brand and merge every navigation entry, core and plugin alike. | `Services/Abstractions/IPluginNavEntryService.cs`, `Contracts/Plugin/PluginNavEntry.cs`, `Models/SystemConfigEntry.cs` |
| CORE-11 | Translation management | `open` | P2 | `core` | TodoFeatures | R8: the host itself needs it — core pages, the admin area and plugin navigation must all resolve through one catalogue. | `Views/SelfService/Profile.cshtml:214`, `Views/Members/Edit.cshtml:370` |
| CORE-12 | Operations, audit and changelog views | `partial` | P3 | `core` | TodoFeatures | R8: operational data and the list of installed versions belong to the host; no plugin can see across all of them. | `Services/Abstractions/IAuditLogService.cs`, `Services/Abstractions/IEventLogService.cs`, `Models/AuditLogEntry.cs`, `Models/SystemEventLog.cs` |
| CORE-26 | Local-unit radius search and export | `open` | P2 | `core` | proposal-2026-08 retained legacy feature | R1/R8: it queries the core organisation/member model and is useful independently of any vendor integration. | `docs/architecture/proposal-2026-08/DECISIONS.md:98-100`, `docs/architecture/proposal-2026-08/research/01-clubmanager-inventory.md:27-37` |
| CHAT-01 | Internal messages and chat | `superseded` | — | `core` | TodoFeatures `:83`, proposal-2026-08 `DECISIONS.md:123-145` | R1: the chat capability shipped in the core on the message substrate; a Matrix or other external binding is a service plugin. | `docs/architecture/proposal-2026-08/DECISIONS.md:123-145`, `Models/Messaging.cs:30,53,74`, `58c7988`, `d89d29b` |

Thirteen entries: twelve `CORE-xx` capabilities plus the corrected `CHAT-01` record. No ID defined on
this page is defined in `core-platform.md` or `plugins.md`. `CHAT-01` carries no priority because a
superseded item schedules nothing; it is kept so the reasoning stays findable.

## CORE-02 — Secure file and media storage

**Status `open` · Priority P0 · Owner `core`.**

There is no general file service at HEAD. The only storage abstraction is single-purpose:
`IProfileImageStorageService` (`Services/Abstractions/IProfileImageStorageService.cs:3`) saves a
profile image for a member id (`:5`) and deletes one by path (`:11`). It has no metadata, no size or
MIME limit, no authorisation check, no download policy and no backend choice.

The capability the core owes: a storage service with metadata, size and MIME limits, safe filename
construction, an authorisation check on every read, an explicit download-versus-inline policy,
deletion, and a pluggable backend. Plugins store a **reference**, never a path under `wwwroot`.

R3 decides the shape: the local backend stays in the core as the default, and an S3 or Nextcloud
plugin must be able to **supersede and disable** it. That is not achievable with implicit DI
last-wins registration, so this item cannot be finished before the provider seam and its
admin-selected precedence rule land — both are defined in
[`core-platform.md`](core-platform.md).

## CORE-04 — Notification inbox, status and preferences

**Status `partial` · Priority P1 · Owner `core`.**

**Delivered.** Two slices shipped and are verifiable at HEAD:

- `58c7988` — *one message substrate for notifications, chat and plugins*. `MessageThread`
  (`Models/Messaging.cs:30`), `Message` (`:53`) and `MessageParticipant` (`:74`) are core entities on
  `ApplicationDbContext` (`Data/ApplicationDbContext.cs:28-30`), driven through `IMessageService`
  (`Services/Abstractions/IMessageService.cs:13`). A notification is a thread the system posts to;
  a chat is a thread its participants post to; a plugin attaches its own conversation as a thread
  carrying its module id and business key. Participation is the access rule and carries the read
  state.
- `d89d29b` — *member inbox over the message substrate*. `Controllers/Messages/Messages_Controller.cs`
  renders one inbox with one unread badge for notifications, chats and plugin threads alike.

**Open residue**, and it is narrow but real:

1. **Channel preferences do not exist.** The word "preference" appears nowhere in `Services/`,
   `Models/` or `Controllers/`. `NotificationService.NotifyAsync` resolves the channel from the
   *message* (`Services/Core/NotificationService.cs:56`), never from the recipient, so a member has
   no say in how they are reached.
2. **Per-user channel activation does not exist.** The two booleans that look like it —
   `Member.NotifyViaEmail` (`Models/Member.cs:85`) and `Member.NotifyViaMatrix` (`:87`) — are
   editable in the UI (`Views/Members/Edit.cshtml:369-370`) and shown in self-service
   (`Views/SelfService/Index.cshtml:18`), but no dispatch path reads either one. They are settable
   flags with no effect.
3. **`NotifyViaMatrix` is an R1 violation in the core model.** It names a vendor on a core entity
   while only two channels exist — `SmtpEmailNotificationChannel`
   (`Services/Channels/SmtpEmailNotificationChannel.cs:9`) and `InAppNotificationChannel`
   (`Services/Channels/InAppNotificationChannel.cs:17`) — and neither of them is Matrix. When
   preferences are built, they must be keyed by *registered channel*, not by a fixed column per
   vendor, and this field has to go with it.

Email stays the one outbound channel the core ships. Matrix, WhatsApp, Telegram and SMS are
channel-provider plugins in [`plugins.md`](plugins.md), reached through the provider seam defined in
[`core-platform.md`](core-platform.md).

## CORE-05 — Versioned templates and communication events

**Status `partial` · Priority P0 · Owner `core`.**

What exists is a **renderer**, not a template system. `ITemplateRenderer`
(`Services/Abstractions/ITemplateRenderer.cs:3`) declares one method, `Render(template, values)`
(`:5`), and `SimpleTemplateRenderer` (`Services/Core/SimpleTemplateRenderer.cs:5`) implements it as
string substitution of `{{key}}` and `<%key%>` (`:17-19`). `IMessageComposer`
(`Services/Abstractions/IMessageComposer.cs:3`) pairs a subject and a body template (`:5`). The
template text is passed in by the caller.

Nothing stores a template, versions it, validates its placeholders or maps a semantic event key to
one. There is no template table on `ApplicationDbContext` (`Data/ApplicationDbContext.cs:14-30`).

Open: channel-neutral stored templates, published placeholder schemas, preview and validation,
versions with history and rollback, layout/master templates, and a mapping from semantic event keys
to templates. Under R2 this stays a single central system: plugins register **event keys,
placeholders and default templates** through contracts and never learn a core template id, and no
plugin ships a second renderer. Campaign tooling, a Listmonk synchronisation or a postal provider
remain plugins on top of it.

## CORE-06 — Generic workflow and state-machine foundation

**Status `open` · Priority P1 · Owner `core`.**

Nothing exists: no workflow definition, instance or history entity is on `ApplicationDbContext`
(`Data/ApplicationDbContext.cs:14-30`), and no service declares one.

### The storage split — decide this *before* designing the engine

This is the one structural divergence that cannot be papered over later, so it is a decision point,
not an implementation detail:

- **Core state would be an EF entity.** Core domain data lives as typed `DbSet<T>` properties on
  `ApplicationDbContext` (`Data/ApplicationDbContext.cs:14-30`), with real columns, real types and
  real foreign keys.
- **Plugin state cannot be.** A plugin reaches persistence only through `IPluginDataStore`
  (`Contracts/Plugin/IPluginMigration.cs:3`): its own prefixed tables (`:7`), raw SQL in
  (`:11`) and out (`:16`), and rows returned as `PluginDataRow`, an
  `IReadOnlyDictionary<string, string?>` (`:33`). Every value is a nullable string. There is no
  typing, no navigation property and no cross-table constraint back to a core entity.

So a workflow instance owned by the core and a workflow instance owned by Finance or Inventar cannot
share one storage shape as things stand. **Decide which of the two the engine uses — a core EF
schema that plugins reach only through a host service, or plugin-prefixed rows the core also writes
through — before any state machine is designed.** Choosing late means rewriting the engine, not
adapting it.

### The honest minimum

The real cases the engine must serve — rental, joining review, approvals, editorial processes —
support exactly four things, and nothing beyond them is justified yet:

1. **Named states.**
2. **Named transitions, each carrying a permission key**, so authorisation is part of the definition
   rather than bolted on by each caller.
3. **An append-only history row per transition**, never updated in place.
4. **A callable command** to attempt a transition, idempotent per instance and transition.

There is no `Rental`, `Membership` or `Dunning` state machine in the core: under R4 those are
plugin-declared definitions. The item is blocked on the remaining plugin-surface increments tracked
in [`core-platform.md`](core-platform.md), because a workflow with no surface to drive it from is
not usable.

## CORE-08 — Family and household relationships

**Status `partial` · Priority P1 · Owner `core`.**

A hierarchy is rendered today, but it is not a modelled relationship. `MemberHierarchyViewModel`
flattens parent members at depth 0 followed by their sub-members at depth 1
(`Models/Member/MemberHierarchyViewModel.cs:13-19`), and the parent link is **derived from the
already loaded `MemberMetadataValue` graph** (`:25`) — a metadata value, not a foreign key.
`Models/Member.cs` declares no parent or household property of any kind.

Open: an explicit, validated relationship between a main member and their family members; cycle
protection, which a metadata string cannot give; consistent membership-type rules; and a defined
effect on communication. Contribution calculation stays in the Finance plugin — this item defines
the relationship, not what it costs.

## CORE-09 — API keys and service accounts: extract into a plugin

**Status `partial` · Priority P2 · Owner `core` (until the extraction lands).**

### The decision, and the reasoning it replaces

The capability **shipped in the core** in `7c3f802`: hashed-only storage, one-time secret display,
an explicit permission list, optional expiry, revocation, last-use tracking, audit, and the
`apikeys.manage` permission held separately from `admin.access` (`docs/api-keys.md`).

The August 2026 list argued API keys were core *"weil Authentifizierung eine Sicherheitsgrenze des
Hosts ist"* — because authentication is a security boundary of the host. **That reasoning is
superseded.** It proves too much: it would keep every authentication mechanism in the core forever,
including the OIDC binding that already ships as the `IamOidc` plugin. The user has decided to
extract API keys into a plugin, and R1 supports it — a bearer-token scheme for external clients is
a binding, not the boundary itself. What the old reasoning *did* get right survives as the
constraint below, narrowed to the part that is actually true.

### The bootstrap ordering is permanent, not temporary

**Cookie/Identity authentication stays in the core forever.** All three plugin-admin controllers are
`[Authorize]` plus `[PermissionAuthorize(PermissionKeys.AdminAccess)]` —
`Controllers/PluginAdmin/PluginAdmin_Controller.cs:10-11`,
`Controllers/Api/PluginsController.cs:10-11`,
`Controllers/Api/PluginAdminCommandsController.cs:12-13` — and plugins are loaded at
`Program.cs:60-61` (`pluginLifecycleService.LoadActivatedAsync()`), **before**
`app.UseAuthentication()` at `:82`. The only possible order is therefore:

```
cookie auth boots → an admin signs in → the plugin is installed and activated → the api-key scheme becomes available
```

never the reverse. An api-key plugin can never be the *only* way to authenticate, because
authenticating is what it takes to install it. This is a permanent property of the bootstrap, not a
gap to be closed later, and any design that assumes otherwise is wrong.

### Sequencing

Extraction is blocked on four platform items, all defined in
[`core-platform.md`](core-platform.md) and cross-referenced here only:
**CORE-13** (the provider seam that lets a plugin contribute an authentication scheme),
**CORE-15** (a settings and secret store for the plugin to keep hashed keys and configuration in),
**CORE-17** (drain before unload) and **CORE-18** (instantiate contributions through DI).
CORE-17 and CORE-18 are not optional refinements here: authentication runs on the request hot path,
so a handler type resolved out of a collectible load context that may unload mid-request is the
worst place in the application for an undrained unload.

### The move list

Out of the core, into the plugin — every path verified at HEAD:

- `Models/ApiKey.cs`
- `Services/Abstractions/IApiKeyService.cs`
- `Services/Core/ApiKeyService.cs`
- `Services/Authorization/ApiKeyAuthenticationHandler.cs`
- the `DbSet<ApiKey>` on `Data/ApplicationDbContext.cs:27`
- `Data/Migrations/202608310101_AddApiKeys.cs`

### What stays in the core regardless

- **Cookie/Identity authentication**, per the ordering above.
- **The scheme-registration path.** A plugin-contributed scheme is registered through
  `IAuthenticationSchemeProvider`, which is already proven by
  `ExternalLoginService.RegisterDeclaredSchemesAsync`
  (`Services/ExternalLogin/ExternalLoginService.cs:23,49`), called from `Program.cs:63-64`. The
  extraction reuses that path; it does not invent one.
- **The permission-definition seam.** Permission keys are declared and checked by the host; a
  plugin-issued key grants nothing the host has not defined.
- **Audit.** Issuing and revoking a credential is host-level evidence and stays where every other
  audit entry is.

### Do not close the documented gaps in the core first

`docs/api-keys.md:72` lists three things as not built: the admin UI (`:74`), an endpoint that
accepts the scheme (`:77`) and per-key rate limiting (`:81`). **All three are built inside the
plugin, after extraction.** Building them in the core first would be writing code destined for
deletion, and it would make the move larger rather than smaller.

### Extraction cost is low — say so plainly

The scheme is non-default, a request with no key header returns `AuthenticateResult.NoResult()`
(`Services/Authorization/ApiKeyAuthenticationHandler.cs:45`), and **no endpoint or policy consumes
it today** (`docs/api-keys.md:77`). Removing it from the core therefore breaks no caller.

### The one real cost: a data decision point

Existing keys are rows in a core EF table (`Data/ApplicationDbContext.cs:27`), while a plugin gets
its own `plugin_<key>_` prefixed tables reached only through `IPluginDataStore`
(`Contracts/Plugin/IPluginMigration.cs:3,7`). The two cannot be the same table.

**Decide explicitly, before the move: migrate the existing rows into the plugin store, or accept a
reset and re-issue every key.** A reset is defensible — keys are hashed, short-lived by policy and
revocable — but it must be a decision on the record, not a discovery during deployment.

## CORE-26 — Local-unit radius search and export

**Status `open` · Priority P2 · Owner `core`.**

The architecture decision explicitly retained the legacy local-unit radius search, but it never
received a canonical item. Build it after CORE-01: authorised users search members or units within
a radius and export the same filtered result. Location data, privacy scope, permission boundaries
and export auditing must be decided in the CORE-01 model rather than copied from Clubmanager.

## CHAT-01 — Internal messages and chat

**Status `superseded` · Owner `core` · Both sides recorded.**

The two sources disagreed, so both are kept:

- **The August 2026 list filed it as a plugin.** `TodoFeatures.md:83` lists *"Interne
  Nachrichten/Chat"* as **P3, Plugin, fehlt** — priority 3, a plugin, not present.
- **The 2026-08 proposal folded it into the core, and that is what shipped.**
  `docs/architecture/proposal-2026-08/DECISIONS.md:123-145` records the decision under the heading
  *"CHAT-01 — folded into the core as one message substrate"*: a notification inbox and a chat are
  the same structure seen from two directions, and building them separately would have given the
  core two inboxes, two read states, two unread counters and two "you have a new message" paths,
  with members having to look in two places.

**The decision resolves in favour of the proposal, because the code agrees with it.** The substrate
shipped in `58c7988` and the member inbox in `d89d29b`: one `MessageThread` with a free-form `Kind`
(`Models/Messaging.cs:30`), `Message` (`:53`) and `MessageParticipant` (`:74`), where participation
is the access rule and `CanPost` separates a notification recipient, who reads but cannot reply,
from a chat participant who can.

This is also what R1 predicts. The chat *capability* is core — it is the same inbox, read state and
unread count as notifications, which is exactly the duplication R1 exists to prevent. What is left
for a plugin is the *binding*: a Matrix, Telegram or WhatsApp bridge that carries threads to and
from an external network. Those are service plugins and are listed in
[`plugins.md`](plugins.md), not here, and they consume the provider seam defined in
[`core-platform.md`](core-platform.md).

`CHAT-01` therefore opens no work of its own. It is kept at `superseded` so that a reader who finds
the P3-plugin line in an old list can see why the repository does something else.

## Self-service residue

Two items from the August 2026 list's *"already covered or only to be topped up"* table are
**genuinely open** and would otherwise be lost, because that table's shape suggests they were done.
They carry no ID of their own: they are residue on the core self-service surface, tracked here
against CORE-08's member model until either grows large enough to earn an item.

1. **Privacy-consent status.** `Member.DataprivacyAccepted` (`Models/Member.cs:89`) is a single
   boolean. There is no version of the declaration that was accepted, no timestamp of acceptance
   and no re-consent path, so a changed privacy statement cannot be re-asked for and an existing
   `true` cannot be attributed to any particular text. Open: version the declaration, record which
   version was accepted and when, and re-ask when the version moves.

2. **The tokenised email-change workflow.** The *columns* exist and the *workflow does not*.
   `Member.PendingEmail` (`Models/Member.cs:97`), `EmailVerificationToken` (`:100`) and
   `EmailVerificationTokenExpiry` (`:102`) are persisted (`Services/Core/MemberFeatureService.cs:530-532`)
   and a pending change is even displayed to the member (`Views/SelfService/Profile.cshtml:211-214`),
   but no core code ever issues a token, sends it, checks its expiry or redeems it — every reference
   outside the model copies the values between projections. Open: issue a time-limited token, mail
   it, redeem it against the expiry, audit the change, and define the behaviour when the address is
   owned by an external identity provider rather than by ClubGear.
