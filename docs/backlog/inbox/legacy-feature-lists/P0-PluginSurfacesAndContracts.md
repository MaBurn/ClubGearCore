# Plugin User Interfaces and Host Contracts

**Priority:** P0  
**Target:** ClubGear Core and Plugin Contract  
**Estimated effort:** L (13–25 person-days)

## Objective

Allow substantial plugins to deliver usable pages, forms, commands, files, and stable JSON APIs
without adding plugin-specific controllers, DTOs, route checks, or rendering branches to Core.

## Functional requirements

1. Plugins can contribute navigable pages with stable, namespaced routes.
2. Page contracts support headings, messages, forms, field validation, tables, filters, sorting,
   pagination, detail views, and confirmation actions.
3. Actions accept typed schemas rather than unvalidated string dictionaries.
4. Core performs authentication, CSRF protection, permission checks, model validation, and
   consistent success/error rendering before or around plugin execution.
5. Plugins can declare download and upload actions through Secure File Storage.
6. Plugins can expose stable namespaced JSON endpoints or an equivalent generic command/query API.
7. API responses use documented error envelopes, cancellation, pagination, and versioning.
8. Plugins can contribute member detail/edit/action, self-service, admin, navigation, generic page,
   background job, audit, notification, and identity-provider capabilities without Core switches.
9. The host validates contribution keys, routes, permissions, and schemas during activation.
10. An incompatible plugin fails activation cleanly and explains the required contract version.
11. Deactivation removes all runtime contributions without requiring an application restart where
    the existing runtime model permits it.
12. The admin UI identifies which plugin owns each page, action, permission, and failed operation.

## Architecture requirements

- Contracts contain generic UI and transport concepts only, never Inventory-, Finance-, or other
  product-specific DTOs.
- Plugins do not reference the Core assembly or access Core database tables directly.
- Contract changes follow semantic versioning and compatibility tests.
- Rendering encodes plugin-provided content by default; explicitly trusted HTML is narrowly scoped.

## Dependencies

- Existing plugin runtime, registry, pages, member slots, permissions, and lifecycle services.
- Secure File Storage for uploads/downloads.
- Enables Inventory 2.0, Finance 2.0, Newsletter, and later large plugins.

## Out of scope

- A second out-of-process/gRPC plugin architecture.
- Plugin-specific behavior in Core.
- Arbitrary server-side Razor assemblies loaded without contract controls.

## Acceptance criteria

- A reference plugin implements a paginated CRUD page and JSON command without Core changes.
- Contract tests prove route isolation, permission enforcement, validation, encoding, and lifecycle.
- Existing plugins remain compatible or receive an explicit migration path.
- No Core branch compares a module ID to select feature behavior.

