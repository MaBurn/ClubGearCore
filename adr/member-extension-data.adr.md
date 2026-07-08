# ADR: Member Extension Data

## Purpose

Persistent architectural decisions for the member-extension-data iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-11 - IMemberExtensionStore Added as a New Contract Interface in Contracts/Plugin/

### Decision

A new public interface `IMemberExtensionStore` is added to `Contracts/Plugin/IMemberExtensionStore.cs` in the `ClubGear.Plugin.Contracts` namespace. It exposes five methods: `WriteAsync`, `ReadAsync`, `ReadAllAsync`, `DeleteAsync`, and `DeleteAllAsync`. All methods are scoped implicitly to the calling plugin's own table by the host implementation. The interface is exposed as a new `MemberData` property on `IPluginHostContext`.

### Rationale

The only existing mechanism for member-scoped data access is raw SQL via `IPluginDataStore.QueryAsync` / `ExecuteAsync` with a manually composed `WHERE MemberId = @memberId` clause (the CarInfo pattern). This approach is correct but forces every plugin to reinvent the same boilerplate and to understand the SQL schema. A typed, named-parameter API on top of the same underlying `PluginDataStore` / `PluginSchemaNamePolicy` stack gives member-profile plugins a first-class, discoverable surface without introducing any new persistence engine or bypassing the existing isolation guardrails. The existing `PluginSchemaNamePolicy.ValidateSql` continues to enforce the per-plugin table-prefix restriction on every SQL call made through `PluginMemberExtensionStore`.

---

## 2026-06-11 - PluginInvocationContext Added to IPluginHostContext; IsSelfService Signal Exposed to Plugins

### Decision

A new `readonly record struct PluginInvocationContext(bool IsSelfService, int? SelfServiceMemberId)` is added to `Contracts/Plugin/PluginInvocationContext.cs`. `IPluginHostContext` gains a new `Invocation` property of this type. `PluginRuntimeAdapter.CreateBridge` is extended with an optional `PluginInvocationContext invocation = default` parameter (default: `{ IsSelfService = false, SelfServiceMemberId = null }`). `MemberPluginSlotService.GetSlotsAsync` gains a corresponding optional parameter and passes the value through when the call originates from `SelfServiceApiController`.

### Rationale

Research confirmed (Q2) that plugins have no way to distinguish admin-initiated from self-service-initiated invocations. The `ClaimsPrincipal` is held privately in `PluginRuntimeBridge._user` and is never surfaced to plugins. A dedicated value-type signal on the host context is the minimal, backward-compatible addition: the default value produces current behavior for all existing call sites. Plugins that do not consume `Invocation` are unaffected. The struct is placed in Contracts because it is part of the plugin-visible API surface; it does not introduce any dependency on ASP.NET or `ClaimsPrincipal`.

---

## 2026-06-11 - Per-Plugin Member Permission Keys Follow moduleId.member.read / moduleId.member.write Convention

### Decision

A new static class `PluginPermissionKeys` is added to `Contracts/Plugin/PluginPermissionKeys.cs`. It defines two static factory methods: `MemberRead(string moduleId)` returning `"{moduleId}.member.read"` and `MemberWrite(string moduleId)` returning `"{moduleId}.member.write"`. These are not registered as core permission keys in `PermissionKeys.cs`. Plugins declare them in their `Permissions` manifest array and enforce them via the existing `bridge.HasPermissionAsync` path.

### Rationale

Research confirmed (Q3) that no `{moduleId}.member.read` or `{moduleId}.member.write` convention exists today. CarInfo uses only core permission keys (`members.read`, `members.manage`, `selfservice.profile.edit`). Placing the convention in Contracts as a static helper prevents key-naming drift across plugins while keeping the keys out of the core `PermissionKeys` static class (which defines only host-owned, system-wide permissions). The enforcement pathway — `IExtensionPermissionFacade.HasPermissionAsync` checking the plugin's declared permissions — is unchanged.

---

## 2026-06-11 - EnsureMemberExtTableAsync Extension Method Added to IPluginMigration.cs

### Decision

A public static extension method `EnsureMemberExtTableAsync(this IPluginMigrationContext, CancellationToken)` is added directly inside `Contracts/Plugin/IPluginMigration.cs`. It issues a `CREATE TABLE IF NOT EXISTS` for the `{prefix}member_ext` table with `(MemberId INTEGER NOT NULL, FieldKey TEXT NOT NULL, Value TEXT NULL, UpdatedAtUtc TEXT NOT NULL, PRIMARY KEY (MemberId, FieldKey))`. The host does not create this table automatically at activation time.

### Rationale

Plugins that choose to use `MemberData` must own their schema, consistent with GR-004 (plugin data is isolated; plugin migrations are tracked separately from core). Having plugins call a single shared helper rather than hand-crafting the DDL every time ensures schema consistency across all adopters. The host does not create the table automatically because not all plugins will use `MemberData`; automatic creation would generate unused tables for plugins that rely only on custom schemas. The extension method belongs in Contracts because it is part of the plugin authoring API.

---

## 2026-06-11 - ContractVersion Bumped from 1.4.0 to 1.5.0

### Decision

`Contracts/Plugin/ContractVersion.cs` is updated: `Current` advances from `new Version(1, 4, 0)` to `new Version(1, 5, 0)`. `MinimumSupported` remains `new Version(1, 0, 0)`.

### Rationale

This iteration adds three new public types to the Contracts assembly (`IMemberExtensionStore`, `PluginInvocationContext`, `PluginPermissionKeys`) and modifies one existing public interface (`IPluginHostContext`, gaining two new properties). Per the established policy (mirroring the extension-point-catalog ADR), adding new public types and interface members to the Contracts assembly warrants a minor version bump. All changes are additive; no existing method signature is removed or modified. Existing plugin assemblies compiled against `1.4.0` remain loadable.

---

## 2026-06-11 - No Core Database Migration Required for This Iteration

### Decision

No new `ApplicationDbContext` migration is added. The `plugin_{moduleId}_member_ext` table is exclusively created by plugin-provided `IPluginMigration` implementations using the `EnsureMemberExtTableAsync` helper.

### Rationale

Research confirmed (Q5) that all existing plugin tables are created through `IPluginMigration` with no FK into `Members` and no registration in `ApplicationDbContext.OnModelCreating`. This iteration follows the same pattern. The SQLite foreign-key pragma is not enabled by the host, so adding `REFERENCES Members(Id)` would provide no enforcement; a bare `INTEGER MemberId` is sufficient and consistent with the CarInfo schema.
