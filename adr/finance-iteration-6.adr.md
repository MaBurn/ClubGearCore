# ADR: Finance Iteration 6

## Purpose

Persistent architectural decisions for the finance-iteration-6 iteration.
Append new entries. Do not rewrite prior history.

## 2026-06-24 - Replace Inline BIC Regex with BicValidator in FinanceData.Validate

### Decision

Remove the inline `Regex.IsMatch(bic, "^[A-Z0-9]{8}([A-Z0-9]{3})?$", ...)` block in `FinanceData.cgcs` lines 513–517 and replace it with a call to the already-extracted `BicValidator.Validate(bic)`. The `if (!string.IsNullOrWhiteSpace(bic))` guard is preserved so that BIC remains optional. The `PluginFieldError` for the `"bic"` field is now populated from `bicResult.ErrorMessage` returned by the validator rather than from a hardcoded inline string. Version is bumped from `1.4.0` to `1.4.1` in both `FinancePluginModule.cgcs` and `plugin.json`.

### Rationale

The inline regex `^[A-Z0-9]{8}([A-Z0-9]{3})?$` incorrectly accepts strings where the first six characters are digits (e.g., `"12345678"`), which are not valid SWIFT BICs. `BicValidator.Validate` uses the correct SWIFT pattern `^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$` (letters-only in positions 1–6) and performs an explicit length guard (`is not 8 and not 11`). The validator was extracted in iteration 3 specifically to replace this inline check; it was wired into the standalone validator test path but the swap at the `FinanceData.Validate` call site was deferred until this iteration.

No regression risk exists: every input accepted by the old inline block that is a structurally valid SWIFT BIC is also accepted by `BicValidator.Validate`. Invalid strings rejected by the inline block remain rejected. No test or contract asserts the old error message text, so the improved message is safe to adopt. Double-normalisation analysis confirms that `BicValidator`'s internal normalisation is a no-op on the already-normalised string produced by `Normalize()`. The optional-BIC semantics are preserved because the outer `IsNullOrWhiteSpace` guard remains in place.

The version bump is `1.4.0 → 1.4.1` (patch) because this is a non-breaking correctness fix to an existing validation path with no new user-visible feature.
