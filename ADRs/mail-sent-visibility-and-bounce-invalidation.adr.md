# ADR: Mail Sent-Visibility and Bounce Invalidation

## Purpose

Persistent architectural decisions for the `mail-sent-visibility-and-bounce-invalidation`
iteration: making ClubGear-sent mail appear in the club's Mailcow "Gesendet"/Sent folder, and
flagging a member's email address as `ungültig` when a bounce (DSN) is returned so future automated
sends to it are suppressed. This iteration is **core-only** — no plugin contract changes. It is
placed in the repo's existing `ADRs/` directory, matching project convention.

Append new entries. Do not rewrite prior history.

## 2026-07-24 - MailKit/MimeKit as the IMAP dependency

### Decision
Add `MailKit` and `MimeKit` (MIT-licensed) as plain `<PackageReference>` entries in
`ClubGear.csproj`. All new code stays nullable- and analyzer-clean under the project's
`TreatWarningsAsErrors`/`AnalysisLevel=latest` build. `System.Net.Mail` remains the SMTP send
transport; MailKit is used only for the two IMAP operations (APPEND and FETCH/SEARCH).

### Rationale
The base class library has no IMAP client, so both required capabilities need a library. MailKit is
the de-facto .NET standard, is actively maintained, is nullable-annotated (so it will not trip the
strict build), and its companion MimeKit provides robust RFC 3464 DSN parsing. Hand-rolling IMAP
over `TcpClient` was rejected as high-risk for no benefit; dropping IMAP entirely could not satisfy
"visible in the Gesendet folder" or automatic bounce detection.

## 2026-07-24 - Sent visibility via IMAP APPEND, no BCC fallback

### Decision
After each successful SMTP send, `SmtpEmailNotificationChannel` builds a `MimeMessage` mirror of the
sent mail and calls a new `IImapMailboxService.AppendToSentAsync`, which APPENDs it into a
**configurable** Sent folder (default `Sent`) with the `\Seen` flag. APPEND is gated by an
`AppendSentEnabled` flag and a configured `ImapHost`; failure (including "Sent folder not found") is
logged and never changes the send result. There is no BCC-to-self fallback.

### Rationale
The user explicitly wants sent mail in the **Gesendet** folder; a plain SMTP send leaves nothing
there and a BCC would land in the Inbox, so APPEND is the only mechanism that meets the request.
Keeping SMTP on `System.Net.Mail` leaves the proven wire transport untouched. Best-effort APPEND
avoids coupling deliverability to a secondary IMAP round-trip. Per user choice, the BCC fallback is
omitted to keep one clear behaviour; the folder name is configurable because it is server-dependent
(Sent / Gesendet / INBOX.Sent).

## 2026-07-24 - Bounce poller as the first core BackgroundService, correlate by recipient address

### Decision
Introduce `BounceMailboxPoller : BackgroundService` — the first core hosted service — registered via
`services.AddHostedService<BounceMailboxPoller>()` in `AddClubGearCoreServices`. It runs a
`PeriodicTimer` loop (interval from config, clamped to >= 1 min), uses `IServiceScopeFactory` for
scoped services, wraps each cycle in try/catch, and honours `ApplicationStopping`. Each cycle calls
`IImapMailboxService.FetchBouncesAsync`, which SEARCHes the (default INBOX) bounce folder for unseen
messages, detects DSNs (multipart/report report-type=delivery-status, a message/delivery-status
part, or a mailer-daemon/postmaster sender), extracts the failed recipient, and marks processed
messages `\Seen`. Bounces are correlated to members **by recipient email address only** — no
`Message-ID` capture and no new `NotificationRecord` column this iteration.

### Rationale
The core has no existing hosted-service or scheduler; the plugin `IPluginBackgroundJob` runner is
fire-once and activation-scoped, so it is unsuitable for a standing poller. A `BackgroundService`
with `PeriodicTimer` is the idiomatic .NET pattern and keeps the poller in core where the mailbox
config and member data live. Recipient-address correlation is sufficient because the invalid flag is
a property of the address itself, not of a specific message; it is also the only stable identifier
outgoing mail carries today (no Message-ID is set or persisted), so it avoids touching the send path.
The `\Seen` flag plus an "only flag if not already invalid" guard make reprocessing idempotent.

## 2026-07-24 - Invalid state as three nullable/boolean columns on Member, auto-cleared on email change

### Decision
Add `EmailInvalid` (bool), `EmailInvalidatedAtUtc` (`DateTime?`) and `EmailInvalidReason`
(`string?`) to `Models/Member`, mapped by EF convention. Persist for existing SQLite databases via a
new idempotent `Data/Migrations/2026072401xx_AddMemberEmailInvalidState` (PRAGMA `table_info` guard,
`ALTER TABLE Members ADD COLUMN …`: `EmailInvalid INTEGER NOT NULL DEFAULT 0`, the other two
`TEXT NULL`) wired into `ApplicationSeeder.EnsureSqliteSchemaCompatibilityAsync`. The flag is set by
the bounce poller and **auto-cleared** wherever `Member.Email` changes value — `MemberFeatureService`
update, `SelfServiceFeatureService` profile update, and CSV import update — via a shared
`ResetInvalidOnChange` helper. No manual "mark valid" action this iteration.

### Rationale
The invalid state is intrinsic member data and belongs on the core entity, matching the existing
membership-fee ALTER precedent; the nullable/boolean columns keep every pre-existing row valid and
make the SQLite change a cheap in-place ALTER. A boolean plus timestamp/reason is the minimal shape
that drives both the badge and the suppression guard while retaining admin context. Auto-clear on
address change is the least-surprising reversal: a freshly entered address is presumed deliverable
until a new bounce proves otherwise, and it needs no extra UI.

## 2026-07-24 - Single central suppression guard at NotifyAsync; extend NotificationRecord statuses

### Decision
Place the "suppress sends to invalid addresses" guard once in `NotificationService.NotifyAsync`: for
the `"email"` channel, if the recipient address matches a member with `EmailInvalid`, skip the
channel send and persist a `NotificationRecord` with the new status `"Suppressed"`. The bounce poller
writes records with the new status `"Bounced"`. `PersistAsync` gains an explicit status parameter.
`Views/Admin/Functions.cshtml` replaces its binary `Sent`/else badge with a mapping
(`Sent`→success, `Bounced`→warning, `Suppressed`→secondary, else danger). No `NotificationRecord`
schema change. No plugin-contract change, so `ContractVersion` is not bumped and no plugin is
repackaged.

### Rationale
Research confirmed every automated send — including plugin mail via `ExtensionNotificationFacade` —
routes through `NotifyAsync`, so a single guard there covers all call sites with no bypass and no per
call-site edits. Reusing the existing `NotificationRecord`/Admin-log surface for `Suppressed` and
`Bounced` gives operators visibility without new tables. Because suppression is enforced centrally in
core, plugins need nothing exposed to them, keeping the change core-only and the plugin API version
stable.
