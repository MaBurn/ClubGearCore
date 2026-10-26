# Mass Mail and Newsletter

**Priority:** P1  
**Target:** New plugin  
**Estimated effort:** XL (26–50+ person-days)

## Objective

Provide consent-aware campaigns, recipient selection, batch delivery, bounce handling, and mailing
list synchronization on top of Core templates and notification channels.

## Functional requirements

1. Authorized users can create draft campaigns with a semantic template, locale policy, sender,
   reply-to, attachments, and scheduled time.
2. Recipient segments can use membership status/type, organization unit, role, explicit inclusion,
   and explicit exclusion through approved Core read models.
3. Before sending, the plugin materializes an immutable recipient snapshot and displays count,
   exclusions, missing/invalid addresses, and sample previews.
4. Campaign approval and send permissions are separate from draft editing.
5. Delivery runs in bounded batches with cancellation, progress, retry, and idempotency.
6. Each recipient has queued, sent, delivered where known, bounced, failed, suppressed, or cancelled
   status without storing unnecessary message content.
7. Opt-in, opt-out, legal basis, timestamp, source, and applicable list are recorded and enforced.
8. Suppression rules prevent sending to invalid, bounced, opted-out, or deleted addresses unless a
   documented mandatory-message policy applies.
9. IMAP/provider bounce processing correlates reports safely and supports manual review.
10. Administrators can revalidate an address and remove suppression with an audited reason.
11. Listmonk or another mailing-list service is an optional provider adapter, not a plugin premise.
12. Exports and reports redact personal data according to the operator's permission.

## Security and operational requirements

- Prevent header injection, unsafe attachments, recipient disclosure, and duplicate sends.
- Apply rate limits and provider backpressure.
- Record campaign and consent audit events and define retention for recipient-level logs.
- Failed external synchronization never changes local consent silently.

## Dependencies

- Versioned Templates, Notification Inbox/channels, Secure File Storage, member and organization read
  models, background jobs, and IMAP mailbox support.

## Out of scope

- Postal delivery; it belongs to a separate provider plugin.
- Editing Core member records directly.
- Hard dependency on Listmonk.

## Acceptance criteria

- A dry run shows the exact recipient snapshot and rendered samples without sending.
- Retrying a batch cannot deliver the same campaign twice to one recipient.
- Opted-out and suppressed recipients are excluded in integration tests.
- Bounce processing is idempotent and cannot suppress an unrelated address.
- Provider failure can be resumed from persisted progress.

