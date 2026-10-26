# Inventory 2.0

**Priority:** P1  
**Target:** `clubgear.plugin.inventory`  
**Estimated effort:** XL (26–50+ person-days)

## Objective

Expand the existing Inventory plugin from basic item CRUD into a complete warehouse, stock,
history, image, barcode, and mobile inventory capability without moving inventory concepts into
Core.

## Functional requirements

1. Manage warehouses and hierarchical storage locations with stable IDs and archive behavior.
2. Manage item categories, inventory items, unique inventory numbers, status, condition, quantity,
   unit, description, location, owner unit, and optional acquisition metadata.
3. Support stock adjustments with reason, actor, timestamp, before/after quantity, and notes.
4. Preserve an immutable history for creation, edits, moves, adjustments, archival, and loans.
5. Attach multiple images through Secure File Storage with ordering and a primary image.
6. Assign, validate, and look up barcodes, QR codes, and configurable reference numbers.
7. Import and maintain reference-catalog entries from validated CSV files with preview and errors.
8. Provide search, filters, sorting, and pagination for item, warehouse, location, category, status,
   code, and organization unit.
9. Provide simple loans with borrower, checkout, due date, return, status, and overdue indication.
10. Expose stable mobile JSON endpoints for authorized listing, lookup, details, stock changes, and
    image upload.
11. Permissions distinguish view, item management, stock adjustment, loan management, and warehouse
    administration and may be scoped to a warehouse or organization unit.
12. Plugin-owned tables and files are removable only through the explicit plugin uninstall policy.

## Dependencies

- Plugin Surfaces and Contracts, Secure File Storage, Notification Inbox, Organization Structure.
- Optional future integration with Rental Marketplace through public plugin contracts/events.

## Out of scope

- Deposits, payments, commercial rental orders, damage settlement, or owner payouts.
- Direct access to Member, Finance, or Core organization tables.
- A second standalone Tool Rental implementation.

## Acceptance criteria

- Concurrent adjustments cannot produce an invalid quantity or lose history.
- Duplicate inventory/reference numbers are rejected according to configured scope.
- Warehouse authorization is enforced identically in UI and mobile APIs.
- Existing Inventory plugin data migrates forward without manual table edits.
- All important commands have integration tests against the plugin data store.

