# Plugin License Boundary Policy

## Purpose

ClubGear core remains licensed under AGPLv2. Plugin support is implemented with a strict contract-first boundary so that third-party plugins can be distributed under proprietary or commercial licenses.

## Technical Boundary Rules

1. Plugins may depend on `ClubGear.Plugin.Contracts` only.
2. Plugins must not reference core implementation namespaces such as `ClubGear.Services`, `ClubGear.Data`, `ClubGear.Controllers`, or `ClubGear.Models`.
3. Core may reference the contract assembly to validate compatibility and host plugins.
4. Runtime interaction must occur through explicit contract types and adapter services, not through direct access to core internals.

## Compliance Notes

- Architecture tests enforce that contract artifacts do not reference the core project.
- Contract compatibility is versioned via `ContractVersion` and validated by core before activation.
- Any plugin requiring direct core implementation references violates this policy and is out of scope for commercial distribution.