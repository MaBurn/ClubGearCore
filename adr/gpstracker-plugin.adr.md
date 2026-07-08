# ADR: GPS Tracker Plugin

## Purpose

Persistent architectural decisions for the gpstracker-plugin iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-12 - Single-Row-Per-Member Schema with UPSERT and Hard-Delete Revoke

### Decision

The GPS Tracker plugin uses a single-row-per-member table `{prefix}tracker` with a `UNIQUE (MemberId)` constraint. Assign is an `INSERT ... ON CONFLICT(MemberId) DO UPDATE` upsert that overwrites the existing tracker. Revoke is a hard `DELETE WHERE MemberId = @memberId`. The table columns are: `MemberId INTEGER NOT NULL`, `TrackerId TEXT NOT NULL`, `Provider TEXT NOT NULL`, `IsActive INTEGER NOT NULL DEFAULT 1`, `IssuedAt TEXT NOT NULL`, `ReturnedAt TEXT NULL`.

### Rationale

The domain invariant is one active tracker per member. The UPSERT pattern is already used by CarInfo for `field_definitions` (UNIQUE on `FieldKey`) and is idiomatic in this codebase. Hard delete on Revoke matches the CarInfo vehicle delete pattern, avoids leaking PII in soft-deleted rows, and keeps the query path simple (a present row means an active assignment).

---

## 2026-06-12 - Permission Key: Reuse members.manage for Assign and Revoke

### Decision

Both `gpstracker.assign` and `gpstracker.revoke` action slots use `PermissionKey = "members.manage"`. No GPS-specific permission key is introduced. The manifest declares `["members.read", "members.manage"]` in its `Permissions` array.

### Rationale

Research (Q4) confirmed that introducing a GPS-specific key (e.g. `"gpstracker.manage"`) would require provisioning it as a new core permission seed — a GR-002 core change. GPS Tracker is an admin-only plugin with no self-service path, so `"members.manage"` is the correct gate. Zero core changes are required.

---

## 2026-06-12 - Shared Plugin Test Helpers Extracted to Helpers/PluginTestHelpers.cs

### Decision

The five private nested test helper classes from `CarInfoPluginSlice3Tests.cs` (`SqlitePluginStore`, `TestPluginHostContext`, `TestMetadataFacade`, `NoOpMemberReader`, `NoOpMemberActions`) are extracted into a new internal file `tests/ClubGear.ArchitectureTests/Helpers/PluginTestHelpers.cs`. The `SqlitePluginStore` constructor is updated to accept a `tablePrefix` parameter instead of hardcoding `"plg_carinfo_"`. `CarInfoPluginSlice3Tests` is updated to pass `"plg_carinfo_"` as the prefix argument. GPS Tracker tests pass `"plg_gpstracker_"`.

### Rationale

Without extraction, the GPS Tracker test file would duplicate ~130 lines of SQLite boilerplate. The shared file establishes a reusable plugin-test fixture convention for all future plugins. The parameterised prefix allows each test suite to use its own short name while keeping assertions prefix-agnostic via `store.GetTableName(...)`.

---

## 2026-06-12 - Status Badge: success Tone for Active, secondary for No Tracker

### Decision

`GpsTrackerBadgeProvider.GetBadgesAsync` returns a single `MemberStatusBadgeSlot` always. When a tracker row exists and `IsActive = 1`: `Tone = "success"`, `Label = "Tracker aktiv"`. In all other cases (no row, or row with `IsActive = 0`): `Tone = "secondary"`, `Label = "Kein Tracker"`.

### Rationale

Research (Q3) confirmed `"success"` maps to `bg-success` and `"secondary"` maps to `bg-secondary` in `_PluginSlots.cshtml`. Both are valid Bootstrap-mapped tone strings. GPS Tracker is the first production plugin to populate the `member.badge` slot. Research (Q1) confirmed the slot service and Razor view handle an empty badge list safely; a single badge is always appropriate here because the tracker status is always defined (either assigned or not).

---

## 2026-06-12 - Detail Card Returns HTML ul via Html.Raw

### Decision

`GpsTrackerDetailCardProvider.GetCardsAsync` returns an HTML `<ul class="list-unstyled mb-0">` string when a tracker is assigned, and `<p class="text-muted">Kein Tracker zugewiesen.</p>` when none. All string values from the database (TrackerId, Provider) are passed through `System.Net.WebUtility.HtmlEncode` before interpolation. The card body is rendered in `_PluginSlots.cshtml` via `@Html.Raw(card.Card.Body)`.

### Rationale

The `Html.Raw` rendering path was established by the previous carinfo-member-profile iteration. No further Razor view changes are needed. XSS risk is mitigated by server-side HtmlEncode on all user-supplied strings. The `<ul>` pattern is consistent with the CarInfo detail card.

---

## 2026-06-12 - Edit Tab Is Plain Text; Mutations Are Action-Only

### Decision

`GpsTrackerEditTabProvider` returns a plain-text summary string (TrackerId, Provider, IssuedAt, ReturnedAt when set, or "Kein Tracker zugewiesen." when not assigned). It does not render HTML or expose any form fields.

### Rationale

The edit tab in this codebase is an informational surface; mutations are handled through the action modal system (Assign / Revoke action slots). Keeping the tab as plain text avoids introducing a second `Html.Raw` surface and is consistent with CarInfo's edit tab convention.

---

## 2026-06-12 - IPluginDataStore Used Throughout; IMemberExtensionStore Not Used

### Decision

All GPS Tracker data access goes through `host.Persistence` (`IPluginDataStore`). `IMemberExtensionStore` / `host.MemberData` is not used.

### Rationale

Research (Q2) confirmed that `IMemberExtensionStore` is not available in the runtime until the carinfo-member-profile Foundation Rebuild (Slice 1 of the previous iteration) is fully merged. Even if available, the key-value store is designed for simple member preferences, not for structured single-row records with typed fields. Using `IPluginDataStore` with raw SQL is the idiomatic approach in this codebase.

---

## 2026-06-12 - Plugin Category Set Only in plugin.json, Not in C# Constructor

### Decision

`GpsTrackerPluginModule.Manifest` does not call `with { Category = "member-profile" }` in C#. The `plugin.json` includes `"category": "member-profile"`.

### Rationale

Research (Q6) confirmed that `PluginManifestParser` reads the `"category"` field from `plugin.json` and applies it via a `with` expression at activation time. When loaded through the package pipeline (the production path), the in-memory manifest will have `Category = "member-profile"` without any C# constructor change. This matches the CarInfo convention.

---

## 2026-06-12 - Production Table Prefix Derived from Module ID; Test Prefix Is a Short Alias

### Decision

The production table name for the tracker is `plugin_clubgear_plugin_gpstracker_tracker` (derived by `PluginSchemaNamePolicy.GetTablePrefix("clubgear.plugin.gpstracker")` = `"plugin_clubgear_plugin_gpstracker_"`). Test code uses the shortcut prefix `"plg_gpstracker_"` as a constructor argument to `SqlitePluginStore`. All test assertions use `store.GetTableName("tracker")` rather than hard-coded names.

### Rationale

Research (Q7) confirmed the production derivation formula. The test shortcut is a deliberate pattern already used in CarInfo (`"plg_carinfo_"`). Using `GetTableName` in assertions decouples the assertion text from the prefix choice and prevents drift if the prefix is ever changed in tests.

---

## 2026-06-12 - GPS Tracker Plugin Is Plugin-Only; Zero Core Changes

### Decision

This iteration introduces no changes to `Contracts/Plugin/`, `Services/`, `Controllers/`, or `Views/`. The only repository-level side effects are: a new `plugins/GpsTracker/` project, a new `ProjectReference` in the test `.csproj`, a new shared test helper file, and a `dotnet sln add` command.

### Rationale

The task brief states this command is "primaer plugin-only" and that any Core change requires prior user approval per ADR 0001 GR-002. All required contract surface (`IPluginDataStore`, `IMemberDetailCardProvider`, `IMemberEditTabProvider`, `IMemberStatusBadgeProvider`, `IMemberActionProvider`, `IPluginMigration`) is already present in the current codebase. No new abstractions are needed.
