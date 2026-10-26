# Plan: ServiceBook – Edit-Cards Grouping and Dependency Guard

Working directory: `.crispy-worktree/`
File root: `.crispy-worktree/`
Execute prefix: `cd /Users/max/Code/ClubGear/plugins/ServiceBook/.crispy-worktree &&`

Primary command path:
- Install: `dotnet restore /Users/max/Code/ClubGear/ClubGear.csproj`
- Test: `dotnet test /Users/max/Code/ClubGear/tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj`
- Run: `dotnet run --project /Users/max/Code/ClubGear/ClubGear.csproj`
- Plugin build: `dotnet build /Users/max/Code/ClubGear/plugins/ServiceBook/ServiceBook.Plugin.csproj`
- Plugin package: `bash /Users/max/Code/ClubGear/scripts/package-plugin.sh /Users/max/Code/ClubGear/plugins/ServiceBook`

---

## Slice 1 — Edit-view tab grouping

**Capability**: Member admin edit view renders CarInfo and ServiceBook inside one grouped Bootstrap card with inner tabs instead of a flat tab list.

- [ ] **1.1** In `/Users/max/Code/ClubGear/Views/Members/Edit.cshtml` line 18, change `ViewData["PluginSlotMode"] = "edit"` to `ViewData["PluginSlotMode"] = "edit-cards"`.
  - DoD: The file contains exactly one assignment of `"edit-cards"` to `PluginSlotMode`; `dotnet build /Users/max/Code/ClubGear/ClubGear.csproj` succeeds with no errors.

- [ ] **1.2** Confirm no other `.cshtml` file in `Views/Members/` overrides `PluginSlotMode` to `"edit"` for the edit view.
  - DoD: A grep for `PluginSlotMode.*"edit"` in `Views/Members/` returns no results (only `"edit-cards"` or unrelated occurrences).

---

## Slice 2 — PluginDependency contract type

**Capability**: A canonical, parseable value type for expressing a versioned plugin dependency (`"moduleId@>=x.y.z"`), usable by the manifest layer and the activation guard.

- [ ] **2.1** Create `/Users/max/Code/ClubGear/Contracts/Plugin/PluginDependency.cs` as `sealed record PluginDependency(string ModuleId, Version MinVersion)` with:
  - `static bool TryParse(string value, out PluginDependency? result)` — splits on the first `@`, strips optional `>=` prefix, calls `Version.TryParse`; returns `false` if the format is invalid.
  - `override string ToString()` → `"ModuleId@>=MinVersion"`.
  - DoD: `dotnet build /Users/max/Code/ClubGear/ClubGear.csproj` succeeds; the type is `public` and in the `ClubGear.Contracts.Plugin` namespace (or the namespace used by existing types in that folder).

- [ ] **2.2** Verify round-trip: `TryParse("clubgear.plugin.carinfo@>=1.0.5", out var dep)` returns `true` with `dep.ModuleId == "clubgear.plugin.carinfo"` and `dep.MinVersion == new Version(1, 0, 5)`; `dep.ToString()` returns `"clubgear.plugin.carinfo@>=1.0.5"`.
  - DoD: Manual check or unit test passes; `TryParse` on a string missing `@` returns `false`; `TryParse` on a string with a bad version number returns `false`.

---

## Slice 3 — Dependency parsing and DB persistence end-to-end

**Capability**: Installing a plugin ZIP whose `plugin.json` contains a `"dependencies"` array parses those entries into `PluginDependency` values, stores them as `DependenciesJson` on `PluginStatusRecord`, and the column is present in SQLite after startup migration.

- [ ] **3.1** In `/Users/max/Code/ClubGear/Contracts/Plugin/PluginManifest.cs`, add a non-positional init-only property (does not change the existing positional constructor):
  ```csharp
  public IReadOnlyList<PluginDependency> Dependencies { get; init; } = Array.Empty<PluginDependency>();
  ```
  - DoD: `dotnet build /Users/max/Code/ClubGear/ClubGear.csproj` succeeds; the 9-parameter positional constructor still compiles without changes at call sites.

- [ ] **3.2** In `/Users/max/Code/ClubGear/Services/Plugins/Manifest/PluginManifestParser.cs`, after the existing `extensionPoints` parsing block, add logic to read `"dependencies"` as a string array (missing key → empty, not an error), call `PluginDependency.TryParse` on each entry, accumulate errors for malformed entries, and set `manifest.Dependencies` via `with { Dependencies = dependencies }`.
  - DoD: Parser fed `"dependencies": ["clubgear.plugin.carinfo@>=1.0.5"]` produces a manifest with one `PluginDependency` entry; parser fed a malformed entry (`"bad-format"`) adds an error message to the errors list; parser fed no `"dependencies"` key produces an empty `Dependencies` list with no error.

- [ ] **3.3** In `/Users/max/Code/ClubGear/Models/PluginStatusRecord.cs`, add one property:
  ```csharp
  [Required]
  public string DependenciesJson { get; set; } = "[]";
  ```
  - DoD: `dotnet build /Users/max/Code/ClubGear/ClubGear.csproj` succeeds; no null-reference warnings on the new property.

- [ ] **3.4** Create `/Users/max/Code/ClubGear/Data/Migrations/202606300101_AddPluginDependencies.cs` following the same pattern as the existing `AddPluginCategory_202606090101` migration. The `ApplyAsync` method must:
  - Run `PRAGMA table_info(PluginStatusRecords)` and skip the `ALTER TABLE` if `DependenciesJson` already exists.
  - Run `ALTER TABLE PluginStatusRecords ADD COLUMN DependenciesJson TEXT NOT NULL DEFAULT '[]'` only when the column is absent.
  - DoD: File exists; calling `ApplyAsync` twice on the same DB completes without error (idempotent); `dotnet build /Users/max/Code/ClubGear/ClubGear.csproj` succeeds.

- [ ] **3.5** In `/Users/max/Code/ClubGear/Services/Core/ApplicationSeeder.cs`, register the new migration by calling `AddPluginDependencies_202606300101.ApplyAsync(...)` immediately after the existing `AddPluginCategory` migration call.
  - DoD: `dotnet run --project /Users/max/Code/ClubGear/ClubGear.csproj` starts without migration errors; the SQLite `PluginStatusRecords` table contains a `DependenciesJson` column with default `'[]'`.

- [ ] **3.6** In `/Users/max/Code/ClubGear/Services/Plugins/Installation/PluginInstallerService.cs`, update both `PersistSuccessAsync` and `PersistFailureAsync` to serialize `manifest.Dependencies` (via their `ToString()` representations) into `record.DependenciesJson`.
  - DoD: After installing a plugin ZIP with `"dependencies": ["clubgear.plugin.carinfo@>=1.0.5"]`, querying the stored `PluginStatusRecord` shows `DependenciesJson` deserialises to `["clubgear.plugin.carinfo@>=1.0.5"]`; `dotnet test /Users/max/Code/ClubGear/tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj` passes.

---

## Slice 4 — Activation dependency guard

**Capability**: `PluginLifecycleService.ActivateAsync` reads `DependenciesJson` from the stored record, resolves each dependency against the runtime registry, and returns `(false, "dependency-not-met", <message>)` when a dependency is absent or below the required minimum version.

- [ ] **4.1** In `/Users/max/Code/ClubGear/Services/Plugins/Runtime/PluginLifecycleService.cs`, update the `Clone` helper to copy `DependenciesJson` from the source record.
  - DoD: `dotnet build /Users/max/Code/ClubGear/ClubGear.csproj` succeeds; no property is silently dropped during clone.

- [ ] **4.2** In the same file, add a dependency guard early in `ActivateAsync` (before the assembly load), executed after the `existing` record is fetched:
  - Deserialize `existing.DependenciesJson` as `string[]` (null/missing → treat as `[]`).
  - For each entry, call `PluginDependency.TryParse`; if parse fails, skip (tolerate stored format drift).
  - Look up `dep.ModuleId` in `_runtimeRegistry`; if absent, return `(false, "dependency-not-met", "Voraussetzung '{dep.ModuleId}' (>= {dep.MinVersion}) muss aktiv sein, bevor dieses Plugin aktiviert werden kann.")`.
  - If present but `runtime.PluginVersion < dep.MinVersion`, return `(false, "dependency-not-met", "Voraussetzung '{dep.ModuleId}' erfordert Version >= {dep.MinVersion}, aktiv ist {runtime.PluginVersion}.")`.
  - DoD: Four unit-test scenarios all pass:
    - (a) `DependenciesJson = "[]"` → activates normally.
    - (b) Dependency present in registry at exactly `MinVersion` → activates normally.
    - (c) Dependency absent from registry → returns `"dependency-not-met"` with `ModuleId` in message.
    - (d) Dependency present but version lower than `MinVersion` → returns `"dependency-not-met"` with version detail in message.
  - `dotnet test /Users/max/Code/ClubGear/tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj` passes.

---

## Slice 5 — ServiceBook plugin manifest and module update

**Capability**: The ServiceBook plugin declares its CarInfo dependency in `plugin.json` and reports the updated version through `ServiceBookPluginModule`.

- [ ] **5.1** In `/Users/max/Code/ClubGear/plugins/ServiceBook/plugin.json`:
  - Add `"dependencies": ["clubgear.plugin.carinfo@>=1.0.5"]` as a top-level field.
  - Bump `"version"` to `"1.0.3"`.
  - DoD: `plugin.json` is valid JSON; contains `"version": "1.0.3"` and `"dependencies": ["clubgear.plugin.carinfo@>=1.0.5"]`.

- [ ] **5.2** In `/Users/max/Code/ClubGear/plugins/ServiceBook/ServiceBookPluginModule.cgcs`:
  - Bump the version to `new Version(1, 0, 3)`.
  - Set `Dependencies` via `with { Dependencies = new[] { new PluginDependency("clubgear.plugin.carinfo", new Version(1, 0, 5)) } }` (or equivalent init syntax).
  - DoD: `dotnet build /Users/max/Code/ClubGear/plugins/ServiceBook/ServiceBook.Plugin.csproj` succeeds; no compile errors.

- [ ] **5.3** Package the plugin and verify the manifest round-trips correctly.
  - DoD: `bash /Users/max/Code/ClubGear/scripts/package-plugin.sh /Users/max/Code/ClubGear/plugins/ServiceBook` completes without error; the output ZIP contains an updated `plugin.json` with version `1.0.3` and the `dependencies` array.

- [ ] **5.4** End-to-end smoke check: upload the packaged ZIP via the admin UI (or API), then attempt to activate ServiceBook while CarInfo is not active; assert the response contains status `"dependency-not-met"`.
  - DoD: The activation API returns a failure result with `status == "dependency-not-met"` and a message referencing `clubgear.plugin.carinfo`; `dotnet run --project /Users/max/Code/ClubGear/ClubGear.csproj` is running for this check.
