# ADR 0003: Plugin Submodule Hygiene Is An Enforced Constraint

Date: 2026-09-14

Status: Accepted

## Purpose

Plugin boundaries are architectural constraints, and a plugin's boundary starts at how it is
physically tracked. Two defects were found in this repository's submodule wiring (CORE-23): the
superproject's `.git/config` pointed `submodule.plugins/Finance.url` at a local filesystem path
instead of the declared remote, and `plugins/IamOidc` was an embedded `.git` directory rather than a
gitlink. Both are invisible to every check that existed, both originate on a developer machine, and
both silently detach a plugin from the remote the repository claims it tracks.

This ADR makes them enforced violations.

## Decision

Two properties MUST hold for every plugin submodule under `plugins/`:

1. `plugins/<X>/.git` MUST be a gitlink **file**. A `.git` **directory** is an embedded repository
   and is a violation.
2. The effective submodule URL in the superproject's `.git/config` MUST resolve to the URL declared
   in `.gitmodules`, compared after normalisation.

### The normalisation rule

URLs are compared by canonical **host plus repository path**. `Normalize` is one ordered, total,
environment-independent string transformation: trim whitespace; remove a trailing `/`; remove a
trailing `.git`; if `://` is present, delete everything up to and including it; in the segment
before the first `/`, delete everything up to and including the **last** `@`; in that same segment,
replace a `:` with `/`; collapse repeated `/`; lowercase.

Two URLs match when their canonical forms are byte-equal. Therefore
`git@git.maxbrenne.de:clubgear/plugin-finance.git` and
`https://gitlab-ci-token:<token>@git.maxbrenne.de/clubgear/plugin-finance.git` both canonicalise to
`git.maxbrenne.de/clubgear/plugin-finance` and match, while the CORE-23 defect
`/Users/max/Code/ClubGear/plugins/Finance` keeps a leading `/` through every step and can never
match a host-rooted form.

Deleting to the *last* `@` is normative: the CI token may itself contain `@`. Scoping the `:`→`/`
rewrite to the pre-first-`/` segment is normative: without it, a colon inside a filesystem path or a
URL path could be rewritten and collide.

Relative URLs (`../plugin-x.git`) and ported SSH URLs canonicalise to forms that do not match the
declared absolute SSH URLs, and are violations by design.

### Where the constraint is enforced

The **authoritative** check is in `tests/ClubGear.ArchitectureTests/PluginBoundaryTests.cs`, which
reads `.gitmodules`, `.git/config` and the filesystem entry type directly rather than shelling out
to `git`. A **mirrored** shell check lives in the `submodules` validate job in `.gitlab-ci.yml`, in
the pairing form the repository already uses for `PluginSubmodules_ShouldBe_CheckedOut`.

Agreement between the two implementations is held by a single committed fixture,
`tests/ClubGear.ArchitectureTests/Fixtures/submodule-url-normalization.cases.tsv`, holding
`<raw>\t<expected canonical>` rows. The C# side drives it as a `[Theory]`; the CI shell step
self-tests against the same rows before inspecting any real submodule. Neither implementation can
drift from the rule, or from the other, without a red build.

## Consequences

**The test is the primary defence, and the CI step is parity only.** In a fresh CI clone the
mirrored step is structurally incapable of failing: `GIT_SUBMODULE_STRATEGY: recursive` means GitLab
creates every submodule from `.gitmodules` itself, so the effective URL is by construction a rewrite
of the declared one and every submodule is created as a proper gitlink. The step is recorded here as
existing for two narrower reasons — convention parity with the established validate-job/architecture-test
pairing, and catching a non-fresh or cached-clone runner whose `.git/config` is stale. Anyone
reading only the CI job MUST NOT mistake it for the real gate. The real gate is the architecture
test, because it runs on the developer machine where these defects originate.

**Normalisation is required, not a convenience.** `GIT_SUBMODULE_FORCE_HTTPS: "true"` makes CI's
effective URLs token-bearing HTTPS rewrites of the declared SSH URLs, so a byte-equality comparison
of raw URLs would fail in CI for every submodule. The alternatives were rejected: skipping the check
in CI would leave the mirrored step doing nothing, and a narrower "reject filesystem paths only"
rule would miss a divergence that points at a different real remote.

**The rule is stated as ordered steps, not a regex, because it is reimplemented in shell.** The
shared fixture converts "the two implementations ought to agree" into a property that fails the
build when it stops holding, in either language.

**Repairing an existing violation is gated.** Converting an embedded repository removes the only
local copy of its history, so a repair MUST first confirm the declared remote is reachable and that
the submodule has no uncommitted or unpushed work, with `git fetch` preceding any
`origin/main..HEAD` comparison. Enforcement ships independently of any local repair.

## Secondary decision: derived values are pointed at, never republished

Documentation MUST NOT carry copies of values derived from a build artifact — package hashes,
signatures and signer keys. It points at the artifact and the procedure that derives them. Concretely,
`plugins/aktivierung.md` names `scripts/generate-plugin-activation-values.py` run against
`plugins/<X>/dist/`, rather than publishing the values, because that script recomputes the package
SHA-256 and aborts on mismatch and is therefore self-verifying.

This is recorded here, rather than treated as record-keeping convention, because it is a durable
source-of-truth rule and not a one-off fix: the published copies had drifted from the artifacts they
described even at their own documented versions, so any future release or packaging document faces
the same decay, and this rule is also the reason no new procedure document was created to hold them.
