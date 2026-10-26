# Phase P — Plan

CAR-02: adopt plugin contract `1.13.0` in CarInfo, release as `1.0.10`.

**Working repository:** `/Users/max/Code/ClubGear/plugins/CarInfo` — a standalone git repository
that sits inside the ClubGear core checkout as a submodule. Every path below is absolute or relative
to this repository. **The core checkout at `/Users/max/Code/ClubGear` is read-only for this run.**

**Working directory:** `/Users/max/Code/ClubGear/plugins/CarInfo` — the repository root itself.
**File root:** the same. **No execute prefix.**

**No worktree is used, and this was decided rather than skipped.** `CarInfo.Plugin.csproj:8`
resolves the contracts project by a path relative to the project file:

```xml
<ClubGearContractsProject Condition="'$(ClubGearContractsProject)' == ''">../../Contracts/Plugin/ClubGear.Plugin.Contracts.csproj</ClubGearContractsProject>
```

From the repository root that resolves into the surrounding core checkout, where
`ContractVersion.Current == MinimumSupported == 1.13.0`. From a worktree at `.crispy-worktree/` the
same relative path would resolve to `/Users/max/Code/ClubGear/plugins/Contracts/Plugin/...`, which
does not exist — restore would fail and the run's **only** automated gate would be unavailable,
recoverable only via a `-p:ClubGearContractsProject=` override that design §6.1 explicitly says is
unnecessary and that this plan forbids. A worktree is therefore not merely declined here, it is
unworkable while that relative path stands. A later reader who re-opens this question should
re-open the csproj line first. Implementation happens directly on `main`; no
`crispy/implementation` branch is created.

## Primary command path

Copied verbatim from `04_design.md` §9. Use these exact strings; do not substitute a plain
`dotnet build`, do not drop `/warnaserror`, do not add `-p:ClubGearContractsProject=`.

- **Setup:** `git status`
- **Install:** `dotnet restore /Users/max/Code/ClubGear/plugins/CarInfo/CarInfo.Plugin.csproj`
- **Test:** `dotnet build /Users/max/Code/ClubGear/plugins/CarInfo/CarInfo.Plugin.csproj -c Release /warnaserror`
- **Run:** `dotnet build /Users/max/Code/ClubGear/plugins/CarInfo/CarInfo.Plugin.csproj -c Release /warnaserror`

There is **no test project in this repository and none is being created** (design §4). The Release
build under `/warnaserror` is the only automated gate, and it is both the Test and the Run command:
a plugin has no standalone run mode, and this run produces no package. `/warnaserror` is mandatory
because CI escalates warnings from the command line (`.gitlab-ci.yml:51`, `:72`) while
`CarInfo.Plugin.csproj` sets no `TreatWarningsAsErrors` — a plain `dotnet build` is a *weaker* gate
than CI and can pass where CI fails.

## Structural exceptions carried forward from design and structure

No layer-only slice was kept — all three slices below are capability-bounded. Two deliberate
overrides of generic CRISPY expectations are carried forward from `04_design.md`, and the Builder
must honour both rather than "fix" them:

- **No ADR tree.** This repository has no `adr/`, `ADRs/` or `docs/` convention and none is invented
  (design §5). The decision record for CAR-02 lands in `CHANGELOG.md` and `README.md`, which is what
  slice 3 is.
- **No test project.** Verification is the Release build plus six named manual checks (design §6.3),
  distributed below to the slice that creates the risk each one closes.

## Global rules for every slice

- **Staging is explicit, always.** `git add -A`, `git add .` and `git commit -a` are **forbidden**.
  Stage the named paths only, then confirm with `git diff --cached --name-only` before committing.
- **Before every commit**, run `git ls-files dist .signing .package` and read `git status`. This is
  an **open check, not an assumption**: `dist/` contains files that were tracked before `.gitignore`
  covered them (confirmed: `dist/CarInfo-1.0.2.zip`, `*.signature.bin`, `dist/signer-public.pem` and
  others are tracked). None of them may enter any commit of this run.
- **`.crispy/` is untracked here and stays that way.** Unlike the core checkout, this repository's
  `.gitignore` has **no `.crispy` entry** — it lists only `bin/`, `obj/`, `dist/*.zip`, `dist/*.bin`,
  `dist/signer-private.pem`, `.package/`, `.signing/`, `*.db`, `*.sqlite`. So `.crispy/` will appear
  as untracked in `git status` on every single slice. It is CRISPY scratch: **never stage it, never
  commit it, and do not add it to `.gitignore`** — adding the ignore rule is itself an unrequested
  tracked-file change.
- **One commit per slice, made by the Builder.** The user has authorised the Builder to commit at the
  end of each slice, once that slice's checks pass — three commits in total. **No `git tag` and no
  `git push`.** Do not squash the three, and do not defer them all to the end: each slice's commit is
  part of its Definition of Done.
- **Commit message convention — follow this repository, not the core.** `git log` here shows short
  imperative subjects with an optional scope prefix (`ci: use fresh clone for gitleaks`,
  `security: rotate exposed package signing key`) and, for the one release commit, a version-led
  subject with a German body (`1.0.9: requiredCoreVersion auf >=1.8.0 anheben, ...`). Recent commits
  carry a `Co-Authored-By: Claude Opus <n> <noreply@anthropic.com>` trailer and **no `Claude-Session`
  trailer** — do not import the core session's trailer block into this repository. Match what
  `git log -3 --format=%B` actually shows at the time of writing.

---

## Slice 1 — Every host-constructed contribution satisfies the canonical `(IPluginHostContext)` seam

Contract-compliant source, landed *before* anything declares that compliance. After this slice the
plugin still declares `>=1.0.0` and is still rejected at activation — nothing about its runtime
disposition has changed, which is why this slice is safe to land alone.

- [ ] **1.1 — Add the canonical constructor to the three load-gated providers.**
  In `CarInfoProviders.cgcs`, immediately after the existing `private readonly CarInfoDataService
  _dataService = new();` line of `CarInfoDetailCardProvider` (class at `:8`), `CarInfoEditTabProvider`
  (`:32`) and `CarInfoActionProvider` (`:283`), insert exactly:

  ```csharp
  // CORE-18 / contract 1.13.0: the canonical constructor. The host builds every
  // contribution as T(IPluginHostContext); there is no parameterless one any more.
  public IPluginHostContext HostContext { get; }

  public CarInfoDetailCardProvider(IPluginHostContext hostContext) => HostContext = hostContext;
  ```

  with the constructor name matching each class. **Change no method body** — every existing member
  keeps reading its own `hostContext` parameter (design §1.2). The **auto-property is load-bearing**:
  a hand-written `private readonly` field assigned and never read is the one shape that builds clean
  without `/warnaserror` and fails CI (design §7). Do not use it, even though
  `docs/plugin-contract.md:89-92` in the core shows it — that divergence is deliberate.
  **DoD:** the three classes each declare one auto-property and one one-parameter constructor; no
  diff hunk in this task touches any line inside an existing method body.

- [ ] **1.2 — Add the same constructor to the two providers the loader never checks.**
  Same insertion in `CarInfoMemberCarsPageProvider` (`:524`) and `CarInfoAdminPanelProvider` (`:769`)
  of `CarInfoProviders.cgcs`. These two are **not** load-gated: `PluginLoader`'s collector never
  yields them, so a tree without their constructors loads, reports healthy, and quietly loses the
  admin panel and the member-cars page via `Rent<T>` returning an empty lease (design §2.1). Do not
  "optimise" these two away on the grounds that the loader does not verify them.
  **DoD:** all five providers now carry the constructor; the file has been edited in one coherent
  pass, so no intermediate state with only three converted types is ever committed.

- [ ] **1.3 — Build under the mandatory escalation.**
  Run **Install:** `dotnet restore /Users/max/Code/ClubGear/plugins/CarInfo/CarInfo.Plugin.csproj`
  then **Test/Run:**
  `dotnet build /Users/max/Code/ClubGear/plugins/CarInfo/CarInfo.Plugin.csproj -c Release /warnaserror`
  This slice is the *only* reason `/warnaserror` is mandatory — it is what proves the chosen
  constructor shape emits no diagnostic CI would escalate.
  **DoD:** the command exits 0 with zero warnings. If it reports CS0414 or any "assigned but never
  used" diagnostic, the wrong constructor shape was used — fix the shape, do not suppress the
  warning.

- [ ] **1.4 — Manual checks 3, 4 and 5 (this slice's risk origin).**
  - **Check 3 — token survival.** `grep -n 'data-carinfo-vehicles\|SerializeVehiclesForHtmlAttribute'
    CarInfoProviders.cgcs`. Both tokens must still be present verbatim. A core test
    (`tests/ClubGear.ArchitectureTests/ServiceBookPluginTests.cs:133`) reads this file **as text**,
    and this run may not edit the core to relax it (design §2.5).
  - **Check 4 — exactly five constructors.** `grep -n 'public CarInfo' CarInfoProviders.cgcs`. Expect
    exactly five constructor hits, one per provider, each with exactly one `IPluginHostContext`
    parameter, and **no second public constructor on any of the five** — `SingleOrDefault` in
    `PluginContributionActivator` treats two as a failure (design §2.2).
  - **Check 5 — migrations still parameterless.** Confirm `CarInfoSchemaMigration`
    (`CarInfoSchemaMigration.cgcs:5`) and `CarInfoFieldLifecycleMigration` (`:80`) declare **no
    constructor at all**. The plugin builds them itself with `new` in `GetMigrations()`; a canonical
    constructor there breaks those call sites (design §2.3).
  **DoD:** all three checks pass and are reported in the implementation log with their actual output,
  not merely asserted.

- [ ] **1.5 — Commit `CarInfoProviders.cgcs` alone.**
  Run the commit gate first: `git ls-files dist .signing .package` and a **read** `git status`.
  Then stage the single path explicitly: `git add CarInfoProviders.cgcs`, verify with
  `git status --short` and `git diff --cached --name-only`, and commit.
  **DoD:** `git show --stat HEAD` lists `CarInfoProviders.cgcs` and nothing else — no `dist/`, no
  `.crispy/`, no `.package/`, no `.signing/`.

**Slice 1 done when** the Release build passes under `/warnaserror`, checks 3/4/5 hold, and one
commit touching only `CarInfoProviders.cgcs` exists.

**Not in this slice:** no version literal, no contract floor, no prose.

---

## Slice 2 — The plugin declares itself as 1.0.10 against contract 1.13.0 at both gates

**Depends on slice 1.** Raising the floor before the constructors exist would commit a tree that
claims 1.13.0 compliance its source does not honour — installable on a 1.13.0 host and then rejected
at activation, the exact defect 1.0.9 half-fixed.

- [ ] **2.1 — Apply all four literals as one atomic edit.**
  Nothing in this repository or in CI compares `plugin.json`'s version to the compiled
  `new Version(...)` (design §2.4), so splitting these by file would commit exactly the drift the
  design warns about.

  | File:line | From | To |
  |---|---|---|
  | `plugin.json:4` | `"version": "1.0.9"` | `"version": "1.0.10"` |
  | `plugin.json:5` | `"requiredCoreVersion": ">=1.8.0"` | `"requiredCoreVersion": ">=1.13.0"` |
  | `CarInfoPluginModule.cgcs:10` | `Version: new Version(1, 0, 9)` | `Version: new Version(1, 0, 10)` |
  | `CarInfoPluginModule.cgcs:14` | `RequiredCoreVersion: ">=1.0.0"` | `RequiredCoreVersion: ">=1.13.0"` |

  `CarInfoPluginModule` is touched for these two literals **and nothing else** — it keeps its
  implicit parameterless constructor, which `Activator.CreateInstance(entryPointType)` requires
  (design §2.3).
  **DoD:** the two files show exactly four changed lines between them, in one editing pass.

- [ ] **2.2 — Build.**
  `dotnet build /Users/max/Code/ClubGear/plugins/CarInfo/CarInfo.Plugin.csproj -c Release /warnaserror`
  Know its limits (design §6.2): it proves `new Version(1, 0, 10)` compiles and that nothing else in
  the file broke. It does **not** read `plugin.json` at all — that file is a `<None>` item — and
  **nothing validates a contract floor at build time**.
  **DoD:** exit 0, zero warnings.

- [ ] **2.3 — Manual check 1: re-read `plugin.json` by eye.**
  Confirm `"version": "1.0.10"` and `"requiredCoreVersion": ">=1.13.0"`, spelled exactly — no `^`,
  no `~`, no `v` prefix, no whitespace inside the range. `PluginManifestParser` **fails closed**
  here, so this is the only one of the two floor sites that will ever tell you it is wrong.
  **DoD:** both strings confirmed character by character.

- [ ] **2.4 — Read `CarInfoPluginModule.cgcs:14` by eye. No grep substitutes for this.**
  This side **fails open**: `PluginManifest.ParseMinimumRequiredVersion` returns
  `ContractVersion.Current` for an unparseable string, so a typo here silently passes activation and
  nothing — not the build, not CI, not the host — ever reports it (design §2.4). Also confirm in the
  same read that `CarInfoPluginModule` still declares **no constructor**.
  **DoD:** the line reads `RequiredCoreVersion: ">=1.13.0",` verbatim, confirmed by looking at it;
  and the class body contains no constructor declaration.

- [ ] **2.5 — Manual check 2: back-reference grep over the tracked tree.**
  `git grep -n '1\.0\.9\|1, 0, 9\|>=1\.8\.0\|>=1\.0\.0'`
  Expected remaining tracked hits: **`CHANGELOG.md` only** — historic prose and the historic
  `## 1.0.9` heading, all of which stay. Any hit in `plugin.json` or `CarInfoPluginModule.cgcs` is a
  missed literal. Hits under `.package/` or `dist/` are ignored or untracked leftovers claiming
  `1.0.9`, and are **correct to leave alone**: no 1.0.10 package exists until the user cuts one.
  **DoD:** the grep output contains no source or manifest hit.

- [ ] **2.6 — Commit the two manifest files alone.**
  Commit gate again: `git ls-files dist .signing .package` and a reviewed `git status`. Then
  `git add plugin.json CarInfoPluginModule.cgcs` and verify with `git diff --cached --name-only`.
  **DoD:** `git show --stat HEAD` lists exactly `plugin.json` and `CarInfoPluginModule.cgcs`.

**Slice 2 done when** the four literals agree across both files, the Release build passes, the
back-reference grep returns `CHANGELOG.md` hits only, and one commit exists.

**Not in this slice:** no CHANGELOG, no README.

---

## Slice 3 — A reader of this repository can find out what 1.0.10 requires and what it breaks

**Depends on slice 2.** This is the design's one hard ordering constraint (§5): a `## 1.0.10`
CHANGELOG heading may only land on a tree whose source already declares `1.0.10`.

- [ ] **3.1 — Insert the CHANGELOG section, copied from the design.**
  In `CHANGELOG.md`, insert the `## 1.0.10 — Contract 1.13.0 uebernommen` section **drafted verbatim
  in `04_design.md` §5.1**, positioned **between `## Unreleased` and `## 1.0.9`**. Copy that text;
  do not re-invent the prose. `## Unreleased` and its three entries (`Changed`, `Added`, `Security`)
  stay **exactly as they are** — its `Security` entry is *about* 1.0.9 and folding it forward would
  misdate it.
  **DoD:** the new section sits between the two existing headings; `git diff CHANGELOG.md` shows
  pure insertion — zero deleted lines and zero modified lines above the insertion point.

- [ ] **3.2 — Apply the per-file umlaut convention.**
  `CHANGELOG.md` gets **real umlauts** (`Kompatibilitäts-`, `ausschließlich`, `übernommen`,
  `fünf`, `unverändert`) to match the neighbouring `## 1.0.9` section, which uses them. Design §5.1
  presents the text transliterated; converting it for this file is the one intentional deviation
  from "verbatim" and is called out in the design itself. Keep the heading's own umlaut consistent
  with that choice. Also confirm the German free-prose form with **no Keep-a-Changelog subheadings**,
  matching the sole released-section precedent.
  **DoD:** no `ae`/`oe`/`ue`/`ss` transliteration remains inside the new `CHANGELOG.md` section where
  a real umlaut belongs, and the section carries no `### Added`-style subheadings.

- [ ] **3.3 — Insert the README section, copied from the design.**
  In `README.md`, insert the `## Kompatibilitaet` section **drafted verbatim in `04_design.md` §5.2**,
  **after `## Zielbild`** (heading at `README.md:5`) and before `## Build` (`:12`). Here the
  **transliterated** form (`ae/oe/ue/ss`) is correct — it matches this file's existing convention
  (`## Verfuegbare Actions`). The existing `## Build`, `## Paketinhalt`, `## Verfuegbare Actions` and
  `## Hinweis zum aktuellen Host-UI` blocks stay as they are.
  **DoD:** `grep -n '^## ' README.md` shows `## Kompatibilitaet` between `## Zielbild` and
  `## Build`, with all four later headings still present and in order.

- [ ] **3.4 — Cross-check the prose against the tree it now describes.**
  Every claim in the two new sections must be true of the tree as of slice 2: the five constructor
  types named (`CarInfoDetailCardProvider`, `CarInfoEditTabProvider`, `CarInfoActionProvider`,
  `CarInfoAdminPanelProvider`, `CarInfoMemberCarsPageProvider`), the two floor sites named
  (`plugin.json` and the compiled manifest in `CarInfoPluginModule`), and the three deliberately
  parameterless types named (`CarInfoPluginModule`, `CarInfoSchemaMigration`,
  `CarInfoFieldLifecycleMigration`).
  **DoD:** each named identifier verified against the actual source. **No build is needed** — neither
  file is compiled and no tooling in this repository reads either; running the build here proves
  nothing about this slice.

- [ ] **3.5 — Manual check: `## Unreleased` is byte-identical.**
  `git diff CHANGELOG.md` and confirm the `## Unreleased` block appears in the diff only as
  unchanged context.
  **DoD:** zero `-` lines anywhere in the `CHANGELOG.md` diff.

- [ ] **3.6 — Commit the two prose files alone.**
  Commit gate a third time: `git ls-files dist .signing .package` and a reviewed `git status`. Then
  `git add CHANGELOG.md README.md` and verify with `git diff --cached --name-only`.
  **DoD:** `git show --stat HEAD` lists exactly `CHANGELOG.md` and `README.md`; `git status` shows a
  clean tree apart from the untracked `.crispy/` scratch directory.

**Slice 3 done when** both sections are in place, `## Unreleased` is untouched, and one commit
exists — at which point the run's deliverable is met: a buildable, committed working tree on `main`.

---

## Non-goals — restated so they are not drifted into

None of the following is part of this run, and doing any of them is a defect, not initiative:

- **No `git tag`.** Cutting the release is the user's act (design §8). In particular do not create
  `v1.0.10`.
- **No packaging, no ZIP, no `dist/` or `.package/` content.** The stale artifacts claiming `1.0.9`
  are correct to leave alone.
- **No signing.** `.signing/` is git-ignored and is never added. The signing-key decision (design
  §8.1) is a release-time question for the user, not a Builder task.
- **No `.gitlab-ci.yml` change.** The `test` job stays rule-gated off.
- **No ADR tree.** No `adr/`, no `ADRs/`, no `docs/` directory is created in this repository.
- **No test project.**
- **No edit to the ClubGear core checkout at `/Users/max/Code/ClubGear`** — of any kind. This
  explicitly includes `docs/plugin-contract.md`, whose canonical-constructor example diverges from
  the shape chosen here. That alignment is a **core-owned follow-up to be filed, not performed**
  (design §7, §8).

## Follow-ups to file, not to do

- Core-side: align `docs/plugin-contract.md:89-92`'s canonical-constructor example with the shape the
  core's own 1.13.0 fixtures use (`AllowedPluginRuntimeFixtures.cs:143`, `PluginProviderSeamTests.cs:198`).
- Core-side, optional: a check that a package's `plugin.json` version matches its compiled manifest
  version — the drift this run had to close procedurally (design §2.4).
