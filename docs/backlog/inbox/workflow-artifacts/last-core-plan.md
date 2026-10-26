# Phase P — Plan

Iteration: `record-corrections-and-submodule-hygiene`
Date: 2026-09-14
Source commit: `fbf4249`
Slices: 8 (from `.crispy/05_structure.md`)

## Primary command path

Copied verbatim from `.crispy/04_design.md` §5. Use these exact strings — no substitutions
(no `pip`, no `python` for `python3`, no shell activation, no solution-file build, no
`dotnet test` without the explicit test-project build first).

- **Setup:** `git submodule update --init --recursive`
- **Install:** `dotnet restore tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj`
- **Test:** `dotnet test tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj -c Release --no-build`
- **Run:** `dotnet run --project ClubGear.csproj`

**Three-command verification gate** (mandatory for every C#-touching slice; there is no solution
file, so the test project must be built explicitly):

```
dotnet build -c Release --no-restore /warnaserror
dotnet build tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj -c Release --no-restore /warnaserror
dotnet test tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj -c Release --no-build
```

## Working directory

- Working directory: repository root `/Users/max/Code/ClubGear`
- File root: `/Users/max/Code/ClubGear`
- Execute prefix: none (`cd /Users/max/Code/ClubGear &&` where the shell resets cwd)
- Branch: `main` (the existing branch — **no worktree, no new branch**)

**Decided: no worktree.** Work happens directly in the current working tree on `main`. There is no
`.crispy-worktree/` and no `crispy/implementation` branch, and no slice creates one. This is the
right choice for this run because the tree is already dirty with work slices 5, 6 and 8 build on,
and because slice 1 mutates `.git/config`, `.git/modules/` and `plugins/IamOidc` working-tree
metadata of **this** checkout, which a linked worktree could not do.

## Starting state: the tree is already dirty, and that is intentional

Do **not** stash, reset, clean or commit-to-clear before starting. The following pre-existing
uncommitted work is an input to this run and must survive it:

| Path | State at start | Who touches it |
|---|---|---|
| `docs/backlog/core-platform.md` | modified | slice 5 (cite targets only), slice 8 step 1 |
| `docs/backlog/plugins.md` | modified | slice 5 (FIN-01 cites), slice 6 (CAR-02 note), slice 8 step 3 |
| `docs/backlog/domains.md` | modified | slice 8 step 3 |
| `docs/backlog/README.md` | modified | slice 8 step 3 (confirm CG-BL-050 only) |
| `docs/backlog/next-runs.md` | **untracked** | slice 8 step 2 **only** |
| `CHANGELOG.md` | modified | slice 6 |
| `CLAUDE.md` | modified | **nobody — out of scope** |
| `plugins/CarInfo` (submodule pointer) | modified (`M plugins/CarInfo`) | **nobody — out of scope** |

Consequence for every DoD that reads a diff: **`git diff <path>` against HEAD also shows the
pre-existing edits** for the six files above. Before editing any of them, snapshot the file
(`cp <path> <scratchpad>/pre/<name>`) and verify your own change with `diff <scratchpad>/pre/<name>
<path>`. "No other hunk changed" always means *relative to the pre-slice snapshot*, never relative
to `fbf4249`, except where a DoD explicitly says `git show fbf4249:…` (those are byte-identity
checks on files this run must not rewrite: `ADRs/consolidated/finance-plugin.adr.md:740,747` and
`ADRs/README.md:9`).

## Staging and commit discipline

- **`git add -A`, `git add .`, `git commit -a` and `git commit --all` must not be used anywhere in
  this run.** Any of them would sweep the out-of-scope `CLAUDE.md` edit and the pre-existing
  `plugins/CarInfo` pointer change into a commit.
- Stage by **explicit path only**, and only paths that slice actually touched. Verify with
  `git status --porcelain` and `git diff --cached --name-only` before every commit.
- **`plugins/CarInfo` is never staged.** Its pointer change predates this run and is outside this
  run's scope; `M plugins/CarInfo` must still be an *unstaged* modification when the run ends.
  Likewise `CLAUDE.md` is never staged.
- **Slice 1 is never committed at all** — it is local git state (`.git/config`, `.git/modules/`,
  `plugins/IamOidc/.git`), and none of that is stageable content anyway. Nothing in slice 1 produces
  a `git add`.
- Per-slice staging sets, exhaustive:
  - Slice 1 — *nothing*. No commit.
  - Slice 2 — `tests/ClubGear.ArchitectureTests/Fixtures/submodule-url-normalization.cases.tsv`,
    `tests/ClubGear.ArchitectureTests/PluginBoundaryTests.cs`, and
    `tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj` **only if 2.2 edited it**.
  - Slice 3 — `.gitlab-ci.yml`, plus the fixture TSV again only if 3.4 amended it.
  - Slice 4 — `Readme.md`.
  - Slice 5 — `plugins/aktivierung.md`, `ADRs/consolidated/finance-plugin.adr.md`,
    `docs/backlog/plugins.md`, `docs/backlog/core-platform.md`.
  - Slice 6 — `docs/plugin-contract.md`, `CHANGELOG.md`, `docs/backlog/plugins.md`.
  - Slice 7 — `ADRs/0003-plugin-submodule-hygiene.md`, `ADRs/README.md`, `.gitignore`.
    **Not** `adr/record-corrections-and-submodule-hygiene.adr.md` — it is git-ignored by design.
  - Slice 8 — `docs/backlog/core-platform.md`, then `docs/backlog/next-runs.md` (which needs an
    explicit `git add docs/backlog/next-runs.md` because it is currently untracked), then
    `docs/backlog/plugins.md`, `docs/backlog/domains.md`, `docs/backlog/README.md`.
- Slices 5, 6 and 8 all stage `docs/backlog/plugins.md`, and slices 5 and 8 both stage
  `docs/backlog/core-platform.md`. That is expected under the scoping rule — each slice contributes
  a different hunk to the same file. Before each of those commits, re-read the staged hunks
  (`git diff --cached docs/backlog/plugins.md`) and confirm they contain only this slice's edit and
  the pre-existing edits already carried in earlier commits of this run.

## Structural notes carried into this plan

- No layer exception was kept in Step 1. Every slice is a bounded capability or an end-to-end
  record correction. The one bundled artifact — the shared fixture TSV — lands **with slice 2**,
  its first (authoritative) consumer, because a TSV with no reader asserts nothing and landing it
  alone would be the shared-data bucket this phase forbids (`05_structure.md:105-108`).
- Packaging layout is preserved completely. The only new **repository** file is
  `tests/ClubGear.ArchitectureTests/Fixtures/submodule-url-normalization.cases.tsv`. Other new
  files are `ADRs/0003-plugin-submodule-hygiene.md` (committed ADR) and
  `adr/record-corrections-and-submodule-hygiene.adr.md` (git-ignored process record).
- Slice 1 produces **no commit**. It is local git state only.
- `docs/backlog/next-runs.md` is touched by **slice 8 only**, step 2, after
  `docs/backlog/core-platform.md` — the governance rule at `next-runs.md:207-209`.
- Scoping rule: each doc slice repairs only the references **its own** edit invalidated. All item
  statuses, counts and run rows belong to slice 8.
- Pre-existing dirty tree to preserve: modified `CLAUDE.md`, `CHANGELOG.md`, `docs/backlog/*`,
  the `plugins/CarInfo` submodule pointer, untracked `docs/backlog/next-runs.md`. `M plugins/CarInfo`
  predates this run and must not be touched.
- Backup location for slice 1 (outside the working tree):
  `/private/tmp/claude-501/-Users-max-Code-ClubGear/97dc48b1-f16f-47c2-b3a1-0c3423754294/scratchpad/iamoidc-git-backup/`

---

## Slice 0 — Pre-flight snapshot (not a slice; one command, no repository change)

- [ ] **0.1 — Snapshot the seven already-dirty paths so later DoDs have a baseline.**
  - `mkdir -p <scratchpad>/pre` then copy `docs/backlog/core-platform.md`,
    `docs/backlog/plugins.md`, `docs/backlog/domains.md`, `docs/backlog/README.md`,
    `docs/backlog/next-runs.md`, `CHANGELOG.md`, `CLAUDE.md` into it, and record
    `git status --porcelain > <scratchpad>/pre/status.txt`.
  - **DoD:** all seven snapshots exist outside the repository; `status.txt` records the starting
    state including `M plugins/CarInfo`; `git status --porcelain` is byte-identical to `status.txt`
    (the snapshot changed nothing); no stash, reset, checkout or clean was run.

---

## Slice 1 — Gated local submodule repair (Flow B, uncommitted)

Destructive and gated. **All six gate commands run before any mutation.** A failed gate changes
nothing and halts *only this slice*; slices 2–8 proceed and slice 8 records the halt.
Nothing in this slice is committed.

- [ ] **1.1 — Run all six gate commands, mutate nothing.** In order, capturing every output:
  1. `git ls-remote git@git.maxbrenne.de:clubgear/plugin-finance.git`
  2. `git ls-remote git@git.maxbrenne.de:clubgear/plugin-iamoidc.git`
  3. `git -C plugins/Finance fetch origin` then `git -C plugins/Finance log origin/main..HEAD --oneline`
  4. `git -C plugins/IamOidc fetch origin` then `git -C plugins/IamOidc log origin/main..HEAD --oneline`
  5. `git -C plugins/Finance status --porcelain`
  6. `git -C plugins/IamOidc status --porcelain`
  - Files: none written. No `git config`, no `submodule sync`, no `deinit`, no `rm` in this sub-task.
  - **DoD:** both `ls-remote` calls exited 0; all four `log`/`status` outputs are empty strings; the
    six outputs are recorded verbatim for slice 8. `git status --porcelain` at the repo root is
    byte-identical to its pre-slice value. If **any** gate failed: STOP slice 1, write the halt
    reason (which command, its exit code, its output) to the run notes, leave the repository
    untouched, and move to slice 2. Do not attempt a partial repair.

- [ ] **1.2 — Reversible repair first: Finance URL.** Only reached if 1.1 passed fully.
  - Run `git submodule sync -- plugins/Finance`.
  - Files: `.git/config` only.
  - **DoD:** `git config --get submodule.plugins/Finance.url` prints exactly
    `git@git.maxbrenne.de:clubgear/plugin-finance.git`; `plugins/Finance/.git` is still a file;
    `git -C plugins/Finance status --porcelain` is empty; `git status --porcelain` shows no new
    entry for `plugins/Finance`. If the URL is anything else, restore the previous value with
    `git config submodule.plugins/Finance.url <old>` and halt slice 1 **before** touching IamOidc.

- [ ] **1.3 — Back up `plugins/IamOidc/.git` outside the working tree and verify the ref.**
  - Copy to `/private/tmp/claude-501/-Users-max-Code-ClubGear/97dc48b1-f16f-47c2-b3a1-0c3423754294/scratchpad/iamoidc-git-backup/`
    (e.g. `cp -a plugins/IamOidc/.git <backup>`).
  - **DoD:** the backup directory exists outside `/Users/max/Code/ClubGear`;
    `git --git-dir=<backup> rev-parse refs/heads/main` prints
    `2835a1fad8cbab14602e334b02d5c11c16f3ab98`; `git --git-dir=<backup> fsck --no-progress` reports
    no missing objects. If the ref does not match `2835a1fa…`, halt slice 1 — the conversion is not
    attempted. No mutation of `plugins/IamOidc` has occurred yet at this point.

- [ ] **1.4 — Convert `plugins/IamOidc` from embedded repository to gitlink, atomically or not at all.**
  - Only reached with 1.1, 1.2 and 1.3 all green. Sequence: `git submodule deinit -f plugins/IamOidc`,
    remove the embedded `plugins/IamOidc/.git` directory, `git submodule update --init plugins/IamOidc`.
  - Files: `.git/config`, `.git/modules/plugins/IamOidc/`, `plugins/IamOidc/.git` (dir → file).
  - **DoD — a half-converted state is impossible:** all of the following hold together, and if any
    one fails the backup from 1.3 is restored to `plugins/IamOidc/.git`, `.git/modules/plugins/IamOidc/`
    is removed, and the pre-slice `.git/config` submodule entries are restored, so the end state is
    either fully converted or byte-identical to pre-slice:
    - `test -f plugins/IamOidc/.git` is true and `test -d plugins/IamOidc/.git` is false
    - `test -d .git/modules/plugins/IamOidc` is true
    - `git -C plugins/IamOidc log -1 --format=%H` prints `2835a1fad8cbab14602e334b02d5c11c16f3ab98`
    - `git -C plugins/IamOidc status --porcelain` is empty
    - `git -C plugins/IamOidc remote -v` shows `git@git.maxbrenne.de:clubgear/plugin-iamoidc.git`

- [ ] **1.5 — Confirm the superproject index was not rewritten.**
  - **DoD:** `git status --porcelain` still shows `M plugins/CarInfo` and shows **no** new entry for
    `plugins/IamOidc` or `plugins/Finance`; `git diff --cached --submodule=short` reports no gitlink
    SHA change for any plugin; `git ls-files -s plugins/IamOidc` still lists the same gitlink SHA as
    before the slice. Nothing from this slice is staged and nothing is committed.

- [ ] **1.6 — Retain the backup; record the outcome.**
  - **DoD:** the backup directory is still present and is listed in the run notes as pending
    deletion; the outcome (full success, or halt with reason and the failing command) is written down
    verbatim for slice 8 step 1. Deletion happens only in the Final Run Gate.

---

## Slice 2 — Submodule-hygiene guard, C# authoritative, with the shared vector fixture (Flow C)

All work inside `tests/ClubGear.ArchitectureTests/`. Additive only — the five pre-existing facts
keep their current assertions.

- [ ] **2.1 — Create the shared normalisation fixture.**
  - File (new, the only new repository file):
    `tests/ClubGear.ArchitectureTests/Fixtures/submodule-url-normalization.cases.tsv`
  - Content: one case per line, `<raw>\t<expected canonical>`, `#` for comment lines, no other
    syntax. Rows are the worked cases of `04_design.md:124-132`: declared SSH form, the
    `GIT_SUBMODULE_FORCE_HTTPS` credential rewrite (must canonicalise **equal** to the declared
    form), ported `ssh://…:22/…` (documented **not** equal), anonymous HTTPS without `.git`, the
    CORE-23 local absolute path, the relative `../plugin-finance.git`, and the case-folding row.
  - **DoD:** the file parses as `raw<TAB>expected` on every non-comment, non-blank line (verify with
    `grep -cP '^[^#]\S*\t\S' <file>` matching the non-comment line count); it contains at least the
    seven design rows plus the eight-step table's row 0 form; every expected value is exactly the
    canonical string given in `04_design.md:124-132`; no trailing whitespace on any line.

- [ ] **2.2 — Wire the fixture into the test project's output.**
  - File: `tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj` (or a path-resolution
    helper if the existing file-reading convention in `PluginBoundaryTests.cs` already resolves from
    the repository root — prefer the incumbent convention and change no csproj if none is needed).
  - **DoD:** `dotnet restore tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj`
    succeeds; the fixture is readable at test time by the same root-resolution path the tests use;
    if the csproj was edited, `git diff tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj`
    shows only the fixture's `Content`/`None` item and no framework, package or metadata change.

- [ ] **2.3 — Add `NormalizeRemoteUrl` and the two config parsers.**
  - File: `tests/ClubGear.ArchitectureTests/PluginBoundaryTests.cs` (existing file, additive).
    Add `private static string NormalizeRemoteUrl(string)` implementing the eight ordered steps of
    `04_design.md:105-137` exactly (step 5 uses the **last** `@` in the pre-first-`/` segment; step 6
    is scoped to that segment only), plus
    `private static IReadOnlyDictionary<string,string> ReadDeclaredSubmoduleUrls(string root)` over
    `.gitmodules` and `ReadEffectiveSubmoduleUrls(string root)` over `.git/config`. Reuse
    `ResolveRepositoryRoot()` (`:62`) and the single `PluginModules` list (`:184-191`) — do **not**
    re-declare the module set.
  - **DoD:** `dotnet build tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj -c Release --no-restore /warnaserror`
    succeeds; `git diff` on `PluginBoundaryTests.cs` shows additions only — no existing assertion
    text changed; no second `PluginModules`/module-name literal list exists in the file
    (`grep -n 'CarInfo' tests/ClubGear.ArchitectureTests/PluginBoundaryTests.cs` shows only the
    pre-existing declaration).

- [ ] **2.4 — Add `FindSubmoduleHygieneViolations(root)` and the four test members.**
  - File: `tests/ClubGear.ArchitectureTests/PluginBoundaryTests.cs`. Add
    `private static IReadOnlyList<string> FindSubmoduleHygieneViolations(string root)` per the
    pseudocode at `04_design.md:212-240` (absent from `.git/config` is **not** a violation; messages
    carry the remedy), plus `PluginSubmodules_ShouldNotBe_EmbeddedRepositories` `[Fact]`,
    `PluginSubmoduleUrls_ShouldMatch_GitmodulesDeclaration` `[Fact]`,
    `SubmoduleUrlNormalization_ShouldMatch_SharedVectors` `[Theory]` fed by `MemberData` reading the
    TSV, and `SubmoduleHygiene_ShouldReport_PlantedViolations` `[Fact]` driving a synthetic temp root.
    Add the mirroring doc-comment in the same form as `PluginBoundaryTests.cs:92`.
  - **DoD:** `SubmoduleUrlNormalization_ShouldMatch_SharedVectors` runs one case per non-comment
    fixture row and all pass; `SubmoduleHygiene_ShouldReport_PlantedViolations` surfaces both a
    planted `.git` **directory** and a planted divergent URL, and asserts each message contains its
    remedy string (`git submodule update --init plugins/<module>` and
    `git submodule sync --recursive` respectively); the planted test needs no network and no real
    defect; the five pre-existing facts still pass with unchanged assertions.

- [ ] **2.5 — Run the three-command gate and report the real-root verdict honestly.**
  - **DoD:** all three run in this order and their results are recorded:
    - `dotnet build -c Release --no-restore /warnaserror`
    - `dotnet build tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj -c Release --no-restore /warnaserror`
    - `dotnet test tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj -c Release --no-build`
  - **Expected-red is legitimate here.** If slice 1 halted at its gate, then
    `PluginSubmodules_ShouldNotBe_EmbeddedRepositories` and
    `PluginSubmoduleUrls_ShouldMatch_GitmodulesDeclaration` are red locally for exactly the two
    known defects (embedded `plugins/IamOidc/.git`, Finance effective URL pointing at the local
    path). That is the guard working. **DoD in that case:** the two reds are reported as
    expected-red, named individually, alongside slice 1's halt reason; the other seven facts and
    every fixture row are green. It is forbidden to weaken, skip, `[Fact(Skip=…)]`, conditionally
    guard, or loosen either assertion to obtain green, and forbidden to edit the fixture to obtain
    green. If slice 1 succeeded, all facts must be green with no explanation needed.

---

## Slice 3 — Mirrored CI shell step with fixture self-test (Flow C)

Depends on slice 2 (the fixture and the rule it pins). `.gitlab-ci.yml` `submodules` job
(`:136-148`) only — job name, stage and `rules` unchanged, no new job, no topology change.

- [ ] **3.1 — Add `normalize()` to the `submodules` job script.**
  - File: `.gitlab-ci.yml`, `submodules` job `script` only, appended after the existing
    `git submodule status --recursive` and the five `test -f` assertions.
  - Implement the same eight ordered steps with `sed` and POSIX parameter expansion only. **No `awk`
    program, no Python** — the job image is the plain `dotnet` image and must not grow a dependency.
  - **DoD:** `grep -n 'awk\|python' ` over the added lines returns nothing; the job's `image`,
    `stage`, `rules` and name are byte-identical to HEAD; `git diff .gitlab-ci.yml` shows changes
    confined to that one job's `script`.

- [ ] **3.2 — Add the fixture self-test loop, before any real submodule is inspected.**
  - File: `.gitlab-ci.yml` (same job). Loop over
    `tests/ClubGear.ArchitectureTests/Fixtures/submodule-url-normalization.cases.tsv`, skipping `#`
    and blank lines, asserting `normalize(raw) == expected` for every row, exiting non-zero and
    naming the offending row on mismatch.
  - **DoD:** the self-test block appears **before** the per-module checks in the script; extracted
    and run under `bash` and under `sh` against the real fixture, it passes every row and prints a
    per-row or summary confirmation; its output for every row equals slice 2's C# `NormalizeRemoteUrl`
    output for the same row (compare the two result lists and show they are identical).

- [ ] **3.3 — Add the two per-module checks and the mirroring/caveat comment.**
  - File: `.gitlab-ci.yml` (same job). Per-module embedded-repo check (`plugins/<X>/.git` must be a
    file) and URL check (`normalize(declared) == normalize(effective)`), reading `.gitmodules` and
    `.git/config` directly — no shelling out to `git` for the URLs. Add a comment stating the
    mirroring with `PluginBoundaryTests.cs` in the same form as `PluginBoundaryTests.cs:92`, and the
    fresh-clone caveat from `04_design.md:160-178` (in a fresh CI clone the step is structurally
    incapable of failing; it exists for parity and for non-fresh runners).
  - **DoD:** pointed at a planted temp root containing a `.git` **directory** and a divergent URL, the
    script exits non-zero and names the offending module; pointed at this checkout it agrees with
    slice 2's verdict (both green, or both flagging the same two modules); the YAML parses
    (`python3 -c "import yaml,sys;yaml.safe_load(open('.gitlab-ci.yml'))"` or equivalent available
    parser) ; the comment names both the mirrored test and the caveat.

- [ ] **3.4 — If this slice added or amended any fixture row, re-trigger slice 2's C# gate.**
  - The fixture is C# input too, so a row added here constrains both languages.
  - **DoD:** either (a) `git diff tests/ClubGear.ArchitectureTests/Fixtures/submodule-url-normalization.cases.tsv`
    is empty for this slice and that is stated explicitly, or (b) all three gate commands are re-run
    after the fixture edit and recorded:
    `dotnet build -c Release --no-restore /warnaserror`,
    `dotnet build tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj -c Release --no-restore /warnaserror`,
    `dotnet test tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj -c Release --no-build`,
    with `SubmoduleUrlNormalization_ShouldMatch_SharedVectors` green on the new row count.

---

## Slice 4 — `Readme.md` states the repository's submodule topology truthfully (Flow A)

- [ ] **4.1 — Capture the original wording byte-for-byte.**
  - `git show fbf4249:Readme.md | sed -n '76,77p'`
  - **DoD:** the two original lines are recorded verbatim for quoting in the blockquote.

- [ ] **4.2 — Rewrite `Readme.md:76-77` in place.**
  - File: `Readme.md`, lines 76-77 only. New claim: all five plugins, Finance included, are tracked
    as git submodules under the `clubgear` GitLab group.
  - **DoD:** for all five modules the new sentence's claim is verifiable —
    `grep -c '^\[submodule' .gitmodules` is 5, `.gitmodules:13-15` declares `plugins/Finance`, and
    `test -f plugins/<X>/.git` holds for each (allowing for slice 1's outcome on IamOidc, in which
    case the README still states the declared topology, which `.gitmodules` supports);
    `git diff Readme.md` touches no other paragraph.

- [ ] **4.3 — Append the dated correction blockquote.**
  - File: `Readme.md`, immediately after the rewritten sentence. Form per `04_design.md:367-372`:
    `> **Correction, 2026-09-14.**`, quoting the false sentence verbatim, citing `.gitmodules:13-15`
    and the `plugins/Finance/.git` gitlink file, naming CORE-23.
  - **DoD:** the quoted original matches 4.1 byte-for-byte; the date reads `2026-09-14`; the cite
    `.gitmodules:13-15` resolves at HEAD (`sed -n '13,15p' .gitmodules` shows the Finance submodule
    block); `CORE-23` appears in the note; no item status or count is asserted here (that is slice 8).

---

## Slice 5 — Activation values become derived, not published (Flow A)

- [ ] **5.1 — Read the authoritative versions before writing anything.**
  - Read `version` from `plugins/CarInfo/plugin.json`, `plugins/Finance/plugin.json`,
    `plugins/IamOidc/plugin.json`, `plugins/Inventar/plugin.json`,
    `plugins/ServiceBook/plugin.json`; list each `plugins/<X>/dist/` state.
  - **DoD:** the five versions are recorded and match the design's table
    (`04_design.md:300-306`): CarInfo `1.0.10`, Finance `1.9.0`, IamOidc `0.1.0`, Inventar `1.0.0`,
    ServiceBook `1.0.4`. Any mismatch is resolved in favour of `plugin.json` (it is authoritative)
    and noted.

- [ ] **5.2 — Rewrite `plugins/aktivierung.md` as a procedure, not payloads.**
  - File: `plugins/aktivierung.md`, rewritten per `04_design.md:277-314` in that order: title with
    `Last-Validated: 2026-09-14` (replacing `Generated:`); the "why there are no values here" note;
    the `python3 scripts/generate-plugin-activation-values.py plugins/<X>` procedure with its input
    table (version/`moduleId`/name from `plugin.json`; package from `dist/<X>-<version>.zip`; hash
    from `<X>-<version>.sha256`; signature from `<X>-<version>.signature.b64`; signer key from
    `dist/signer-public.pem`; `PLUGIN-SHA256SUMS` as the cross-plugin aggregate) and the fact that it
    recomputes the zip SHA-256 and **aborts on mismatch**; the `dist/`-retrieval note (the
    `package-plugins` job `.gitlab-ci.yml:166-204`, artifacts **expire in 7 days** `:195-200`, job
    covers CarInfo, ServiceBook and Finance **only**, so `scripts/package-plugin.sh plugins/<X>` is
    the durable route); the five values-free per-plugin sections with CarInfo `1.0.10` **awaiting
    release**; the closing note on the stale per-plugin `aktivierungswerte.md` files (Finance
    `1.5.0`, ServiceBook `1.0.2`).
  - **DoD:**
    - `grep -n 'expectedSha256Hex\|signatureBase64\|signerPublicKeyPem\|Marketplace descriptor values' plugins/aktivierung.md`
      returns nothing.
    - The two previously published hash literals no longer appear **anywhere** in the repository
      (`git grep -n <hash1>` and `git grep -n <hash2>` both empty).
    - Each of the five recorded versions equals its `plugins/<X>/plugin.json` version (5.1).
    - The CarInfo section records the **absence** of the release, so run 1 cutting `v1.0.10` later
      cannot invalidate it.
    - Every path and line cite in the rewritten file resolves at HEAD (`.gitlab-ci.yml:166-204` and
      `:195-200` shown to be the `package-plugins` job and its `expire_in`).
    - `dist/`, `PLUGIN-SHA256SUMS` and every `signer-public.pem` are unmodified
      (`git status --porcelain` shows no entry for them) — no re-signing, no key rotation.

- [ ] **5.3 — Append the dated entry to `ADRs/consolidated/finance-plugin.adr.md`.**
  - File: `ADRs/consolidated/finance-plugin.adr.md`, appended at end of file per
    `04_design.md:379-391`: `## 2026-09-14 - Activation values are derived, not published`, with
    Decision and Rationale sections, naming CORE-22 item 4.
  - **DoD:** `git diff` shows additions at end of file only; lines `:740` and `:747` are
    byte-identical to HEAD (`git show fbf4249:ADRs/consolidated/finance-plugin.adr.md | sed -n '740p;747p'`
    equals the working-tree lines) — ADR append-only discipline; the new entry's commands resolve
    (`scripts/generate-plugin-activation-values.py` exists).

- [ ] **5.4 — Repair only the references this edit invalidated.**
  - Files: `docs/backlog/plugins.md:181-186` (FIN-01 — the `plugins/aktivierung.md:57,61` cites) and
    `docs/backlog/core-platform.md` `:40`/`:327` pointer **targets** only.
  - **DoD:** no `plugins/aktivierung.md:57` or `:61` cite remains anywhere
    (`git grep -n 'aktivierung.md:5[0-9]\|aktivierung.md:6[0-9]'` empty); each replaced cite resolves
    at HEAD against the rewritten file; **no** item status, count, section prose or run row was
    changed in either file (those belong to slice 8) — verified by reading the diff hunks;
    `docs/backlog/next-runs.md` is **not** touched — it is untracked, so prove it with
    `diff <scratchpad>/pre/next-runs.md docs/backlog/next-runs.md` returning no differences, not with
    `git diff`. Staged paths for this slice are exactly the four listed in the staging section; `git
    diff --cached --name-only` shows no other path and never `plugins/CarInfo` or `CLAUDE.md`.

---

## Slice 6 — The canonical-constructor example is corrected to the *measured* shape (Flow A)

- [ ] **6.1 — STEP 1: measure the diagnostic (`CS0414` is a hypothesis, not a fact).**
  - Throwaway project **outside** this repository (use the session scratchpad). Compile the original
    `docs/plugin-contract.md:86-94` snippet — `private readonly IPluginHostContext _host;` with the
    expression-bodied constructor — against `ClubGear.Plugin.Contracts` using
    `dotnet build -c Release /warnaserror`. Record the exact diagnostic ID and full message text.
  - **DoD:** no file inside `/Users/max/Code/ClubGear` was created or modified by this step
    (`git status --porcelain` identical before and after); the observed ID and message are written
    down verbatim; **one** of three written branches is selected and recorded:
    (a) `CS0414` observed → the note names `CS0414`;
    (b) a different ID observed → the note names **that** ID;
    (c) nothing emitted → the diagnostic-ID sentence is **dropped** and the note confines itself to
    what is verified (the field is never read; the shape diverges from the core's own fixtures).
    A plausible-but-unobserved ID is never written.

- [ ] **6.2 — Capture the original two lines, then correct `docs/plugin-contract.md:86-94` in place.**
  - `git show fbf4249:docs/plugin-contract.md | sed -n '86,94p'` first.
  - File: `docs/plugin-contract.md`, lines 86-94 → the block-bodied constructor assigning a public
    auto-property per `04_design.md:322-333`.
  - **DoD:** the corrected snippet compiles clean under `dotnet build -c Release /warnaserror` in the
    same throwaway project from 6.1; the original two lines are recorded and match
    `git show fbf4249:docs/plugin-contract.md` byte-for-byte; no other section of the doc changed.

- [ ] **6.3 — Append the dated correction blockquote with the measured reason.**
  - File: `docs/plugin-contract.md`, immediately after the corrected snippet. Form per
    `04_design.md:337-349`: `> **Correction, 2026-09-14.**`, quoting the original two lines verbatim,
    citing `plugins/CarInfo/.gitlab-ci.yml:51,72`, `plugins/Finance/.gitlab-ci.yml:51,72`,
    `plugins/ServiceBook/.gitlab-ci.yml:51,72`, and
    `tests/ClubGear.ArchitectureTests/Fixtures/AllowedPluginRuntimeFixtures.cs:143-148` — **`:143-148`,
    not `:143`** (CORE-22 item 5's own cite is off by five: `:143` is the constructor, the public
    auto-property is at `:148`). Naming CORE-22 item 5.
  - **DoD:** the branch chosen in 6.1 is the one reflected in the wording; the quoted original is
    byte-identical to `fbf4249`; every cite resolves at HEAD (`sed -n '51p;72p'` on each of the three
    plugin CI files shows a `/warnaserror` line; `sed -n '143,148p'` on
    `AllowedPluginRuntimeFixtures.cs` shows the constructor through the public auto-property); the
    string `:143` does not appear alone as that cite anywhere in the new text; no `## Version history`
    section was added (contract stays `1.13.0`; this is a correction to an example).

- [ ] **6.4 — Add the required `CHANGELOG.md` entry.**
  - File: `CHANGELOG.md` — one `Changed` entry, required by `docs/plugin-contract.md:257-258` for any
    edit to the contract doc.
  - **DoD:** `diff <scratchpad>/pre/CHANGELOG.md CHANGELOG.md` shows exactly one added `Changed`
    bullet describing the example correction and nothing else — `CHANGELOG.md` already carries
    pre-existing uncommitted edits, so this check is against the pre-slice snapshot, **not** against
    HEAD; those pre-existing edits are left intact and unmodified; no version bump and no
    contract-version change; `docs/plugin-contract.md:257-258` still reads as the rule that mandated
    it.

- [ ] **6.5 — Repair only the reference this edit invalidated, then re-run the gate.**
  - File: `docs/backlog/plugins.md:141-142` — the CAR-02 note asserting the doc still shows the
    failing field shape.
  - **DoD:** the CAR-02 note no longer claims the doc shows the failing shape and its cites resolve;
    no item status or count changed in `plugins.md` — verified with
    `diff <scratchpad>/pre/plugins.md docs/backlog/plugins.md`, since `plugins.md` is already dirty
    (slice 8 owns statuses and counts); `docs/backlog/next-runs.md` untouched, proven by
    `diff <scratchpad>/pre/next-runs.md docs/backlog/next-runs.md`. The three-command gate is re-run and green (no repository C# changed, but `CHANGELOG.md`
    and the contract doc are part of the run's gate discipline):
    `dotnet build -c Release --no-restore /warnaserror`,
    `dotnet build tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj -c Release --no-restore /warnaserror`,
    `dotnet test tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj -c Release --no-build`.

---

## Slice 7 — The run's decisions land by kind of decision (Flow A + C)

Depends on slices 2-3 (the constraint's text is the shipped guard) and slice 5 (the secondary
decision).

- [ ] **7.1 — Write the committed architecture ADR.**
  - File (new, tracked): `ADRs/0003-plugin-submodule-hygiene.md`. Naming `NNNN-kebab-case.md` and
    structure matching `0001`/`0002`: `# ADR 0003: …`, `Date: 2026-09-14`, `Status: Accepted`, prose
    sections. Primary decision: no embedded `.git` directories, and effective submodule URLs must
    resolve to the `.gitmodules` declaration under host+path normalisation. Secondary decision:
    derived values are pointed at, never republished. Include the "numbered top-level, not
    `consolidated/`" reasoning from `04_design.md:464-469`.
  - **DoD:** heading structure matches `ADRs/0002-ui-style-guide.md`'s (same top-level heading form,
    `Date:`/`Status:` lines present); `0003` is the next free number (`ls ADRs/*.md` shows only
    `0001`, `0002` before this); the date reads `2026-09-14`; **every** claim cites a shipped
    artifact from slices 2, 3 or 5 (`PluginBoundaryTests.cs` member names, the fixture TSV path, the
    `.gitlab-ci.yml` `submodules` job, `plugins/aktivierung.md`) and each cited path exists at HEAD.

- [ ] **7.2 — Index the new ADR without touching `ADRs/README.md:9`.**
  - File: `ADRs/README.md` — one `## Structure` bullet for `0003`, alongside the `0001`/`0002` bullets.
  - **DoD:** `git diff ADRs/README.md` shows exactly one added bullet line; line `:9` is
    byte-identical to HEAD (`git show fbf4249:ADRs/README.md | sed -n '9p'` equals the working-tree
    line 9) — it records that the committed lowercase `adr/` no longer exists and must stay true.

- [ ] **7.3 — Ignore `/adr/` and write the untracked process record.**
  - Files: `.gitignore` (add `/adr/`); `adr/record-corrections-and-submodule-hygiene.adr.md` (new,
    **untracked**) holding guard placement, pointer-target choice, correction forms, repair gating.
  - **DoD:** `git check-ignore -v adr/` resolves to the new `.gitignore` rule;
    `git status --porcelain` shows **no** `adr/` entry; the process record exists on disk and is
    **not** inside `.crispy/`; no committed lowercase `adr/` path is reintroduced
    (`git ls-files adr/` empty).

---

## Slice 8 — Backlog records reconciled, item file first (Flow A)

Depends on every prior slice, including slice 1's outcome (halt or success). **The internal order is
the governance rule at `next-runs.md:207-209`, not a preference.** No earlier slice touched
`next-runs.md`.

- [ ] **8.1 — ORDER STEP 1: `docs/backlog/core-platform.md` first (source of truth).**
  - File: `docs/backlog/core-platform.md` — CORE-22 row `:40`; preamble `:290,:292` (four→six items;
    item 4 no longer open); item 4 `:327-331` (drop the category-restructure mandate, which defers to
    run 6); item 5 `:332-340` (including the `:143`→`:143-148` cite fix); CORE-23 row `:41` and
    section `:370-391` — recording the committed guard, the `Readme.md` instance of the same defect
    class, and **slice 1's actual outcome**.
  - **DoD:** the page says six items and does not say item 4 is open; the category restructure is no
    longer mandated for this run; the `:143-148` cite appears and no bare `:143` cite for the fixture
    remains; slice 1's outcome is recorded truthfully **including a halt** (with the failing gate
    command if it halted); every file:line cite added or touched here resolves at HEAD.

- [ ] **8.2 — ORDER STEP 2: `docs/backlog/next-runs.md` second, after 8.1 is complete.**
  - File: `docs/backlog/next-runs.md` — `:33`; `:67-91` including the `/crispy:start` line at `:90`
    whose category-restructure wording must go; header `:3-4`.
  - **DoD:** this file's first modification in the whole run happens here, after 8.1. Because the
    file is **untracked**, prove it with the snapshot taken at run start:
    `diff <scratchpad>/pre/next-runs.md docs/backlog/next-runs.md` was identical through slices 1-7
    and differs only now. Staging it requires an explicit `git add docs/backlog/next-runs.md`. The
    category-restructure wording is gone from `:90`; `core-platform.md` and `next-runs.md` do **not**
    disagree on any statement about CORE-22 or CORE-23 (compare the two pages' claims item by item);
    the governance rule text at `:207-209` itself is unchanged.

- [ ] **8.3 — ORDER STEP 3: remaining pages, aligned to 8.1.**
  - Files: `docs/backlog/plugins.md`; `docs/backlog/domains.md` (`:66`, `:175`, `:179-180`);
    `docs/backlog/README.md`.
  - **DoD:** CG-BL-050 row `docs/backlog/README.md:153` **still reads `open`** and is confirmed not
    edited — `README.md` is already dirty, so check
    `diff <scratchpad>/pre/README.md docs/backlog/README.md` has no hunk covering `:153`, not
    `git diff` against HEAD; no page mandates the
    category restructure for this run; no page says "four items" or that item 4 is open
    (`git grep -n 'four items'` empty).

- [ ] **8.4 — Run-wide cite audit.**
  - **DoD:** every file:line cite added or touched by this entire run resolves at HEAD — enumerate
    them from `git diff` and check each one; a single dangling cite fails the slice, because that is
    the defect class CORE-22 exists to remove.

---

## Final Run Gate

- [ ] **G1 — Three-command verification gate, in order, from `/Users/max/Code/ClubGear`.**
  - `dotnet build -c Release --no-restore /warnaserror`
  - `dotnet build tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj -c Release --no-restore /warnaserror`
  - `dotnet test tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj -c Release --no-build`
  - **DoD:** all three exit 0, **or** the only failures are slice 2's two expected-red facts with
    slice 1's halt reason attached. If the restore state is stale, run the Install command first:
    `dotnet restore tests/ClubGear.ArchitectureTests/ClubGear.ArchitectureTests.csproj`.

- [ ] **G2 — Host still boots (smoke check).**
  - `dotnet run --project ClubGear.csproj`
  - **DoD:** the host starts and serves without a startup exception, then is stopped. No slice in
    this run changes a CLI, script or other runnable entry point, so this is a smoke check rather
    than a slice-level acceptance criterion; the Setup command
    `git submodule update --init --recursive` is the prerequisite if submodules are not initialised.

- [ ] **G3 — Layout and diff discipline.**
  - **DoD:** the only new **repository** file is
    `tests/ClubGear.ArchitectureTests/Fixtures/submodule-url-normalization.cases.tsv` plus
    `ADRs/0003-plugin-submodule-hygiene.md`; `git status --porcelain` shows no `adr/` entry; no
    `src/features/` or relocated folder exists; nothing from slice 1 is staged or committed; the full
    `git diff` is reviewed before finishing.
  - **DoD — out-of-scope paths never entered a commit:** `git status --porcelain` still shows
    `M plugins/CarInfo` as an **unstaged** modification and `CLAUDE.md` as an **unstaged**
    modification; `git log --name-only fbf4249..HEAD` lists neither `plugins/CarInfo` nor
    `CLAUDE.md`; `git diff --cached --name-only` is empty at the end of the run. The `plugins/CarInfo`
    pointer change predates this run and is outside its scope — no slice commits it.
  - **DoD — no worktree artifacts:** `git worktree list` shows no `.crispy-worktree` entry,
    `git branch --list 'crispy/*'` is empty, and `.crispy-worktree/` does not exist. All work is on
    `main` in `/Users/max/Code/ClubGear`.

- [ ] **G4 — Delete the slice-1 backup, last.**
  - Remove `/private/tmp/claude-501/-Users-max-Code-ClubGear/97dc48b1-f16f-47c2-b3a1-0c3423754294/scratchpad/iamoidc-git-backup/`
  - **DoD:** deleted **only** after G1-G3 pass; if slice 1 halted, there is no backup to delete and
    that is recorded. `test -f plugins/IamOidc/.git` and
    `git -C plugins/IamOidc log -1 --format=%H` = `2835a1fad8cbab14602e334b02d5c11c16f3ab98` are
    re-confirmed immediately before deletion.
