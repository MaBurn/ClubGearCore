# Notification Inbox and Delivery Preferences

**Priority:** P0  
**Target:** ClubGear Core and Plugin Contract  
**Estimated effort:** L (13–25 person-days)

## Objective

Extend the existing notification infrastructure into a user-facing inbox with reliable status,
preferences, and plugin-safe event delivery. Email is the default outbound medium shipped with
Core. Every additional medium is supplied by an independently installable channel-provider plugin.

## Functional requirements

1. Authenticated users can list their notifications with pagination and an unread-only filter.
2. Users can view unread count, mark one or all notifications as read, and delete permitted items.
3. A notification contains recipient, title, body/summary, creation time, read time, severity,
   source, event type, and an optional validated action target.
4. Core and plugins can emit notifications through one facade using stable event-type keys.
5. Core provides the in-app inbox and email delivery by default; email is the default outbound
   channel when an event does not select another permitted channel.
6. Additional media are registered by channel-provider plugins. Examples include Matrix,
   WhatsApp, Telegram, SMS, E-Post Mailer, and future providers not known to Core.
7. Users can select allowed channels per event category where policy permits choice. Only active,
   configured providers for which the recipient has a valid destination are selectable.
8. Each event definition declares which delivery capabilities it needs, such as plain text, HTML,
   subject, attachments, delivery receipts, or maximum message length. Core dispatches only to a
   provider that advertises all required capabilities.
9. If a selected provider is unavailable, Core follows an explicit event policy: queue for retry,
   fall back to email, deliver to the in-app inbox only, or fail. It must never fall back silently.
10. Mandatory security or administrative events cannot be fully disabled by user preference.
11. Recipient resolution supports direct users, members, roles, and organization-unit roles without
   plugins querying identity tables.
12. Delivery is retryable and idempotent; a transient channel failure does not duplicate inbox rows
    or provider messages.
13. Failed deliveries are visible to authorized administrators with redacted sensitive content.
14. Retention and bulk cleanup are configurable and auditable.
15. Plugins can only create notifications within their declared permission/capability scope.

## Channel-provider plugin requirements

1. The Plugin Contract defines a generic notification-channel provider interface. Core contains no
   provider-specific branches, DTOs, configuration keys, icons, or destination formats.
2. A provider registers a stable namespaced channel key, display metadata, supported capabilities,
   destination requirements, configuration schema, health check, and send operation.
3. Provider configuration and credentials are namespaced per plugin. Secrets are encrypted at rest,
   redacted in UI and logs, and never returned through ordinary configuration APIs.
4. Recipient destinations are typed and provider-owned, for example a Matrix user/room ID, phone
   number, Telegram chat ID, or E-Post recipient data. Core stores only the contract-defined
   reference and consent/preference state needed for dispatch.
5. Providers validate and normalize destinations without changing the member's unrelated profile
   fields.
6. The send operation receives a provider-neutral rendered message plus correlation and
   idempotency keys. It returns a normalized accepted, delivered, failed, or unknown status and an
   opaque provider message reference.
7. Providers may asynchronously update delivery status through a restricted callback or polling
   contract. External payloads are authenticated and deduplicated.
8. Rate limiting, retry guidance, message-size restrictions, attachment support, and delivery
   receipts are advertised rather than assumed by Core.
9. Activating, deactivating, upgrading, or removing a provider must not delete notification history
   or user preferences. A missing provider is shown as unavailable.
10. A provider cannot read notifications or recipient data that were not dispatched to it.

## Example plugin boundaries

- Matrix, WhatsApp, Telegram, and SMS plugins translate the generic message and destination into
  their respective APIs and authentication models.
- An E-Post Mailer plugin may require postal addressing, PDF/attachment support, and asynchronous
  delivery status. Those capabilities belong to that plugin, not to Core.
- Provider-specific templates may refine a channel rendering, but semantic event definitions and
  fallback policy remain controlled by Core's template and notification systems.

## Dependencies

- Existing `NotificationService`, notification records, channels, plugin facade, members, and users.
- Plugin Surfaces and Contracts for provider registration, configuration, lifecycle, and health.
- Organization Structure for unit-role recipients.
- Template and Communication Events for rendered channel messages.
- Secure secret storage; Secure File Storage for providers that support attachments.

## Out of scope

- Full person-to-person chat.
- Shipping Matrix, WhatsApp, Telegram, SMS, E-Post, or other third-party provider implementations
  as part of Core.
- Marketing campaign management.

## Acceptance criteria

- Inbox authorization prevents reading or mutating another user's notifications.
- Unread counts remain correct after concurrent read/delete operations.
- Preference and mandatory-event behavior is covered by tests.
- Retried delivery produces at most one logical notification per idempotency key and recipient.
- A reference channel plugin can register, configure, send, report health, and update delivery
  status without any provider-specific Core change.
- Deactivating the selected provider applies the declared fallback policy and never silently sends
  through a different medium.
- Contract tests cover capability negotiation, secret redaction, destination isolation, provider
  failure, idempotency, and plugin lifecycle.
