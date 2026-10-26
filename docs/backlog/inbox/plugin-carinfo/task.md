# Task

run 2 aus next-runs.md

## Scope resolution (chosen by the user when this run was started)

Run 2 in `../../docs/backlog/next-runs.md` is five runs, one per plugin repository. This run is the
first of them and owns **this repository only** (`plugin-carinfo`): backlog item **CAR-02**.

> CAR-02 — Adopt contract 1.13.0: canonical `T(IPluginHostContext)` constructor on all five
> provider types, manifest floor to `>=1.13.0`, re-tag and re-package. Blocks activation entirely
> until done.

The core side already shipped: contract `1.13.0` with `MinimumSupported = 1.13.0` is live in the
core repository, so this plugin currently does not activate at all. The contract text is in
`../../docs/plugin-contract.md`, section 1.13.0.

The other four adoptions (FIN-02, SB-02, INV-02, IAM-02) are separate runs in their own
repositories and are out of scope here.
