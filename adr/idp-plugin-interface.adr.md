# ADR: IdP Plugin Interface

## Purpose

Persistent architectural decisions for the idp-plugin-interface iteration.

Append new entries. Do not rewrite prior history.

---

## 2026-06-17 - Runtime IdP Plugins Are Not Viable Under Current ASP.NET Core Startup Ordering

### Decision

Runtime-dynamic OIDC/OAuth2 auth handler registration via loaded plugins is rejected as unviable. The design uses a configuration-based alternative: a host-registered generic OIDC handler whose options are populated at request time from `SystemConfigEntry`, with plugin code contributing metadata and claims-mapping logic — not the middleware handler itself.

### Rationale

`Program.cs` loads plugins after `app.Build()` via `IPluginLifecycleService.LoadActivatedAsync()`. Remote auth handler types (`OpenIdConnectHandler`, etc.) must be registered in DI before `app.Build()` because the middleware pipeline snapshot is frozen at that point. `IAuthenticationSchemeProvider.AddScheme` can add scheme names post-startup, but only points at handler types already in DI. Since `Program.cs` registers no OIDC or OAuth2 handler type today, plugin-supplied handlers arrive too late. The only viable architecture without a host restart is to have the host own the handler type (registered before Build) and let plugins supply configuration and mapping logic.

---

## 2026-06-17 - Configuration-Based Alternative: OidcOptionsReloader + SystemConfigEntry

### Decision

A single `AddOpenIdConnect("oidc.generic", ...)` call is added to `Program.cs` before `builder.Build()`. Its runtime parameters (authority, clientId, clientSecret, scopes, callbackPath) are supplied at request time by `OidcOptionsReloader`, a singleton implementing `IConfigureNamedOptions<OpenIdConnectOptions>`. `OidcOptionsReloader` reads `SystemConfigEntry` rows under `Section = "externallogin.<providerKey>"` via `ISystemConfigService`. No per-provider DI scheme registration is performed; all plugin-declared providers share the single generic handler and are disambiguated by a `providerKey` state parameter in the challenge.

### Rationale

This is the standard ASP.NET Core pattern for deferred/runtime-configurable OIDC options — the handler type is static (satisfying the DI constraint before Build), while the option values are dynamic (read at request time from persistent config). This unblocks plugin-supplied IdP providers without requiring a host restart and without duplicating handler type registrations per provider.

---

## 2026-06-17 - IIdentityProviderPlugin: Metadata + Claims Mapping Only, No Handler Code

### Decision

`IIdentityProviderPlugin` (in `ClubGear.Plugin.Contracts`) exposes three capabilities: `GetConfigSchema()` (field schema for admin UI), `TestConnectionAsync(config, ct)` (optional connectivity check), and `MapClaimsAsync(context, ct)` (transform or augment raw IdP claims into `PluginClaimEntry` entries). Plugin code never touches ASP.NET Core Identity or Authentication types directly. All auth types are mirrored at the Contracts boundary: `PluginClaimEntry`, `PluginExternalLoginContext`, `PluginExternalLoginTestResult`.

### Rationale

`ForbiddenNamespaces` enforces isolation from `ClubGear.*` core namespaces. While `Microsoft.AspNetCore.Authentication` is not currently blocked by the enforcement array, the Contracts assembly must not reference it (confirmed by `ContractsAssembly_ShouldNotReference_CoreAssembly` boundary test convention). Mirror types analogous to `PluginRuntimeNotification` / `NotificationMessage` are the established pattern for crossing this boundary without coupling Contracts to ASP.NET Core.

---

## 2026-06-17 - ExternalLogin Path Added to AccountController, Not a New Controller

### Decision

Two new `[AllowAnonymous]` GET actions — `ExternalLoginChallenge` and `ExternalLoginCallback` — are added to the existing `AccountController` (`Controllers/Account/Account_Controller.cs`). No new account-facing controller is created.

### Rationale

`AccountController` already owns all unauthenticated entry points (Login, Register, AccessDenied, Logout). Adding the challenge and callback actions there keeps route coherence (`/Account/ExternalLogin*`) and avoids splitting login-related routes across two controllers. The controller remains thin: both actions delegate immediately to `IExternalLoginService`.

---

## 2026-06-17 - OauthID as the External Identity Link Key

### Decision

`Member.OauthID` (string, max 100, UNIQUE index) is the lookup key for linking an ASP.NET Identity `ApplicationUser` to a `Member` record after successful external login. `ExternalLoginService.HandleCallbackAsync` queries `Member` by `OauthID = subjectClaim` after obtaining claims from the IdP. If no `Member` is found, the outcome `ExternalLoginStatus.NoLinkedMember` is returned, and the user is shown a link-account page; no automatic `Member` record is created.

### Rationale

`Member.OauthID` already carries a UNIQUE constraint enforced in `ApplicationDbContext.OnModelCreating`, confirming intent as a uniqueness key for external identity linking. The existing admin `Edit` view already allows operators to set `OauthID`. No automatic member provisioning is designed in this iteration (deferred to a future iteration) to avoid scope creep and unintended trust escalation.

---

## 2026-06-17 - PluginSchemaFieldType.Secret Added for Secret Field Masking

### Decision

A new enum value `Secret` is appended to `PluginSchemaFieldType` in `PluginSchemaContracts.cs`. The admin UI renders `Secret` fields as `<input type="password">` and displays `***` for any saved non-empty value. The underlying storage remains a plaintext `string` in `SystemConfigEntry.Value`. Encryption at rest is explicitly deferred.

### Rationale

The task requirement is a masked admin UI field for IdP credentials (client secret, etc.). No `Password` or `IsSecret` flag exists in the current schema contracts. Adding a `Secret` enum value is additive and backward-compatible for existing plugins. Encryption at rest (e.g., ASP.NET Core Data Protection API applied to `SystemConfigEntry`) is a separate concern deferred to a future iteration; the `Secret` field type establishes the UI contract independently of the storage contract.

---

## 2026-06-17 - PluginIdentityProviderContribution as 11th Positional Parameter of RegisteredPluginRuntime

### Decision

`RegisteredPluginRuntime` gains an 11th positional parameter `IReadOnlyList<PluginIdentityProviderContribution> IdentityProviders` appended after `AuditSinks` (10th). `PluginIdentityProviderContribution(string ProviderType, int Order = 0)` is added to `PluginContributions.cs`. `IPluginContributionSink` gains `void AddIdentityProvider(PluginIdentityProviderContribution contribution) { }` with a default no-op body. All construction sites receive the new trailing argument.

### Rationale

This follows the identical pattern established for `AuditSinks` (10th parameter, added in the audit-sink-plugins iteration) and all prior contribution types. The default no-op body ensures existing compiled plugins are unaffected without recompilation.

---

## 2026-06-17 - ContractVersion Bumped to 1.7.0

### Decision

`ContractVersion.Current` advances from `1.6.0` to `1.7.0`. `MinimumSupported` remains `1.0.0`.

### Rationale

`IIdentityProviderPlugin`, the three mirror record types, `PluginIdentityProviderContribution`, `PluginSchemaFieldType.Secret`, and the `identity.provider` extension point are all new additions to the public Contracts surface. These are additive (minor) changes. Plugins using the new interface must declare `requiredCoreVersion: ">=1.7.0"`.

---

## 2026-06-17 - Pre-Implementation User Approval Gate (ADR 0001 GR-002)

### Decision

Before the Builder begins any file changes, the user must explicitly confirm approval for the core changes described in this design. The Builder's plan must start with an explicit approval gate. Core changes requiring approval: `Program.cs` `AddOpenIdConnect` addition, `RegisteredPluginRuntime` 11th parameter, `ContractVersion` bump to `1.7.0`, new `PluginSchemaFieldType.Secret` enum value.

### Rationale

`01_task.md` states: "Vor Core-Aenderungen ist User-Freigabe nach ADR 0001 erforderlich." The changes above modify core infrastructure (startup registration, the shared runtime record, the public Contracts version). This gate must not be bypassed.
