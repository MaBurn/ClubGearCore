# ADR: Extension Point Catalog

## Purpose

Persistent architectural decisions for the extension-point-catalog iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-10 - Extension-Point Constants Placed in Contracts Assembly as Public

### Decision

A new public static class `PluginExtensionPoints` is added to `Contracts/Plugin/PluginExtensionPoints.cs` in the `ClubGear.Plugin.Contracts` namespace. All seven known extension-point string values are defined as `public const string` fields. A `public static IReadOnlySet<string> All` property and a `public static bool IsKnown(string)` method expose the catalog for consumers. `ContractVersion.Current` advances from `1.3.0` to `1.4.0`.

### Rationale

The task describes extension-points as "stable contract values." Plugin authors already reference `Contracts/Plugin` for every other stable API surface (`IPluginModule`, `IPluginContributionSink`, `PluginManifest`, etc.). Placing the constants there gives plugin authors a typed reference to the known values, eliminating scattered inline string literals. An `internal` placement or a `Services/Plugins`-only class would deny plugin authors access to the constants, defeating the purpose of a published catalog. The versioning cost (minor bump to `1.4.0`) is appropriate under the established ADR policy: adding a new public type to the Contracts assembly is an additive API surface change. The pattern mirrors `PermissionKeys.cs` (const fields + private HashSet + public read-only accessor + query method) for consistency within the codebase.

---

## 2026-06-10 - Extension-Point Validation Added to PluginManifestParser, Not Runtime Loader

### Decision

Allowlist validation of the `extensionPoints` array is performed inside `PluginManifestParser.Parse()`, immediately after `ReadStringArray`. Each value is checked against `PluginExtensionPoints.IsKnown`. An unrecognized value appends an error message and causes the parse result to be invalid. The runtime contribution sink (`PluginLoader`, `PluginContributionCollector`) is not changed.

### Rationale

The manifest parser is already the single choke-point for all manifest contract enforcement (required fields, version format, `requiredCoreVersion` syntax). Putting allowlist validation there ensures that a plugin with an unrecognized extension-point string is rejected at install time, before any assembly loading or DB writes occur. Enforcing the same check at the runtime loader would require adding a cross-reference between contribution types and extension-point strings, which would be a larger, more invasive change not justified by current requirements. The parser validation is sufficient to make the catalog a binding contract.

---

## 2026-06-10 - Category Allowlist Validation Explicitly Deferred

### Decision

The `category` field in `plugin.json` continues to accept any non-empty string without validation. No allowlist check is added in this iteration.

### Rationale

The task scope is the extension-point catalog. The existing test `ZipInstall_PersistsCategory_FromManifest_OnFailure` deliberately uses an arbitrary category value (`"vehicle-data"`) to verify that an incompatible-version install still persists the declared category. Adding category allowlist validation now would break that test and would require either updating the test fixture to a known category or adding `"vehicle-data"` to a category catalog, both of which are out of scope. Category validation is a natural follow-on iteration.

---

## 2026-06-10 - CarInfo Manifest Updated to Declare member.badge

### Decision

`plugins/CarInfo/plugin.json` and `plugins/CarInfo/CarInfoPluginModule.cgcs` are updated to add `"member.badge"` to the `extensionPoints` array. The `selfservice.profile` declaration is retained.

### Rationale

CarInfo registers a `PluginMemberSlotKind.StatusBadge` provider via `AddMemberProvider` but its manifest omitted `"member.badge"`. Once parser validation is active this would cause CarInfo's own manifest to fail. The fix aligns the manifest declaration with the actual contribution. `selfservice.profile` maps to `PluginExtensionPoints.SelfServiceProfile` (a recognized constant declared for future use) and is valid to retain as a forward-looking declaration of intent.

---

## 2026-06-10 - Plugin Authoring Guide Extended with Extension-Point Catalog Table

### Decision

A new Section 4a is inserted into `docs/architecture/plugin-authoring-guide.md` between the existing Section 4 (Manifest-Datei) and Section 5 (Erlaubte Host-Interaktion). It contains a table mapping each extension-point string to its category, provider interface or contribution method, and the `PluginExtensionPoints` constant name. The section also documents that unknown values are rejected at install time and that `selfservice.profile` is reserved.

### Rationale

The guide currently mentions extension-point strings only as opaque examples in two code snippets. No prose or table explains what each string means, which interface it corresponds to, or that an allowlist exists. Plugin authors cannot write a compliant manifest without this information. The new section closes that gap and makes the guide the authoritative reference for the catalog, consistent with how the guide already documents permissions, contribution types, and packaging requirements.
