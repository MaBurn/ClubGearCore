# Versioned Templates and Communication Events

**Priority:** P0  
**Target:** ClubGear Core  
**Estimated effort:** L (13–25 person-days)

## Objective

Replace ad-hoc message bodies with one channel-neutral, versioned template system that Core and
plugins can use through semantic event keys and declared placeholder schemas.

## Functional requirements

1. Administrators can create, edit, preview, archive, and restore templates.
2. Templates have a stable key, channel, locale, subject/title, body, optional layout, state, and
   version history.
3. Every save creates an immutable version with actor, timestamp, and change reason.
4. Administrators can compare versions and roll back by creating a new current version.
5. Placeholder schemas define key, type, required status, description, and safe sample value.
6. Validation rejects unknown required placeholders, malformed syntax, recursive layouts, and a
   template assigned to an incompatible event or channel.
7. Preview renders against controlled sample data and clearly distinguishes missing values.
8. Semantic event keys map to channel/locale templates without callers storing numeric template IDs.
9. Plugins can register namespaced event definitions, placeholders, and default templates.
10. Plugin removal does not silently delete historical messages or template versions.
11. Rendering is deterministic, HTML-encodes values by default, and supports plain text and HTML.
12. Cache invalidation makes saved or rolled-back versions effective predictably.

## Authorization and audit requirements

- View, edit, publish, rollback, and delete/archive are separate capabilities where appropriate.
- Template changes and mappings are auditable.
- Secrets and unrestricted object traversal are never available to template expressions.

## Dependencies

- Existing `IMessageComposer`, `ITemplateRenderer`, audit services, and notification infrastructure.
- Plugin contract contributions for plugin-owned event definitions.

## Out of scope

- Newsletter campaign selection and scheduling.
- A general-purpose scripting language in templates.
- Provider-specific SMTP, postal, or Listmonk configuration.

## Acceptance criteria

- Tests cover version creation, comparison, rollback, locale fallback, layouts, and encoding.
- A plugin registers and renders a namespaced event without Core code changes.
- Invalid placeholders and recursive layouts fail before publication.
- Rendering the same version with the same input produces the same output.

