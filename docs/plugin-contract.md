# The Plugin Contract

`Contracts/Plugin` is a separate assembly with its own SemVer, declared in
`Contracts/Plugin/ContractVersion.cs`. It is unrelated to the core's CalVer in `ClubGear.csproj` —
do not sync the two.

```csharp
ContractVersion.Current           // what this host implements
ContractVersion.MinimumSupported  // the oldest plugin build this host still loads
```

A plugin records the contract version it was built against in its manifest. The host compares the
two on load: a plugin newer in major version than the host is rejected, a plugin older than
`MinimumSupported` is rejected, anything in between loads.

## What "additive" means here, and why it matters

Plugins are distributed as compiled, signed packages. They are **not** recompiled when the core
changes. A contract change is therefore only safe if already-compiled plugin assemblies keep
resolving every member they reference at runtime.

That rules out some things that look harmless in source:

| Change | Source-compatible | Binary-compatible |
|---|---|---|
| New interface member **with** a default implementation | yes | yes |
| New interface member without one | no | no |
| New `init`-only property on a record | yes | yes |
| New parameter on a record's primary constructor, even with a default value | yes | **no** |
| Reordering constructor parameters | no | no |
| Removing or renaming anything | no | no |

The fourth row is the trap. Adding an optional parameter to a positional record changes the
constructor arity, and a plugin compiled against the old arity fails at load time with a
`MissingMethodException`. Add an init-only property instead.

`MinimumSupported` moves only when an old shape is actually **removed**. Adding never moves it.

## Version history

### 1.15.0 — icon columns on generic plugin pages

A `PluginPageColumn` can now be flagged as an icon column. When `IsIconColumn` is `true`, the host
renders that column's cell value as a Bootstrap Icon (`<i class="bi {value}"></i>`) instead of
plain text, in both the list view and the detail view — the same rendering `PluginPageCommand.Icon`
and `PluginNavEntry.Icon` already use, extended to a per-row value. The cell value is still
whatever string the provider's `GetRowsAsync` returns for that key — typically a Bootstrap Icons
class name such as `bi-box-seam`.

**Added:** init-only property `PluginPageColumn.IsIconColumn`, defaulting to `false`. The existing
positional constructor is unchanged, so this increment is binary-compatible and `MinimumSupported`
remains `1.13.0`.

### 1.14.0 — grouped navigation and multi-page modules

Plugin navigation entries can now declare optional grouping metadata. The host renders navigation
entries sharing a `GroupKey` in one dropdown in the main navigation instead of as separate flat
links.

A module can now also register more than one `page.generic` provider. Before 1.14.0 the host
always resolved the first-registered provider for a module regardless of the requested
`pageKey`, so a second `AddPageProvider` call was unreachable. The host now matches a contribution
by `PageKey` first; a module that registers exactly one provider without a `PageKey` keeps
resolving exactly as before.

**Added:** init-only properties `PluginNavEntry.GroupKey`, `PluginNavEntry.GroupLabel`,
`PluginNavEntry.GroupIcon`, and `PluginPageProviderContribution.PageKey`. All existing positional
constructors are unchanged, so this increment is binary-compatible and `MinimumSupported` remains
`1.13.0`.

**Recommended pattern for a plugin that needs distinct sections of one feature area** (e.g.
bookings vs. reports): register one `page.generic` provider per section, each with its own
`PageKey`, and give their `PluginNavEntry`s the same `GroupKey` so they appear together as one
dropdown in the main navigation — rather than building a single page that switches behaviour on a
query parameter.

### 1.13.0 — contributions are host-constructed (CORE-18)

Motivation: until 1.12.0 the host created every contribution with
`Activator.CreateInstance(type)` — a parameterless constructor and nothing else. That had three
consequences. A contribution could not be handed anything at construction, which is why
`IPluginHostContext` has no `Providers` member and why CORE-13 stopped where it did. Construction
happened in several places, some of which the host could not account for, so deactivation could
unload a plugin's `AssemblyLoadContext` while an instance was still in use. And a construction
failure surfaced inconsistently: a silently skipped slot in one place, a thrown exception during
login in another.

1.13.0 replaces that with a single rule: **a contribution has exactly one public constructor,
`T(IPluginHostContext)`**, and the host constructs it inside a scope it owns.

**Added:** nothing in the contract assembly. `IPluginHostContext` already existed and is unchanged;
it is now also the construction parameter, so it adds nothing to a plugin's compile surface.

Host-side, not part of the contract assembly: `IPluginContributionActivator.Rent<T>(moduleId,
contributionType, user)` returning a `PluginContributionLease<T>`, in
`ClubGear.Services.Abstractions`.

**Removed: the parameterless construction contract.** A shape was genuinely removed, so
`MinimumSupported` moves with `Current` to `1.13.0`. `IPluginRegistryReader.CreateMemberProvider<T>`
is gone, replaced by the pure lookup `ResolveContributionType`.

**Changed.** The loader now checks the manifest's `requiredCoreVersion` against the host's contract
range at **activation**, not only at install. Without that check an already-installed plugin would
never be re-gated and would fail later with "no `(IPluginHostContext)` constructor" — true, but the
wrong diagnosis for the administrator. Every declared contribution type is also verified to be
public, non-abstract and to declare the canonical constructor; anything else rejects the whole
plugin with a message naming the module and the type.

**What a plugin built against 1.12.0 or earlier sees.** It no longer activates. Plugin
administration shows it as incompatible with, for example:

```
Plugin 'clubgear.plugin.carinfo' ist nicht mit dieser Host-Version kompatibel:
Es verlangt Contract >= 1.0.0, der Host unterstuetzt 1.13.0 bis 1.13.0.
```

This is the intended, legible form of the break. Every plugin shipped from this repository is
affected; the adoption work is tracked per submodule in `docs/backlog/plugins.md`.

**What a plugin must do.**

```csharp
public sealed class S3StorageProvider : IAuditSinkProvider
{
    // The canonical constructor. Exactly one public constructor, exactly this parameter.
    public S3StorageProvider(IPluginHostContext hostContext)
    {
        HostContext = hostContext;
    }

    public IPluginHostContext HostContext { get; }
}
```

> **Correction, 2026-09-14.** The example above originally declared
> `private readonly IPluginHostContext _host;` with the expression-bodied constructor
> `public S3StorageProvider(IPluginHostContext host) => _host = host;`. That field was assigned and
> never read, and the shape diverges from the one the core's own fixtures use
> (`tests/ClubGear.ArchitectureTests/Fixtures/AllowedPluginRuntimeFixtures.cs:143-148`), where the
> canonical constructor assigns a public auto-property instead of a private field. The shape shown
> now is that fixture's. (CORE-22 item 5.)

and raise the manifest floor:

```json
"requiredCoreVersion": ">=1.13.0"
```

**The one exception, and it is a rule, not an oversight.** The **module entry point** — the type
implementing `IPluginModule` named by `entryPoint` — keeps its **parameterless** constructor.
`IPluginHostContext` is built from the module's manifest, and the manifest is only reachable from an
already-constructed module, so requiring the canonical constructor there would be circular. The
compensating control is that the entry-point *type* is isolation-scanned before it is instantiated,
which is strictly more than the host did before 1.13.0, where `RegisterContributions` ran wholly
unchecked. Route handlers are likewise unchanged: they are delegates, scanned at registration and
invoked through the runtime adapter.

### 1.12.0 — the provider contribution seam (CORE-13, increment 1)

Motivation: `IPluginContributionSink.AddService` has been a required contract member since the
beginning, `PluginLoader` has always collected the contributions, and `RegisteredPluginRuntime`
has always carried them on `.Services` — but **nothing ever read them**. Declaring a service was a
no-op that looked like a feature. CORE-13 gives that seam its first consumer, so a plugin can
supply a service the core or another plugin consumes: Matrix as a notification channel, S3 or
Nextcloud instead of local file storage.

**Added**

| Member | Kind |
|---|---|
| `PluginCapabilityKeys` | new static class: the host-validated allowlist of capability keys |
| `PluginCapabilityKeys.NotificationChannel` / `.StorageBlob` / `.DocumentStore` / `.AuthScheme` | `const string` |
| `PluginCapabilityKeys.All` / `.IsKnown(key)` / `.GetArity(key)` | lookup helpers, mirroring `PluginExtensionPoints` |
| `PluginCapabilityArity` | new enum: `SingleActive`, `FanOut` |

Host-side, not part of the contract assembly: `IPluginProviderRegistry` with `Resolve<T>`,
`ResolveAll<T>` and `GetRegistrations`, in `ClubGear.Services.Abstractions`.

**Removed:** nothing. `MinimumSupported` stays at `1.8.0`.

**Changed — this one can break an existing plugin.** The `Key` of a `PluginServiceContribution` is
now validated against `PluginCapabilityKeys` when the plugin loads. An unrecognised key **rejects
the whole plugin** with a message naming the allowed keys, where previously any string was
accepted and ignored. This is deliberate: a plugin whose provider is unreachable would otherwise
report as installed and healthy while doing nothing.

**What a plugin built against 1.11.0 or earlier sees.** Nothing breaks and nothing needs
recompiling — *unless* it calls `sink.AddService(...)`. If it does, the key must now be one of the
four above. No plugin shipped from this repository calls `AddService` at all, so nothing in-tree is
affected; a third-party plugin that declared a service against the old no-op behaviour must change
the key or drop the call. There is no silent-downgrade path here on purpose: the alternative is a
provider that never runs and gives no reason why.

**What a plugin must do to use the new capability.**

```csharp
public void RegisterContributions(IPluginContributionSink sink)
{
    sink.AddService(new PluginServiceContribution(
        PluginCapabilityKeys.StorageBlob,          // allowlisted key
        typeof(S3StorageProvider).FullName!));      // type in the plugin's own assembly
}
```

The provider type must be public, live in the plugin's own assembly and — since 1.13.0 — declare
the canonical constructor `T(IPluginHostContext)`. The host constructs it through
`IPluginContributionActivator.Rent<T>`, the same path the audit-sink and identity-provider seams
use. (Until 1.12.0 this read "a parameterless constructor"; that shape was removed in 1.13.0.)

**Not yet available, and why.** A provider still cannot consume *another* provider, because
`IPluginHostContext` has no `Providers` member — but the blocker is gone: CORE-18 landed in 1.13.0,
so the host now hands the context to a contribution at construction, and adding `Providers` is a
pure addition to `IPluginHostContext`. That remaining member is CORE-13's next increment. Which
provider wins a `SingleActive` capability is likewise not yet an administrator's choice — `Resolve`
returns the first by module id and logs the competing claims. That is CORE-14. Both are tracked in
`docs/backlog/core-platform.md`.

### 1.11.0 — sorting and paging for plugin pages (CORE-03, increment 1)

Motivation: `IPluginPageProvider` gave a plugin a table and a single filter string. Inventory 2.0
and Finance 2.0 both list thousands of rows, and without paging in the contract each of them would
have needed its own handling in the host — the exact special-casing CORE-03 exists to prevent.

**Added**

| Member | Kind |
|---|---|
| `IPluginPageProvider.GetRowsPageAsync(context, query, ct)` | interface method with a default implementation |
| `PluginPageQuery` | new record: filter, entity key, sort key, direction, page, page size |
| `PluginPageRows` | new record: one page of rows plus the total match count |
| `PluginSortDirection` | new enum |
| `PluginPageDefinition.DefaultPageSize` | init-only `int?`, default `null` |
| `PluginPageColumn.Sortable` | init-only `bool`, default `false` |

**Removed or changed:** nothing. `MinimumSupported` stays at `1.8.0`.

**What a plugin built against 1.10.0 sees.** Nothing breaks and nothing needs recompiling. The
default implementation of `GetRowsPageAsync` calls the plugin's existing `GetRowsAsync` and then
sorts and pages the result in memory, so such a plugin gains a working pager for free. Because
`Sortable` defaults to `false`, no column offers sorting until the plugin opts in, and because
`DefaultPageSize` defaults to `null`, the page keeps showing every row exactly as before.

**What a plugin should do to use it properly.** Mark the columns worth sorting:

```csharp
new PluginPageColumn("name", "Bezeichnung") { Sortable = true }
```

Advertise a page size on the definition:

```csharp
new PluginPageDefinition("items", "Inventar", "id", columns, commands, "inventory.read", "Suchen…")
{
    DefaultPageSize = 50
}
```

And, if the plugin has more rows than it wants to materialise per request, override the method so
sorting and paging happen in the store rather than in memory:

```csharp
public async Task<PluginPageRows> GetRowsPageAsync(
    IPluginHostContext context, PluginPageQuery query, CancellationToken ct = default)
{
    var q = _db.Items.Where(i => query.FilterValue == null || i.Name.Contains(query.FilterValue));
    var total = await q.CountAsync(ct);

    if (query.SortKey == "name")
        q = query.SortDirection == PluginSortDirection.Descending
            ? q.OrderByDescending(i => i.Name)
            : q.OrderBy(i => i.Name);

    if (query.PageSize is int size)
        q = q.Skip((query.Page - 1) * size).Take(size);

    return new PluginPageRows(await MapAsync(q, ct), total);
}
```

`TotalCount` is the number of rows matching the filter, **not** the length of `Rows`. The host
renders the pager from it; returning `Rows.Count` would collapse the pager to a single page.

**Guarantees the host provides.** A plugin never has to defend against a hand-edited query string.
Before the query reaches the plugin, `PluginPageService.Clamp` caps the page size at
`PluginPageService.MaxPageSize` (200), raises any page below 1 to 1, and discards a sort key that
does not name a column the definition marked `Sortable`.

**Ordering in the in-memory fallback** is ordinal and case-insensitive over the raw cell text, with
empty and missing values last in both directions. Cell values are strings, so a plugin needing
numeric or culture-aware ordering must override `GetRowsPageAsync` and sort in its own store.

### 1.10.0 and earlier

Not documented retroactively here. See `CHANGELOG.md` for the entries that introduced them.

## Rules for changing this contract

1. Extend only for a capability a **named, concrete** plugin needs now. Speculative surface is how
   a contract becomes impossible to remove things from.
2. Every increment ships with a compatibility test proving a provider that implements only the
   previous shape still works. See `PluginPagePagingContractTests`.
3. Bump the minor version per increment. Raise `MinimumSupported` only when an old shape is removed,
   which is a major bump.
4. Record the change in `CHANGELOG.md` under `Changed`, naming every added member, and add a section
   here.
