# Core platform backlog

Audience: Maintainers and architects working on the plugin host, the contract and the loader
Scope: Core-owned items in the plugin-platform / host layer — the seams plugins are loaded through
Last-Validated: 2026-09-14
Source-Commit: fbf4249

Owner for every item on this page is `core`: the change lands in this repository. The
classification rules R1–R8, the status and priority vocabularies and the crosswalk from the retired
June 2026 ID space are in [`README.md`](README.md).

**Each ID is *defined* exactly once, in the item table below and its matching detail section.**
Any other occurrence of an ID on this page is a cross-reference to that single definition.

The organising decision of this page is **CORE-13, the one provider seam**. The user asks for
service plugins consumable by other plugins; the proposal-2026-08 design asks for a
`channel-provider` plugin type; a 2026-06-16 ADR block asks for a notification-channel contract.
Those are one thing, not three, and the shipped contract already carries most of it:
`IPluginContributionSink.AddService(PluginServiceContribution)` is a required, non-default member
(`Contracts/Plugin/PluginContributions.cs:7,31`) that the loader already collects
(`Services/Plugins/Runtime/PluginLoader.cs:130,140,160`) onto `RegisteredPluginRuntime.Services`
(`Services/Abstractions/IPluginRegistryReader.cs:17,23`). **It has no consumer anywhere in the
host.** The seam is carved and inert; only the host side is missing. That is what makes this work
additive rather than a contract break.

## Items

| ID | Title | Status | Priority | Owner | Source | Classification rationale | Evidence |
|---|---|---|---|---|---|---|---|
| CORE-03 | Plugin surfaces and host contracts — remaining increments | `partial` | P1 | `core` | TodoFeatures, proposal-2026-08 | R5: one host-side page/surface mechanism, so both plugin kinds render through it. | `Contracts/Plugin/IPluginPageProvider.cs`, `Contracts/Plugin/PluginExtensionPoints.cs:5-15`, `2405439` |
| CORE-13 | One provider contribution seam | `partial` | **P0** | `core` | June 2026 list, proposal-2026-08 `channel-provider`, user's service plugins | R5: two plugin kinds, **one** mechanism — the provider seam is the mechanism. | `Contracts/Plugin/PluginContributions.cs:7,31`, `Services/Plugins/Runtime/PluginLoader.cs:130,140,160`, `Services/Abstractions/IPluginRegistryReader.cs:23` |
| CORE-14 | Provider selection and precedence | `open` | **P0** | `core` | R3, R5 | R3: a storage plugin must be able to supersede *and disable* the core default, which needs explicit selection, not DI last-wins. | `Services/Core/NotificationService.cs:23,32` |
| CORE-15 | Plugin settings and secret store | `open` | P0 | `core` | June 2026 list, `DECISIONS.md` open item 6 | R8: the host itself needs it and every credentialed connector needs it — two independent consumers. | `docs/architecture/proposal-2026-08/DECISIONS.md:170`, `ADRs/0001-plugin-development-guardrails.md:211,223` |
| CORE-16 | Make the plugin boundary real | `delivered` | **P0** | `core` | June 2026 list, proposal-2026-08 D5 | R6-adjacent: an isolation boundary that must hold before and while any plugin runs is permanent core. | `Services/Plugins/Runtime/PluginIsolationScanner.cs`, `tests/ClubGear.ArchitectureTests/PluginLoadTimeIsolationTests.cs`, `bba6cf2`, `ffe5622` |
| CORE-17 | Provider lifecycle: drain in-flight calls before ALC unload | `delivered` | P0 | `core` | June 2026 list residue, proposal-2026-08 Q3 | R6: only the host can sequence unload; a plugin cannot drain itself. | `Services/Plugins/Runtime/PluginActivityTracker.cs`, `tests/ClubGear.ArchitectureTests/PluginDeactivationDrainTests.cs`, `087db19` |
| CORE-18 | Instantiate contributions through DI, not `Activator.CreateInstance` | `delivered` | P0 | `core` | proposal-2026-08 Q3 | R5: provider-to-provider consumption is impossible while a provider receives no host context. | `Services/Plugins/Runtime/PluginContributionActivator.cs`, `Contracts/Plugin/ContractVersion.cs`, `9bd0197` |
| CORE-19 | Manifest: category allowlist plus service/functional kind | `partial` | P1 | `core` | June 2026 list residue, R5 | R5: the two plugin kinds must be declarable in the manifest before the host can treat them differently. | `Contracts/Plugin/PluginManifest.cs:26`, `tests/ClubGear.ArchitectureTests/PluginInstallerServiceTests.cs:206,218` |
| CORE-20 | Dashboard widget extension point | `open` | P2 | `core` | June 2026 list | R8: the host owns the dashboard; a widget seam serves every functional plugin, so more than two consumers. | `Contracts/Plugin/PluginExtensionPoints.cs:5-15` |
| CORE-21 | Plugin health, diagnostics and data lifecycle | `open` | P2 | `core` | June 2026 list | R6: activation risk checks, uninstall and data deletion are host duties; a plugin cannot be trusted to police itself. | `Services/Plugins/Uninstall/PluginUninstallService.cs`, `Services/Plugins/Status/DbPluginStatusStore.cs` |
| CORE-22 | Documentation integrity sweep | `delivered` | **P0** | `core` | this consolidation | P0 by definition: it corrects a false record. Core-owned because every affected file is core documentation. | `ADRs/consolidated/plugin-platform.adr.md:923-1014`, `docs/architecture/proposal-2026-08/DECISIONS.md:138-140`, `ADRs/README.md:9`, `plugins/aktivierung.md:48,50`, `docs/plugin-contract.md:89-99` |
| CORE-23 | Repository hygiene: submodule wiring | `delivered` | P1 | `core` | this consolidation, verified against the working tree | R7: submodule packaging is the standing rule, so defects in it are core repository work. | `tests/ClubGear.ArchitectureTests/PluginBoundaryTests.cs`, `.gitlab-ci.yml`, `Readme.md:73-80` |
| CORE-24 | Manifest consistency: one version, checked | `open` | P1 | `core` | CAR-02, 2026-09-09 | R6: only the host reads both declarations, so only the host can compare them. | `Services/Plugins/Manifest/PluginManifestParser.cs`, `Services/Plugins/Runtime/PluginLoader.cs`, `plugins/CarInfo/plugin.json`, `plugins/CarInfo/CarInfoPluginModule.cgcs` |
| CORE-25 | Secure first-run installation assistant | `open` | P1 | `core` | July 2026 install plan | R8: bootstrap identity, system configuration and global pre-install routing are host responsibilities. | [`inbox/install/2026-07-install-assistant.md`](inbox/install/2026-07-install-assistant.md), `Models/SystemConfigEntry.cs`, `Program.cs` |
| CORE-27 | Plugin authoring guide and project template | `open` | P2 | `core` | CG-BL-050, CG-BL-051 | R7: the host repository owns the contract, packaging script and compliant repository scaffold. | `docs/plugin-contract.md`, `scripts/package-plugin.sh`, [`README.md`](README.md) crosswalk |
| CORE-28 | Architecture tests fail to compile against a plugin's checked-out working tree, not its declared contract floor | `open` | P1 | `core` | found 2026-10-03 while verifying the PageKey routing fix (contract 1.14.0) | R6: only the host can decide what a plugin build is allowed to assume about core test compilation; today it assumes the exact source on disk. | `tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj` (`ProjectReference` to `plugins/CarInfo`, `plugins/Finance`, `plugins/Inventar`, `plugins/ServiceBook`), `CarInfoSlice1-5Tests.cs`, `FinanceSlice6-8*Tests.cs`, `PluginSlice3GroupKeyTests.cs`, `PluginNavEntryServiceTests.cs` |

Fourteen items. No ID on this page appears in `core-capabilities.md` or `plugins.md`.

## CORE-13 — One provider contribution seam

**Status `partial` · Priority P0 · Owner `core` · Keystone.**

### Increment 1 — shipped 2026-09-02

Contract `1.12.0`. Where to pick this up:

| Landed | File |
|---|---|
| `PluginCapabilityKeys` allowlist + `PluginCapabilityArity` | `Contracts/Plugin/PluginCapabilityKeys.cs` |
| `Resolve<T>` / `ResolveAll<T>` / `GetRegistrations` | `Services/Abstractions/IPluginProviderRegistry.cs` |
| Implementation over the runtime registry | `Services/Plugins/Runtime/PluginProviderRegistry.cs` |
| Load-time rejection of an unknown key | `Services/Plugins/Runtime/PluginLoader.cs` |
| 11 tests | `tests/ClubGear.ArchitectureTests/PluginProviderSeamTests.cs` |

`PluginServiceContribution` finally has a consumer. Decisions and their reasoning are in
`ADRs/consolidated/plugin-platform.adr.md` under the three 2026-09-02 entries; the plugin-author
view, including the one behavioural break, is `docs/plugin-contract.md` under 1.12.0.

### Increment 2 and later — not started

Deliberately left out, each with the reason:

1. **A provider cannot consume another provider.** `IPluginHostContext` has no `Providers` member,
   because `CreateMemberProvider` instantiates through a parameterless
   `Activator.CreateInstance` and cannot hand the context in. Needs **CORE-18** first. This is the
   part that makes service plugins consumable *by other plugins* (R5), so the seam is not finished
   without it.
2. **No administrator selection.** `Resolve` picks the first provider by module id. **CORE-14.**
3. **`NotificationService` still uses `ToDictionary`.** Untouched on purpose: no plugin can
   contribute an `INotificationChannel` today, so the collision is unreachable there. Swap it in
   the increment that routes channels through this seam, not before.
4. **No drain on unload** (**CORE-17**) and **no widened boundary check** (**CORE-16**). Both
   remain open, and both are why this increment is `partial` rather than `shipped`: the seam is
   usable and tested, but widening the provider surface without them widens the hole.

The boring, obvious solution: extend the existing audit-sink / identity-provider contribution
pattern and finally give `PluginServiceContribution` a consumer. Do not invent a framework.

A plugin declares a provider in `RegisterContributions`:

```
sink.AddService(new PluginServiceContribution(
    Key: "storage.blob",              // capability key, host-validated allowlist
    ServiceType: "Acme.S3StorageProvider"));
```

The loader already collects this. What is missing is a host service, `IPluginProviderRegistry`:

- **`Resolve<T>(capabilityKey)`** — the *selected* provider for a single-active capability
  (storage, document store), or the core default when none is selected. Selection is CORE-14.
- **`ResolveAll<T>(capabilityKey)`** — every registered provider, for fan-out capabilities
  (audit sinks, notification channels).
- **Reachable from a plugin** through a new `IPluginHostContext`
  (`Contracts/Plugin/IPluginHostContext.cs`) member, so one plugin can consume another plugin's
  provider without either knowing the other. This is the "service plugins consumable by other
  plugins" requirement, and it is the reason the seam is one mechanism (R5) rather than a separate
  channel-provider type.
- **Capability keys are a host-validated allowlist**, not free text. An unknown key is a load-time
  rejection, not a silent no-op.

The intended consumers, all of which become plugins on the far side of this seam: notification
channels (Matrix, Telegram, WhatsApp, SMS), storage providers (S3, Nextcloud), document providers
(Nextcloud), and the authentication-scheme provider produced by the API-key extraction recorded in
[`core-capabilities.md`](core-capabilities.md).

### The collision finding — confronted, not assumed away

`INotificationChannel` lives in the core namespace
(`Services/Abstractions/INotificationService.cs:8-10`) and `NotificationService` builds a
name-keyed `IReadOnlyDictionary` **in its constructor**
(`Services/Core/NotificationService.cs:23,32`):

```
_channels = channels.ToDictionary(c => c.ChannelName, StringComparer.OrdinalIgnoreCase);
```

`ToDictionary` **throws on a duplicate key**. Two plugins both claiming `"email"` — or one plugin
claiming a name the core already uses — is therefore a *scope-construction failure*, not an
override: the DI container fails to build the service, and the failure surfaces far from its cause.
There is no precedence mechanism today, in this path or any other. CORE-13 must replace that
constructor-time dictionary with a lookup that tolerates collisions and defers to CORE-14's
selection.

### Blockers — all three cleared 2026-09-05

CORE-13 could not ship before **CORE-16**, **CORE-17** and **CORE-18**. All three are now
`delivered` (`bba6cf2`, `ffe5622`, `087db19`, `9bd0197`), so **CORE-13 increment 2 is unblocked**
and nothing in this list stands in front of it any more. The reasons are kept because they explain
why the seam was cut this way:

- **CORE-16** — widening the provider surface without widening the boundary check widens the hole.
- **CORE-17** — a provider call in flight when the load context unloads is an undefined-behaviour
  crash, and providers are called far more often than background jobs are.
- **CORE-18** — a provider instantiated with no DI and no host context cannot consume another
  provider, which is the whole point of the seam.

This item supersedes the phantom 2026-06-16 notification-channel ADR block; see CORE-22.

## CORE-14 — Provider selection and precedence

**Status `open` · Priority P0 · Owner `core`.**

R3 says local file storage stays in the core as the **default** but a storage plugin must be able
to **supersede and disable** it. That is impossible with implicit DI last-wins registration, which
is neither observable nor administrable. This item defines the selection.

**Precedence is admin-selected, per capability key, and the core implementation is the default.**

- Each single-active capability key has exactly one *active* provider at a time. Until an
  administrator changes it, that is the core implementation — an unconfigured system behaves
  exactly as it does today, and installing a plugin never silently changes where files are stored.
- Selecting a plugin provider for a key **supersedes and disables** the core default for that key.
  That is the R3 requirement stated as a mechanism.
- Fan-out keys have no selection: `ResolveAll` returns every registered provider, and each is
  enabled or disabled individually.
- Selection is persisted host-side and shown in plugin administration, so "which storage is live"
  is answerable without reading DI registration order.

**Duplicate keys — the explicit answer.** When two providers claim the same capability key:

1. **Registration never throws.** All claimants are recorded, including the core default. This is
   the direct fix for the `NotificationService` constructor-time collision recorded under CORE-13:
   a duplicate must be a resolvable state, not a scope-construction failure.
2. **For a single-active key**, only the administrator-selected provider is resolved. If none is
   selected, the core default wins; if there is no core default and no selection, `Resolve` returns
   nothing and the caller degrades — it does not pick arbitrarily.
3. **For a fan-out key**, all enabled claimants run. Duplicate names are disambiguated by module
   id, so two plugins may both offer a channel called `"email"`.
4. **The collision is surfaced**, not hidden: an unresolved duplicate on a single-active key is a
   visible warning in plugin administration, at install time, naming both claimants.

## CORE-16 — Make the plugin boundary real

**Status `delivered` · Priority P0 · Owner `core`.**

**Delivered 2026-09-05** — `bba6cf2` (load-time isolation gate) and `ffe5622` (the `plugins/**`
source guardrail). Both holes below are closed; the text is kept so the original defect stays
readable.

- The scan moved out of the adapter into `Services/Plugins/Runtime/PluginIsolationScanner.cs` and
  now runs at load time, before `RegisterContributions` and again over every declared contribution
  type. A violation refuses the plugin at activation and surfaces against the owning module id in
  plugin administration, rather than throwing the first time an administrator opens the page.
- The guardrail exists as a **source** scan over `plugins/**/*.cs` and `*.cgcs`
  (`tests/ClubGear.ArchitectureTests/PluginBoundaryTests.cs`). Source, not reflection, because
  `IamOidc` is not `ProjectReference`d by the test project and a reflection test would have skipped
  it silently. It reads its forbidden set from `PluginIsolationScanner.ForbiddenNamespaces`, so the
  runtime gate and the build gate cannot drift apart.

One path stayed open **by design**: the module entry point keeps its parameterless constructor,
because `IPluginHostContext` is built from `module.Manifest` and is only reachable from an already
constructed module. It is scanned before instantiation instead, and the exception is documented in
`docs/plugin-contract.md`.

The original defect, verified at the time:

- **`EnsureIsolated` is bypassed on the provider path.** The check runs only from `RunAsync`
  (`Services/Plugins/Runtime/PluginRuntimeAdapter.cs:112`) and `InvokeAsync`
  (`:130`), and it inspects only the *instance fields of the delegate's target type*. Providers
  created through `PluginRegistry.CreateMemberProvider`
  (`Services/Plugins/Runtime/PluginRegistry.cs:47-68`) never pass through either, so they are not
  checked at all.
- **The `plugins/**` → core-`DbContext` architecture test does not exist.** The guardrail that
  plugin code must not reach the core `ApplicationDbContext` is written down but not enforced by
  anything in `tests/ClubGear.ArchitectureTests/`.

Acceptance: the isolation check covers every path by which plugin code is instantiated or invoked,
including providers, and an architecture test fails the build when plugin code references the core
`DbContext`. This must land **before** CORE-13 widens the provider surface.

## CORE-17 — Provider lifecycle: drain in-flight calls before ALC unload

**Status `delivered` · Priority P0 · Owner `core`.**

**Delivered 2026-09-05** — `087db19`. Deactivation now seals the module inside the registry lock,
drains the calls still inside it outside that lock, then unloads **unconditionally**: a bounded
wait, never an unload that can be refused, so a stuck plugin cannot pin the host. On overrun the
module is unloaded anyway, a warning names the module id and the in-flight count, and the same note
lands on `PluginStatusRecord.LastError` so an operator sees it in plugin administration without
reading logs. The budget is `Plugins:Drain:TimeoutSeconds` (default 5), which also replaced the
hard-coded background-job stop budget. The original defect:

`PluginRegistry.Unregister` (`Services/Plugins/Runtime/PluginRegistry.cs:109-133`) removes the
plugin state and calls `AssemblyLoadContext.Unload()` at `:132` with **no drain of in-flight
calls**. Background jobs are the only contribution kind that has a stop path today.

The precedent to generalise already exists: the 2026-06-17 decision *"Cancellation and Drain
Sequence Before AssemblyLoadContext.Unload"* at
`ADRs/consolidated/plugin-platform.adr.md:1136`. Generalise it from background jobs to **all**
contributions, providers included. Providers are called on the request hot path, far more often
than jobs run, so the window this leaves open is much wider than the one the 2026-06-17 decision
closed.

## CORE-18 — Instantiate contributions through DI, not `Activator.CreateInstance`

**Status `delivered` · Priority P0 · Owner `core`.**

**Delivered 2026-09-05** — `9bd0197`. Contributions are constructed through a host-controlled scope
against a single canonical constructor, `T(IPluginHostContext)`; `Activator.CreateInstance` is gone
from that path. This was a deliberate **break**, not a compatible addition: `ContractVersion` moved
to `Current = MinimumSupported = 1.13.0`, and a new activation-time gate in `PluginLoader.LoadAsync`
makes the break surface as a legible version rejection. Without that gate the floor alone would not
have stopped an already-installed plugin from loading, and the failure would have been inconsistent
— a silent skip in some paths, a thrown exception at login in another. Plugin-side adoption is
filed per repository as CAR-02, FIN-02, SB-02, INV-02 and IAM-02 in [`plugins.md`](plugins.md). The
original defect:

`PluginRegistry.CreateMemberProvider` (`Services/Plugins/Runtime/PluginRegistry.cs:47-68`) resolves
the provider CLR type out of the plugin assembly and creates it with
`Activator.CreateInstance(providerClrType)` at `:66` — a public parameterless constructor and
nothing else. A provider therefore receives **no DI, no runtime bridge and no host context**.

Provider-to-provider consumption is impossible until instantiation passes at least the
`IPluginHostContext`, which makes this a hard prerequisite of CORE-13 rather than a tidy-up.
Acceptance: contributions are constructed through a scope the host controls, the host context is
injected, and construction failures are reported against the owning module id.

## CORE-19 — Manifest: category allowlist plus service/functional kind

**Status `partial` · Priority P1 · Owner `core`.**

`PluginManifest.Category` already exists and is already **init-only**
(`Contracts/Plugin/PluginManifest.cs:26`, defaulting to `"General"`). Adding an allowlist and a
service/functional kind therefore constrains values that already round-trip through install and
persistence. **No contract bump is needed** — `Contracts/Plugin/ContractVersion.cs:5` stays at
`1.11.0` and `:10` keeps `MinimumSupported` at `1.8.0`.

Open: the set of permitted categories is not validated, and the manifest cannot yet say whether a
plugin is a service/provider plugin or a functional plugin (R5). CORE-13 needs that distinction to
decide which modules may claim capability keys.

**Flag before implementing:** `ZipInstall_PersistsCategory_FromManifest_OnFailure`
(`tests/ClubGear.ArchitectureTests/PluginInstallerServiceTests.cs:206`) deliberately installs a
package with `category: "vehicle-data"` at `:218` and asserts the value is persisted even when the
install fails. `"vehicle-data"` is not a plausible allowlist member, so **introducing an allowlist
breaks this test**. Decide deliberately whether the allowlist admits it or the test moves to an
allowlisted category; do not discover it during the build.

## CORE-22 — Documentation integrity sweep

**Status `delivered` · Priority P0 · Owner `core`.**

Items 1, 2 and 3 were corrected on 2026-09-02; items 4 and 5 are corrected now. Item 6 is not an
open action in its own right — it only records *why* the drift happened, and that record stays; the
mechanism that prevents a recurrence is filed separately as **CORE-24**.

P0 because it corrects a false record. Six items, each verified at HEAD:

1. **The phantom notification-channel ADR block —
   `ADRs/consolidated/plugin-platform.adr.md:923-1014`.** Under the heading
   `## Source: Notification Channel Provider` at `:923`, eight decisions dated 2026-06-16 describe
   `INotificationChannelProvider`, a `PluginNotificationChannelContribution` as the tenth
   positional parameter of `RegisteredPluginRuntime`, a `notification.channel` extension point, a
   core-first / plugin-fallback precedence rule and a `ContractVersion` bump to `1.5.0`.
   **None of it exists.** A repository-wide search for those three type and key names matches only
   that block. `Contracts/Plugin/` holds no notification-channel contract;
   `Contracts/Plugin/PluginExtensionPoints.cs:5-15` declares exactly eleven constants and
   `notification.channel` is not among them; `RegisteredPluginRuntime`
   (`Services/Abstractions/IPluginRegistryReader.cs:17`) has twelve positional parameters and none
   of them is a channel contribution. Corroboration that the block is spurious rather than merely
   unimplemented: the 2026-06-11 member-extension-data section of the same file *also* claims the
   bump to `1.5.0`, so the two cannot both be true.
   **Correct it by appending a dated entry, never by rewriting history** — the ADR convention is
   append-only, and a rewritten record is a second false record. CORE-13 is the real decision that
   supersedes it.
   **Done 2026-09-01** — corrected by appending. The false block at `:923-1014` is left
   byte-identical, so how it arose stays readable.
2. **`docs/architecture/proposal-2026-08/DECISIONS.md:138-140`** states that a plugin "never
   touches the message tables, it calls `IMessageService`". A plugin cannot call `IMessageService`:
   the interface is not exposed on `Contracts/Plugin/IPluginHostContext.cs` and appears nowhere in
   `Contracts/Plugin/`. Either expose it or correct the sentence; today it reads as a shipped
   capability.
   **Done 2026-09-02** — the sentence now reads "a host-provided facade", with a dated
   correction note naming CORE-13 as the facade that does not exist yet.
3. **`ADRs/README.md:9`** says the former lowercase ADR directory's entries now live in
   `consolidated/`, but `adr/` still holds three files — `club-accounts-bookings.adr.md`,
   `finance-plugin-packaging.adr.md` and `payment-capture.adr.md`. Either finish the migration or
   correct the claim.
   **Done 2026-09-02** — migration finished: all three appended to
   `ADRs/consolidated/finance-plugin.adr.md` under `## Source:` headings, all 17 dated entries
   counted across, `adr/` deleted, README Finance row updated.
4. **`plugins/aktivierung.md`** recorded activation payloads whose versions disagreed with the
   shipped manifests: `:57` and `:61` used to document Finance `1.8.0` while
   `plugins/Finance/plugin.json` declares `1.9.0`. The file is now rewritten as a values-free
   procedure (`plugins/aktivierung.md:48,50` for the Finance entry) that derives values from
   `plugin.json` and `dist/` instead of publishing them.
   **Done 2026-10-02** (`294013e`). The June 2026 list also asked to restructure the authoring and
   activation guides by plugin category; that restructure is not required to fix this defect and is
   dropped from this item's scope. It is not lost — it is now canonical as CORE-27 and follows
   CORE-19/CORE-24 in [`next-runs.md`](next-runs.md).
5. **The canonical-constructor example in `docs/plugin-contract.md:89-92` did not match what
   actually worked.** It showed the injected `IPluginHostContext` stored in a `private readonly`
   field. A field that is assigned and never read builds clean under a plain `dotnet build` but is
   escalated to an error by the `/warnaserror` every plugin pipeline uses, so a plugin author
   following the documented example could produce a plugin that compiles locally and fails in CI.
   The shape actually adopted during the CarInfo adoption — and that matches the core's own 1.13.0
   fixtures at
   `tests/ClubGear.ArchitectureTests/Fixtures/AllowedPluginRuntimeFixtures.cs:143-148` — is a public
   auto-property, `public IPluginHostContext HostContext { get; }`.
   **Found 2026-09-09 during CAR-02; done 2026-10-02** (`30fd068`) — the example now shows the
   property shape.
6. **Item 4's version drift has no guard, and neither does anyone else's.** See **CORE-24** — the
   defect is recorded here because it is how the drift is *found*, but fixing it is a mechanism and
   therefore its own item.

## CORE-24 — Manifest consistency: one version, checked

**Status `open` · Priority P1 · Owner `core`.**

A plugin declares its version and its contract floor **twice**, and nothing compares the two:

- `plugin.json` — read at install time by `Services/Plugins/Manifest/PluginManifestParser.cs`.
- the compiled in-code `PluginManifest` on the module type — read at activation time by
  `Services/Plugins/Runtime/PluginLoader.cs`.

The two gates read different declarations, so the failure modes are asymmetric. A stale contract
floor in `plugin.json` fails **closed**: the install is rejected and the author finds out. A stale
floor or version in the in-code manifest fails **open**: the plugin activates and keeps its old
claim, and the plugin pipelines' tag check only ever reads `plugin.json`. A release can therefore
ship with the two disagreeing and nothing anywhere notices.

This was verified, not theorised: at the start of CAR-02 CarInfo declared `">=1.8.0"` in
`plugin.json` and `">=1.0.0"` in its in-code manifest, and had shipped that way. The adoption held
the two together by hand, and by procedure in the plan — which is exactly the kind of guarantee that
survives until the first person who has not read the plan.

Acceptance: a host-side check that a loaded module's compiled manifest agrees with the `plugin.json`
it shipped beside, on both version and contract floor, and reports the disagreement against the
owning module id rather than silently preferring one. Filed 2026-09-09 from CAR-02.

## CORE-25 — Secure first-run installation assistant

**Status `open` · Priority P1 · Owner `core`.**

A fresh deployment currently has no `/install` bootstrap path. Deliver one transactional,
idempotent flow that detects an unconfigured instance, collects neutral operator and first-admin
settings, creates the first Identity administrator through supported services, records completion,
and permanently closes the bootstrap route. Pre-install routing must preserve static assets,
health/error handling and required authentication endpoints without redirect loops.

The old twelve-slice source is retained only for acceptance detail. Before implementation, decide
post-install route behaviour, pre-install registration policy, concurrency handling, whether demo
data is development-only, and the auditable completion record. Security review is mandatory because
this creates the first privileged identity and writes secrets. Acceptance includes fresh-database,
parallel-submit, restart, post-completion denial and containerised end-to-end tests.

## CORE-27 — Plugin authoring guide and project template

**Status `open` · Priority P2 · Owner `core`.**

Merge CG-BL-050 and CG-BL-051 into one developer-experience item: organise the authoring and
activation guidance by the final service/functional manifest vocabulary, then provide a minimal
project template that obeys the public contract, canonical constructor, package/signing workflow,
submodule boundary and architecture gates. Start after CORE-19 settles the vocabulary; do not let
cosmetic guide work block CORE-19 or CORE-24.

## CORE-23 — Repository hygiene: submodule wiring

**Status `delivered` · Priority P1 · Owner `core`.**

**Delivered 2026-10-02.** Both defects below were local git state, not tracked content, so they are
repaired on this machine rather than by a commit; what *is* committed is the guard that makes the
same drift fail the build on any machine. `PluginSubmodules_ShouldNotBe_EmbeddedRepositories` and
`PluginSubmoduleUrls_ShouldMatch_GitmodulesDeclaration`
(`tests/ClubGear.ArchitectureTests/PluginBoundaryTests.cs`) run both checks in C#; a mirrored shell
block in the `.gitlab-ci.yml` `submodules` job runs the same two checks under `sh`/`dash`/`bash`
without shelling out to `git`, reading the same URL-normalisation rule so the two cannot drift
apart. Both facts are confirmed green against this repository, which independently proves the local
repair held: every `plugins/<X>/.git` is a gitlink file and every effective URL normalises equal to
its `.gitmodules` declaration.

1. **The Finance submodule URL was pinned to a local path.** `.gitmodules:15` declares
   `git@git.maxbrenne.de:clubgear/plugin-finance.git`; the local `.git/config` had
   `submodule.plugins/Finance.url` set to `/Users/max/Code/ClubGear/plugins/Finance`. Repaired with
   `git submodule sync -- plugins/Finance`; the config now reads the declared URL.
2. **`plugins/IamOidc` was an old-style embedded `.git` directory.** Converted to the standard
   gitlink form (`git submodule deinit` + removing the embedded `.git` + `git submodule update
   --init`), with the git-ignored signing key and package artefacts backed up outside the
   repository first and restored byte-identical afterwards, because `deinit` clears the work tree.
   The repair was a full success with no halt: all six pre-flight gates were green before touching
   anything, both repairs completed, and the superproject index was never rewritten —
   `plugins/IamOidc` and `plugins/Finance` stayed at their existing gitlink SHAs throughout. The
   full log, including two deviations handled mid-repair, is recorded under "Slice 1" in
   `.crispy/07_implementation.log`.

**ServiceBook is not included — its gitlink is tracked and clean.** The `?` seen in an earlier
`git status` was the submodule-status flag for untracked content *inside* the submodule, not a
missing gitlink, and there is nothing to stage.

**`Readme.md` carried the same defect class — a false claim about the repository.** It said "All
except `Finance` are tracked as git submodules... `Finance` lives directly in this repository",
which `.gitmodules:13-15` and the `plugins/Finance/.git` gitlink already contradicted. Corrected by
appending a dated note rather than rewriting the original sentence away, matching CORE-22's
append-only convention (`Readme.md:73-80`, `30a2b90`).

## CORE-28 — Architecture tests coupled to a plugin's current checkout, not its contract floor

**Status `open` · Priority P1 · Owner `core`.**

`tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj` carries a `ProjectReference`
straight to `plugins/CarInfo`, `plugins/Finance`, `plugins/Inventar` and `plugins/ServiceBook`.
This makes core's own test *compilation* depend on whatever commit each submodule happens to be
checked out to on the machine running the build — not on the contract version core actually
declares support for (`ContractVersion.MinimumSupported`), and not on the gitlink commit core's own
last commit records.

**Found 2026-10-03** while verifying the CORE-28 PageKey-routing fix: `dotnet build` of the test
project (not `--no-build`, which silently reuses whatever `.dll` was last compiled) produced 92
`CS7036`/`CS0618` errors, all in plugin-specific test files that construct `CarInfoActionProvider`,
`CarInfoEditTabProvider`, `CarInfoAdminPanelProvider`, `FinanceActionProvider`,
`FinanceKassenwartPageProvider`, `FinanceEditTabProvider` with the pre-1.13.0 parameterless
constructor, and one reference to `FinancePermissions.KassenwartAccess` after it was marked
`[Obsolete]`. None of this is a regression in core: at the gitlink commits core's own last commit
actually records (`plugins/Finance` `1b8f403`, confirmed via `git show 1b8f403:FinanceProviders.cgcs`),
these same provider types still have the old parameterless shape the tests expect. The break is
purely that the local working trees of `plugins/CarInfo` and `plugins/Finance` had already moved
past CORE-18's canonical-constructor adoption (CAR-02, FIN-02) and FIN-02's permission rename,
independently and without a corresponding core commit bumping the gitlink.

Two separable problems:

1. **No regression test can currently prove this**, because `dotnet test --no-build` (the pattern
   this repository's own verification instructions use) reuses a stale compiled test assembly and
   reports the *previous* successful compile's results, not whether the current source tree
   compiles. `899/901 passing` was reported against such a stale `.dll` on 2026-10-03.
2. **The architectural question CAR-02/SB-02/INV-02/IAM-02/FIN-02 keep re-raising**: should core's
   test project assert plugin-specific behaviour by constructing plugin-internal types directly
   against whatever the submodule happens to have checked out, or should that verification live in
   each plugin's own test project (as `plugins/Finance/Finance.Tests/` already does for its newer
   work) and core only assert the generic contract/isolation surface? The current mixed state means
   a plugin-side-only change, never touched from core, can make `tests/ClubGear.ArchitectureTests`
   stop compiling.

Not blocking any shipped behaviour today — the main `ClubGear.csproj` build stays green throughout
because it does not reference plugin source. Blocking: any `dotnet test` run against current
source, and therefore the `Stop` hook's and `CLAUDE.md`'s own verification gate, the moment someone
actually rebuilds instead of trusting a cached `.dll`.
