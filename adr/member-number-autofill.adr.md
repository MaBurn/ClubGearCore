# ADR: Member Number Autofill

## Purpose

Persistent architectural decisions for the member-number-autofill iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-07-08 - Remove Model-Level [Required] on Member.MemberNumber; Restore Required-on-Edit Explicitly

### Decision

`[Required]` is removed from `Member.MemberNumber` (`Models/Member.cs`). This is the root cause of the
reported defect: `Create.cshtml`/`_MemberForm.cshtml` render `asp-for="MemberNumber"`, which pulls
`data-val-required` from that DataAnnotation, so jquery.validate.unobtrusive blocks a blank Create
submission client-side before the already-implemented server-side auto-numbering
(`MemberFeatureService.GenerateNextMemberNumberAsync`, called from `CreateAsync` when the field is blank)
ever gets a chance to run.

Because `Views/Members/Edit.cshtml` renders `asp-for="MemberNumber"` against the same model (it does not
reuse `_MemberForm.cshtml`), removing the annotation would also silently make Edit's Mitgliedsnummer
optional, both client- and server-side, with `MemberFeatureService.UpdateAsync` performing an unconditional
overwrite (`tracked.MemberNumber = member.MemberNumber;`) and no auto-generation fallback. Required-on-Edit
behavior is therefore restored by hand at every layer that previously relied on the DataAnnotation instead
of removing the model-wide annotation:
- `Views/Members/Edit.cshtml`: hand-authored `data-val="true" data-val-required="..."` attributes added
  directly on the `MemberNumber` `<input>`, preserving client-side blocking on Edit.
- `Controllers/Member/Member_Controller.cs`, `Edit` (POST): explicit `ModelState.AddModelError` when the
  posted value is blank, restoring server-side enforcement.
- `Services/Core/MemberFeatureService.cs`, `UpdateAsync`: the previously-unconditional assignment becomes
  conditional (only overwrite when the incoming value is non-blank), as a defense-in-depth guard at the
  service boundary against any caller that bypasses the controller's check.

The now-dead `ModelState.Remove(nameof(Member.MemberNumber))` workaround in `Create` (POST) is removed,
since `ModelState.IsValid` is already true for a blank value once `[Required]` is gone.

### Rationale

`[Required]` is annotation on a single shared model consumed by two different views with two different
business rules (optional-with-autofill on Create, mandatory on Edit). Keeping the annotation would have
required either duplicating the model for two nearly-identical forms, or accepting the reported bug as a
permanent limitation. Removing the annotation and re-establishing "required" explicitly, per-surface,
exactly where it is actually needed (Edit's view markup, Edit's controller action, and the service's write
path) is the smallest change that fixes Create without silently relaxing Edit — every place that could let
a blank number reach persistence via Edit was enumerated in research and is closed here. EF Core's column
nullability is driven by the C# nullable-reference-type annotation on the property, not by `[Required]`, so
no database migration is needed: the column remains `NOT NULL`, and `CreateAsync` already guarantees a
non-blank value (typed or generated) before every `SaveChangesAsync`.

---

## 2026-07-08 - Bounded Retry-on-Conflict for Concurrent Auto-Number Generation

### Decision

`MemberFeatureService.CreateAsync` wraps its auto-generate-and-insert sequence in a bounded retry loop
(`MaxMemberNumberGenerationAttempts = 3`) that catches `Microsoft.EntityFrameworkCore.DbUpdateException`
around `SaveChangesAsync`, re-invokes `GenerateNextMemberNumberAsync` for a fresh candidate, and retries.
If all attempts are exhausted, a `ClubGear.Services.BusinessLogicException` is thrown with a German,
user-facing message, which `GlobalExceptionMiddleware` already renders as a friendly feedback banner.

### Rationale

Research confirmed `GenerateNextMemberNumberAsync`'s existing collision check (`AnyAsync` against
`_db.Members`) and the counter increment (`UpsertSystemConfigAsync`, staged in memory only) are not wrapped
in any transaction or lock; the only durable commit point is the single `SaveChangesAsync` at the end of
`CreateAsync`, which is protected by the pre-existing unique index on `Members.MemberNumber`
(`Data/ApplicationDbContext.cs` lines 58-60). Before this iteration, a blank `MemberNumber` almost never
reached the server (client-side validation blocked it), so this race was latent and effectively unreachable
in practice. Making blank-and-autofill the normal, supported Create flow (this iteration's whole purpose)
makes concurrent double-submits of blank Mitgliedsnummer a realistic, not merely theoretical, scenario, so
shipping the autofill feature without also closing this gap would trade one bug (client-side block) for
another (occasional `DbUpdateException` surfaced as a raw 500 to the admin). A bounded catch-and-retry is
correct here without introducing locks or transactions: a `DbUpdateException` on this exact insert can only
mean a concurrent request committed the same candidate first, so a fresh `GenerateNextMemberNumberAsync`
call — which re-queries `_db.Members` from the database — is guaranteed to see that newly-committed number
and skip past it on the very next attempt.

### Explicitly Out of Scope

`AdminController.SaveConfigEntry` (`Controllers/Admin/Admin_Controller.cs` lines 102-115) is a single
generic save action shared by every `SystemConfig` section (Club data, Members numbering, and any future
section) and performs no validation of individual values (e.g. empty `MemberNumberPrefix`, non-numeric
`MemberNumberNextNumber`, negative `MemberNumberPadding`). Tightening that endpoint is a cross-cutting
change with a blast radius far beyond "Mitgliedsnummer auto-fill on Create" and is not made in this
iteration. The existing `GetConfiguredNumber`/`TryExtractNumber` fallback-and-clamp behavior in
`MemberFeatureService` already prevents any of those bad values from throwing or corrupting data — at worst
they silently reset numbering to a safe default, a separate admin-configuration-hygiene concern left for a
future iteration if requested.
