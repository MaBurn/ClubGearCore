# One entry per feature. Scores are my assessment, stated so they can be argued with.
#   value    — how much a member/board/treasurer notices it
#   effort   — 1 = XS … 5 = XL
#   blocking — how much other work is stuck behind it
#   risk     — chance of building the wrong thing or of it going wrong in production

FULL = [
 dict(id="CORE-03", name="Plugin surfaces and contracts", owner="Core", effort_label="L · 13–25 PT",
      value=5, effort=3, blocking=5, risk=3, mock="03-plugin-page.svg",
      pitch="Every large plugin needs pages, forms, uploads and a JSON twin. Build that once, or build it "
            "into the host five times as special cases.",
      today=[
        "The contract gives a plugin a table with columns and commands, one filter string and untyped "
        "string arguments.",
        "No pagination, no sorting, no detail view, no upload, no download, no JSON endpoint.",
        "Clubmanager solved this with a 128 KB inventory controller in the host."],
      after=[
        "A plugin describes its page as data; the host renders it, checks permissions and validates input.",
        "The same definition serves the HTML page and its mobile JSON twin.",
        "Accessibility, CSRF and encoding are solved once in the host, not per plugin."],
      without=[
        "Inventory 2.0, Finance 2.0 and Newsletter each need host changes.",
        "Every plugin invents its own form handling — and its own security bugs.",
        "The BFSG accessibility work would have to be repeated per plugin."],
      depends="nothing", unlocks="INV-01, FIN-01, NEWS-01, DOC-01, every later plugin",
      risknote="Over-building. Mitigation: extend only for a capability a concrete plugin needs now, "
               "one compatibility test per increment."),

 dict(id="CORE-02", name="Secure file and media storage", owner="Core", effort_label="M (+S backend port) · 7–14 PT",
      value=5, effort=2, blocking=4, risk=2, mock="02-secure-file-storage.svg",
      pitch="Uploads are needed by almost every module. Clubmanager paid for them at least seven times.",
      today=[
        "ClubGear has exactly one upload path: the profile image.",
        "Clubmanager has seven separate image/attachment tables and no storage service at all.",
        "No content-signature check, no quota, no orphan cleanup, no per-plugin isolation."],
      after=[
        "One service: policy per scope, size and MIME checks, generated storage names, audit, cleanup.",
        "Business modules keep an opaque file id — never a path under wwwroot.",
        "Backend is selectable: local filesystem or Nextcloud WebDAV (plugin), with online migration."],
      without=[
        "Inventory images, newsletter attachments, documents and finance imports each reinvent it.",
        "Path traversal and executable upload become seven separate risks.",
        "Nextcloud as a file store is not possible at all."],
      depends="system config, audit", unlocks="INV-01, NEWS-01, DOC-01, EDIT-01, CMS-01, POST-01",
      risknote="Low. The interface is well understood; the only real trap is letting plugins pass paths."),

 dict(id="CORE-01", name="Organisation structure and subdivisions", owner="Core", effort_label="L · 13–25 PT",
      value=4, effort=3, blocking=4, risk=3, mock="01-org-structure.svg",
      pitch="One authoritative hierarchy of chapters, departments and committees — the scope that "
            "permissions, calendars and inventory all hang off.",
      today=[
        "ClubGear has no organisation model. Everything is club-wide or personal.",
        "Clubmanager has local units with delegated admins — plus five Keycloak group tables mirroring them.",
        "Unit-scoped permissions do not exist, so delegation means making someone a global admin."],
      after=[
        "Units with parent/child, memberships with dates, contacts and delegated administrators.",
        "Permission checks scoped to one unit and optionally inherited by descendants.",
        "Self-service join/leave with an approval policy per unit."],
      without=[
        "Calendar, inventory ownership, editorial roles and unit-role notifications have no scope to use.",
        "Delegating anything keeps meaning full admin rights.",
        "Every later module invents its own grouping concept."],
      depends="members, roles, audit", unlocks="CORE-07, INV-01, EDIT-01, ORGVIEW-01, unit-scoped notifications",
      risknote="Medium. Cycle prevention, archival and inherited permissions are easy to get subtly wrong; "
               "they need tests from day one."),

 dict(id="CORE-05", name="Versioned templates and communication events", owner="Core", effort_label="L · 13–25 PT",
      value=4, effort=3, blocking=3, risk=2, mock="06-template-editor.svg",
      pitch="Every message the club sends, editable by a human, versioned, with a way back.",
      today=[
        "ClubGear renders messages with a simple string renderer. No versions, no preview, no history.",
        "Clubmanager has versioning and rollback — but in two parallel systems (V1 portal and V2 controllers).",
        "Callers would have to know numeric template ids."],
      after=[
        "Semantic event keys: a caller emits member.welcome, the system picks channel, locale and version.",
        "Every save is an immutable version with actor and reason; compare and roll back.",
        "Declared placeholder schema — publishing a template with an unknown field is blocked."],
      without=[
        "Message text stays in code and needs a developer for every wording change.",
        "Finance, newsletter and letters each carry their own text handling.",
        "No audit of what a member was actually told, and when."],
      depends="CORE-02, CORE-03", unlocks="CORE-04, FIN-01, NEWS-01, POST-01, EDIT-01",
      risknote="Low–medium. The trap is a second renderer surviving next to it — Clubmanager's exact mistake."),

 dict(id="CORE-04", name="Notification inbox and delivery preferences", owner="Core", effort_label="L · 13–25 PT",
      value=4, effort=3, blocking=3, risk=3, mock="04-notification-inbox.svg",
      pitch="One inbox for everything the system wants to tell a member — and any medium as a plugin.",
      today=[
        "ClubGear can send to two channels and stores a flat notification record. No inbox, no preferences.",
        "Clubmanager's inbox, unread count and per-product channel settings already work well.",
        "But its Matrix agent is part of the host — exactly what must not be repeated."],
      after=[
        "Inbox with unread count, mark read, delete, action link; per-category channel preferences.",
        "Email ships with core; Matrix, Telegram, SMS, E-Post become installable provider plugins.",
        "Explicit fallback policy per event; retry-safe delivery with an idempotency key."],
      without=[
        "Each plugin builds its own mail sending and its own idea of 'notified'.",
        "Adding a new medium later means changing the core.",
        "No administrator view of what failed to reach whom."],
      depends="CORE-05, CORE-03, CORE-01", unlocks="channel plugins, NEWS-01, FIN-01, RENT-01, CORE-07",
      risknote="Medium. Capability negotiation and idempotency are the parts that look simple and are not."),

 dict(id="CORE-08", name="Family and household relationships", owner="Core", effort_label="M · 6–12 PT",
      value=3, effort=2, blocking=2, risk=2, mock="09-family-relationships.svg",
      pitch="Explicit, dated relationships between members — so family fees and household mailings stop "
            "being guesswork.",
      today=[
        "ClubGear has a member hierarchy but no relationship semantics.",
        "Clubmanager has one table and three actions — the thinnest legacy feature of all.",
        "Nothing validates cycles, duplicate links or impossible date ranges."],
      after=[
        "Primary/related member with type, effective dates and status; hierarchy shown in the member view.",
        "Membership-type changes validated against family rules before saving.",
        "A minimal read model that Finance can use for discounts — without touching core tables."],
      without=[
        "Family fee rules in Finance stay manual or wrong.",
        "Newsletter sends one mail per person instead of one per household.",
        "Ending a relationship silently loses its history."],
      depends="members, membership types", unlocks="FIN-01 fee rules, NEWS-01 household collapsing",
      risknote="Low. Mostly validation and privacy rules; small surface."),

 dict(id="CORE-07", name="Calendar and events", owner="Core", effort_label="L + M store port · 19–37 PT",
      value=4, effort=4, blocking=2, risk=4, mock="08-calendar.svg",
      pitch="Club dates, recurring meetings and registrations — with the data living in your Nextcloud.",
      today=[
        "ClubGear has no calendar at all.",
        "Clubmanager has events, recurrence, exceptions and registrations — with Nextcloud wired straight "
        "into a 42 KB service.",
        "Its app password sits in a plain string field with a // TODO: Verschlüsseln comment."],
      after=[
        "Events, recurring meetings, exceptions, capacity and waitlists, scoped to organisation units.",
        "In Nextcloud mode the VEVENT is the record — including registrations as ATTENDEE/PARTSTAT.",
        "ClubGear keeps a rebuildable cache; members see the calendar in their phone directly."],
      without=[
        "Dates stay in a separate Nextcloud calendar with no link to members or registrations.",
        "No capacity, no waitlist, no reminder that reaches the right unit."],
      depends="CORE-01, CORE-04, CORE-02 backend port", unlocks="assembly/meeting modules, course booking later",
      risknote="Highest of the core items. Capacity becomes optimistic (ETag + If-Match), foreign CalDAV "
               "clients can drop X- properties, and availability moves to Nextcloud."),

 dict(id="CORE-06", name="Workflow and state machine foundation", owner="Core", effort_label="L · 13–25 PT",
      value=2, effort=3, blocking=2, risk=3, mock="07-workflow.svg",
      pitch="A reusable approval/lifecycle engine — genuinely valuable only once something needs it.",
      today=[
        "ClubGear has no workflow engine.",
        "Clubmanager built one (5 tables, admin UI, seeding) and then bypassed it with a hard-coded "
        "14 KB RentalStateMachine.",
        "That is the clearest warning in the whole survey."],
      after=[
        "Namespaced definitions with versions, permitted transitions, permissions per transition.",
        "Immutable history, idempotent commands, retry-safe side effects through an outbox.",
        "Plugins register their own workflows; the core stays domain-free."],
      without=[
        "Rental, membership approval and editorial each hard-code their own states.",
        "No common audit of who approved what."],
      depends="CORE-03, audit, background jobs", unlocks="RENT-01, membership approval, EDIT-01 releases",
      risknote="Its value is entirely conditional on RENT-01 or a comparable process actually being built. "
               "If rental is dropped, this drops with it."),

 dict(id="INV-01", name="Inventory 2.0", owner="Plugin", effort_label="XL · 26–50+ PT",
      value=5, effort=5, blocking=1, risk=2, mock="10-inventory.svg",
      pitch="The domain people touch weekly — and the one closest to a straight port.",
      today=[
        "The plugin lists inventory number, description, category, status and location. Basic CRUD.",
        "Clubmanager has 55 actions, 30 views, warehouses, stock, images, QR codes, bulk import, "
        "Excel export and an inventory-taking mode.",
        "Plus a 70 KB mobile controller."],
      after=[
        "Warehouses and hierarchical locations, stock movements with immutable history.",
        "Images via CORE-02, barcodes and reference catalogue, warehouse-scoped permissions.",
        "Simple lending, and a mobile JSON API with the same permissions as the web page."],
      without=[
        "The club keeps using Clubmanager for inventory — which means keeping Clubmanager running.",
        "Rental (RENT-01) has nothing to build on."],
      depends="CORE-03, CORE-02, CORE-01, CORE-04", unlocks="RENT-01",
      risknote="Low risk, high volume. The known scope is large but well understood; slice it "
               "(warehouses+stock → images+codes → loans → mobile API)."),

 dict(id="FIN-01", name="Finance 2.0 and fee runs", owner="Plugin", effort_label="XL · 26–50+ PT",
      value=5, effort=5, blocking=1, risk=4, mock="11-finance.svg",
      pitch="The treasurer's workload — fee runs, payment matching and SEPA — end to end and auditable.",
      today=[
        "The plugin has versioned bank data, SEPA mandates, accounts, fee demands and pain.008 at "
        "differing maturity.",
        "Payment file import and matching are missing entirely.",
        "Clubmanager has the whole matching workflow in a 73 KB controller."],
      after=[
        "Reproducible fee runs with draft → reviewed → approved → exported states and pinned rules.",
        "camt import with duplicate detection, deterministic match suggestions with visible reasons, "
        "match/unmatch/rematch fully audited.",
        "IBAN masked by default; unmasking is a separate, audited permission."],
      without=[
        "Fee collection stays manual or stays in Clubmanager.",
        "The single most time-consuming volunteer job stays unautomated."],
      depends="CORE-05, CORE-08, CORE-03", unlocks="RENT-01 payments, donation receipts, e-invoicing later",
      risknote="Money and personal bank data. Needs the highest test depth and an explicit key strategy; "
               "a wrong fee run is visible to every member."),

 dict(id="NEWS-01", name="Mass mail and newsletter", owner="Plugin", effort_label="XL · 26–50+ PT",
      value=3, effort=5, blocking=1, risk=4, mock="12-newsletter.svg",
      pitch="Consent-aware campaigns with a frozen recipient list and real bounce handling.",
      today=[
        "ClubGear has no campaign feature, but already has IMAP, a bounce poller and email invalidation.",
        "Clubmanager has 24 actions including bounce fetching, revalidation and a full postal fallback run.",
        "Its 70 KB InvalidEmailLetterService is the argument for keeping letters a separate plugin."],
      after=[
        "Segments from approved read models, immutable recipient snapshot, dry run with samples.",
        "Bounded batches with cancel/resume, per-recipient status, suppression and opt-out enforcement.",
        "Listmonk as an optional adapter, not a premise."],
      without=[
        "Club-wide mailings stay in Clubmanager or in someone's mail client.",
        "No consent record, which is the part that actually carries legal risk."],
      depends="CORE-04, CORE-05, CORE-02", unlocks="editorial distribution, POST-01 fallback",
      risknote="GDPR and deliverability. Sending twice, or to someone who opted out, is the failure mode "
               "that damages trust fastest."),
]

COMPACT = [
 ("NC-01", "Nextcloud plugin — file backend + calendar store", "Plugin", "M–L",
  "Makes the local/Nextcloud choice real. Only worth building once CORE-02 and CORE-07 expose their ports.", 4, 1),
 ("CORE-09", "API keys and service accounts", "Core", "M",
  "Every external client and the mobile inventory app needs this. Currently nothing can authenticate but a browser session.", 3, 2),
 ("CORE-12", "Audit, log and changelog views", "Core", "S–M",
  "The services exist; this is the admin UI on top. Cheapest item on the list.", 2, 1),
 ("CORE-10/11", "Branding, navigation, translations", "Core", "S–M / M",
  "Cosmetic until the club wants its own look, then suddenly urgent. Translations only matter with non-German members.", 2, 1),
 ("RENT-01", "Marketplace and rental", "Plugin", "XL",
  "The reason CORE-06 exists. Money, liability and state transitions — the highest-risk plugin of all.", 5, 3),
 ("EDIT-01", "Editorial and publication planning", "Plugin", "XL",
  "Submissions, issues, approvals for club magazine and newsletter. Only if the club actually publishes.", 5, 2),
 ("DOC-01", "Document compendium", "Plugin", "M",
  "Protected document store with metadata and search. Cheap once CORE-02 exists.", 2, 2),
 ("NC-DOC-01", "Nextcloud document connector", "Plugin", "M",
  "Largely absorbed by NC-01 if the file backend lands first — check before planning it separately.", 2, 1),
 ("POST-01", "Postal letters", "Plugin", "L",
  "PDF letters from core templates with provider dispatch. The legacy 'letter to invalid email' feature lives here.", 3, 2),
 ("TREE-01", "Decision trees, tech help, meetings", "Plugin", "L",
  "One generic guided-process plugin, configured three ways. Independent of almost everything else.", 3, 2),
 ("ORGVIEW-01", "Org chart view", "Plugin", "M",
  "Pure visualisation of CORE-01. Nice to have, no dependencies beyond it.", 2, 1),
 ("CHAT-01", "Internal messages and chat", "Plugin", "L",
  "Clarify the need first: system messages are CORE-04, rental talk is a thread in RENT-01.", 3, 1),
 ("GPS-01", "GPS trackers and Traccar", "Plugin", "L",
  "Very club-specific. Only if vehicles are actually tracked.", 3, 1),
 ("CMS-01", "Public content pages", "Plugin", "M",
  "Editable public pages. Login, imprint and privacy stay core.", 2, 1),
 ("MISC", "Birthday greetings · radius search", "Plugin / Core", "S–M each",
  "Two features that exist in Clubmanager and are missing from the backlog. Decide keep or drop.", 1, 1),
]
