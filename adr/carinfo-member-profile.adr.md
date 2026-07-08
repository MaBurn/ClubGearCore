# ADR: CarInfo Member Profile

## Purpose

Persistent architectural decisions for the carinfo-member-profile iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-12 - Foundation Rebuild Chosen Over Incremental Patch

### Decision

Slice 1 of this iteration rebuilds the Member Extension Data Foundation (the four types
documented in `adr/member-extension-data.adr.md` but never merged to main): `IMemberExtensionStore`,
`PluginInvocationContext`, `PluginPermissionKeys`, and `EnsureMemberExtTableAsync`. This is treated
as a Core change under ADR 0001 GR-002. The alternative — patching CarInfo without the foundation
types — was explicitly rejected by the user.

### Rationale

Without the foundation, CarInfo cannot implement self-service vehicle CRUD correctly (the
`IsSelfService` signal would be unavailable) and the permission key convention
(`PluginPermissionKeys.MemberWrite`) would have no canonical definition. Rebuilding the foundation
in Slice 1 makes CarInfo's Slice 2 changes clean, idiomatic, and reusable by future plugins.

---

## 2026-06-12 - Detail Card Body Returns HTML, Rendered via Html.Raw

### Decision

`CarInfoDetailCardProvider.GetCardsAsync` returns an HTML `<ul>` string in
`MemberDetailCardSlot.Body`. `_PluginSlots.cshtml` line 47 is changed from
`@card.Card.Body` to `@Html.Raw(card.Card.Body)` and the wrapping `<p>` becomes a `<div>`.
All vehicle-supplied strings are passed through `System.Net.WebUtility.HtmlEncode` before
interpolation into the HTML string.

### Rationale

The user chose an HTML list over a plain-text summary for the detail card. `MemberDetailCardSlot.Body`
is already a plain string — no contract change is required. `Html.Raw` unescapes the HTML the plugin
intentionally emits. XSS risk is mitigated by server-side HtmlEncode on every user-supplied value
(make, color, license plate) before it enters the markup string. The `<p>` to `<div>` wrapper
change prevents invalid nesting of `<ul>` inside `<p>`.

---

## 2026-06-12 - Delete-Then-Add Is the Edit Flow; No Dedicated carinfo.edit Action

### Decision

There is no `carinfo.edit` action slot. Members edit a vehicle by deleting it (via `carinfo.delete`)
and re-adding it (via `carinfo.add`). The detail card's HTML list provides the current license
plates; the action modal is the edit surface.

### Rationale

The schema-driven action modal path is fully implemented and already covers both add and delete. A
dedicated edit action would require either a pre-populated form (which the modal does not support
today) or a fetch-and-repopulate flow that would require new JS. The delete-then-add pattern is
sufficient for the current UX requirement and keeps the plugin surface minimal.

---

## 2026-06-12 - carinfo.add and carinfo.delete Use PluginPermissionKeys.MemberWrite, Not members.manage

### Decision

The `PermissionKey` for `carinfo.add` and `carinfo.delete` action slots is changed from
`"members.manage"` to `PluginPermissionKeys.MemberWrite("clubgear.plugin.carinfo")`
(= `"clubgear.plugin.carinfo.member.write"`). `carinfo.field.define` retains `"members.manage"`.
The manifest gains `"clubgear.plugin.carinfo.member.write"` in its `Permissions` array. The core
authorization layer grants `clubgear.plugin.carinfo.member.write` to users who hold
`selfservice.profile.edit`.

### Rationale

Research confirmed (Q4) that self-service users hold `selfservice.profile.edit` and `selfservice.access`
but not `members.manage`. Without a permission key that self-service users can satisfy, every
`carinfo.add`/`carinfo.delete` call from a member would result in `PluginPermissionDeniedException`.
The ADR-planned `PluginPermissionKeys` convention is the correct fix: it scopes write access to the
specific plugin rather than granting broad `members.manage`. Admin users who hold `members.manage`
must also receive `clubgear.plugin.carinfo.member.write` (via the same grant site) so that the
admin vehicle CRUD path continues to work.

---

## 2026-06-12 - CarInfoDataService Continues Using host.Persistence; host.MemberData Reserved for Future Use

### Decision

`CarInfoDataService` does not migrate vehicle or field-definition storage to `host.MemberData` in
this iteration. All SQL operations continue to use `host.Persistence` (the existing
`PluginDataStore`). `CarInfoSchemaMigration` calls `EnsureMemberExtTableAsync` to provision the
`plg_carinfo_member_ext` table, but no CarInfo code reads or writes to it yet.

### Rationale

The existing SQL schema for cars, field definitions, and field values is correct and working. A
migration of that data to the key-value `member_ext` table would be a lossy transformation (the
`member_ext` table stores string key-value pairs, not typed relational rows). The `MemberData` store
is designed for simple member-scoped preferences and feature flags, not for structured multi-row data
like a vehicle list. Provisioning the table now ensures the migration is idempotent for future
adopters without forcing an unnecessary CarInfo schema change.

---

## 2026-06-12 - TestPluginHostContext Gains NoOpMemberExtensionStore and Default Invocation

### Decision

`TestPluginHostContext` in `CarInfoPluginSlice3Tests` gains two new property implementations:
`MemberData = new NoOpMemberExtensionStore()` (all methods are no-ops or return null/empty) and
`Invocation = default` (IsSelfService: false). A new private class `NoOpMemberExtensionStore`
implementing `IMemberExtensionStore` is added inline in the test file.

### Rationale

Adding two required properties to `IPluginHostContext` is source-breaking for all existing
implementors. The test context is the only non-production implementor. The no-op and default values
preserve all existing test assertions while satisfying the new contract without introducing any test
logic complexity.

---

## 2026-06-17 - carinfo.delete Switches from licensePlate to carId as Primary Key

### Decision

The `carinfo.delete` action slot's argument schema changes from accepting `licensePlate` (text) to
accepting `carId` (integer string). `CarInfoDataService` gains `DeleteCarByIdAsync(host, memberId,
carId, ct)` which resolves the car by `Id WHERE MemberId = @memberId` (ownership guard). The legacy
`DeleteCarAsync(…, licensePlate)` method is retained but is no longer called from the self-service
action path. The edit-tab text is updated to prefix each car line with `[{Id}]` so the UI can
populate the `carId` argument without a separate lookup.

### Rationale

Research confirmed (Q3/Q7) that the database `cars.Id` is a stable AUTOINCREMENT key independent of
`LicensePlate`. The new `carinfo.update` action may change a license plate, after which the old
`carinfo.delete` (keyed on plate) would fail with "not-found". Using `carId` as the delete key
makes add-update-delete sequences atomic and plate-change safe. Surfacing the `Id` in the edit-tab
text is the lowest-friction mechanism because no new API endpoint or contract change is needed.

---

## 2026-06-17 - carinfo.update Action Added for Self-Service Vehicle Editing

### Decision

A new `MemberActionSlot` with key `carinfo.update` is added to `CarInfoActionProvider.GetActionsAsync`
with `PermissionKey = "clubgear.plugin.carinfo.member.write"`. Its argument schema includes `carId`
(required), `make`, `color`, `licensePlate`, and all active dynamic `field.*` keys. `CarInfoDataService`
gains `UpdateCarAsync(host, memberId, carId, make, color, licensePlate, extraFields, ct)` which:
(1) verifies car ownership via `Id WHERE MemberId`, (2) checks for a plate collision with another
car of the same member before changing the plate, (3) issues a single `UPDATE cars SET … WHERE Id`,
and (4) calls the existing `UpsertFieldValuesAsync` for dynamic fields. No database migration is
needed.

### Rationale

Research confirmed (Q2) that the contract layer supports any action key — no new interface is
required. Research confirmed (Q7) that UPDATE by `Id` with a prior duplicate-plate check is the
clean strategy given the `UNIQUE INDEX` on `(MemberId, LicensePlate)`. Delete-and-reinsert was
rejected because it would change the car's `Id`, breaking any future reference to that vehicle (e.g.
external audit logs or future foreign-key additions).

---

## 2026-06-17 - carinfo.field.define MemberActionSlot Removed

### Decision

The `MemberActionSlot` with key `carinfo.field.define` and its handler in `CarInfoActionProvider`
are removed. Field template management is handled exclusively through `CarInfoAdminPanelProvider`
via the `field.upsert` admin panel command.

### Rationale

Research confirmed (Q6) that `carinfo.field.define` is an exact duplicate of the admin panel
command — same method call, no additional guards or logic. Keeping a duplicate action slot with
`PermissionKey = "members.manage"` in the member action panel confuses the UI surface and pollutes
the self-service action list for admins. The admin panel is the correct and already-established
surface for field template management.

---

## 2026-06-17 - CarInfoMemberCarsPageProvider Added for Admin Per-Member Vehicle View

### Decision

A new `CarInfoMemberCarsPageProvider` implementing `IPluginPageProvider` is added to
`CarInfoProviders.cgcs`. It is registered in `CarInfoPluginModule.RegisterContributions` via
`sink.AddPageProvider(...)`. The plugin manifest gains `"page.generic"` in its `extensionPoints`
array. The page key is `"carinfo.member-cars"`. Admins navigate to:
`/plugin-pages/clubgear.plugin.carinfo/carinfo.member-cars?entityKey={memberId}`. The `entityKey`
parameter carries the member ID as a string. `GetRowsAsync` returns one row per car for that member
with `Id`, `LicensePlate`, `Make`, `Color`, `UpdatedAtUtc`, and dynamic field columns.
`ExecuteCommandAsync` handles `car.add`, `car.edit` (entityKey = carId), and `car.delete`
(entityKey = carId), all gated by `RequiredPermission = "members.manage"`.

### Rationale

Research confirmed (Q5) that `IPluginPageProvider` is the correct existing contract for a tabular
per-member car admin view. The Inventar plugin demonstrates the exact same pattern (entityKey =
inventory number). Using the generic page mechanism avoids introducing a new host contract or view,
keeps the admin car data editable through the same modal/command infrastructure that already exists,
and aligns with the established `AddPageProvider` extension point on `IPluginContributionSink`.

---

## 2026-06-12 - New CarInfoSelfServiceIntegrationTests Cover Permission Key and IsSelfService Signal

### Decision

A new test class `CarInfoSelfServiceIntegrationTests` is added to
`tests/ClubGear.ArchitectureTests/`. It uses in-memory SQLite (same pattern as
`CarInfoPluginSlice3Tests`) and covers three scenarios: (1) `GetActionsAsync` with a self-service
invocation context emits the `clubgear.plugin.carinfo.member.write` permission key on the add and
delete slots; (2) `ExecuteAsync` for `carinfo.add` with a valid self-service context succeeds; (3)
`ExecuteAsync` for `carinfo.delete` with an unknown license plate returns `"not-found"`.

### Rationale

Research confirmed (Q5) that no existing test exercises the self-service vehicle CRUD path with
real plugin code. The existing `SelfServicePluginActionApiTests` uses a fake slot service. The new
tests directly instantiate `CarInfoActionProvider` and `CarInfoDetailCardProvider` against an
in-memory database with a `TestPluginHostContext` where `IsSelfService = true`, providing the
missing coverage without requiring a full ASP.NET integration test.
