using System.Runtime.Loader;
using System.Security.Claims;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Core;
using ClubGear.Services.Plugins.Runtime;
using ClubGear.Plugin.Finance;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginNavEntryServiceTests
{
    // ----------------------------------------------------------------
    // helpers
    // ----------------------------------------------------------------

    private static IPluginRegistryReader BuildRegistry(params RegisteredPluginRuntime[] runtimes)
    {
        var registry = new PluginRegistry();
        foreach (var rt in runtimes)
        {
            // We register a no-op module so the registry accepts the runtime.
            // The nav-entry service only calls GetRegisteredPlugins(), which
            // returns the runtime objects directly.
            registry.Register(rt, new NoOpPluginModule(rt.ModuleId), AssemblyLoadContext.Default);
        }

        return registry;
    }

    private static RegisteredPluginRuntime MakeRuntime(string moduleId, IReadOnlyList<PluginNavEntry> navEntries)
        => new RegisteredPluginRuntime(
            moduleId,
            moduleId,
            new Version(1, 0, 0),
            $"test:{moduleId}",
            Array.Empty<PluginRouteContribution>(),
            Array.Empty<PluginServiceContribution>(),
            Array.Empty<PluginMemberProviderContribution>(),
            Array.Empty<PluginBackgroundJobContribution>(),
            navEntries,
            Array.Empty<PluginAuditSinkContribution>(),
            Array.Empty<PluginIdentityProviderContribution>(),
            Array.Empty<PluginSelfServiceProfileProviderContribution>());

    private static ClaimsPrincipal UserWithPermissions(params string[] permissions)
    {
        var claims = permissions
            .Select(p => new Claim("permission", p))
            .ToList();
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static ClaimsPrincipal AnonymousUser()
        => new ClaimsPrincipal(new ClaimsIdentity());

    // ----------------------------------------------------------------
    // tests
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetVisibleNavEntries_ReturnsEntry_WhenRequiredPermissionIsNull()
    {
        var entry = new PluginNavEntry("Open Nav", "bi-box", "/open", null, 10);
        var registry = BuildRegistry(MakeRuntime("plugin.a", new[] { entry }));
        var service = new PluginNavEntryService(registry, new StubPermissionService(_ => false));

        var result = await service.GetVisibleNavEntriesAsync(AnonymousUser(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Open Nav", result[0].Label);
    }

    [Fact]
    public async Task GetVisibleNavEntries_FiltersEntry_WhenPermissionNotHeld()
    {
        var entry = new PluginNavEntry("Secret Nav", "bi-lock", "/secret", "secret.read", 10);
        var registry = BuildRegistry(MakeRuntime("plugin.b", new[] { entry }));
        var service = new PluginNavEntryService(registry, new StubPermissionService(_ => false));

        var result = await service.GetVisibleNavEntriesAsync(AnonymousUser(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetVisibleNavEntries_IncludesEntry_WhenPermissionIsHeld()
    {
        var entry = new PluginNavEntry("Members Nav", "bi-people", "/members", "members.read", 10);
        var registry = BuildRegistry(MakeRuntime("plugin.c", new[] { entry }));
        var service = new PluginNavEntryService(
            registry,
            new StubPermissionService(key => key == "members.read"));

        var result = await service.GetVisibleNavEntriesAsync(
            UserWithPermissions("members.read"), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Members Nav", result[0].Label);
    }

    [Fact]
    public async Task GetVisibleNavEntries_ReturnsSortedBySortOrderThenLabel()
    {
        var entries = new[]
        {
            new PluginNavEntry("Zebra", "bi-z", "/z", null, 20),
            new PluginNavEntry("Alpha", "bi-a", "/a", null, 20),
            new PluginNavEntry("First", "bi-f", "/f", null, 10),
        };
        var registry = BuildRegistry(MakeRuntime("plugin.d", entries));
        var service = new PluginNavEntryService(registry, new StubPermissionService(_ => false));

        var result = await service.GetVisibleNavEntriesAsync(AnonymousUser(), CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal("First", result[0].Label);   // SortOrder 10
        Assert.Equal("Alpha", result[1].Label);   // SortOrder 20, Label "Alpha" < "Zebra"
        Assert.Equal("Zebra", result[2].Label);   // SortOrder 20, Label "Zebra"
    }

    [Fact]
    public async Task GetVisibleNavEntries_IncludesFinanceNavEntry_WhenPermissionServiceGrantsKassenwartAccess()
    {
        var entry = new PluginNavEntry(
            "Kassenwart", "bi-cash-coin", "/finance", FinancePermissions.KassenwartAccess, 10);
        var registry = BuildRegistry(MakeRuntime("clubgear.plugin.finance", new[] { entry }));
        var service = new PluginNavEntryService(
            registry,
            new StubPermissionService(key => key == FinancePermissions.KassenwartAccess));

        var result = await service.GetVisibleNavEntriesAsync(AnonymousUser(), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("Kassenwart", result[0].Label);
    }

    [Fact]
    public async Task GetVisibleNavEntries_ExcludesFinanceNavEntry_WhenPermissionServiceDeniesKassenwartAccess()
    {
        var entry = new PluginNavEntry(
            "Kassenwart", "bi-cash-coin", "/finance", FinancePermissions.KassenwartAccess, 10);
        var registry = BuildRegistry(MakeRuntime("clubgear.plugin.finance", new[] { entry }));
        var service = new PluginNavEntryService(registry, new StubPermissionService(_ => false));

        var result = await service.GetVisibleNavEntriesAsync(AnonymousUser(), CancellationToken.None);

        Assert.Empty(result);
    }

    // ----------------------------------------------------------------
    // inner stubs
    // ----------------------------------------------------------------

    private sealed class StubPermissionService : IPermissionService
    {
        private readonly Func<string, bool> _predicate;

        public StubPermissionService(Func<string, bool> predicate)
        {
            _predicate = predicate;
        }

        public Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionKey, CancellationToken cancellationToken = default)
            => Task.FromResult(_predicate(permissionKey));
    }

    private sealed class NoOpPluginModule : IPluginModule
    {
        private readonly string _moduleId;

        public NoOpPluginModule(string moduleId)
        {
            _moduleId = moduleId;
        }

        public PluginManifest Manifest => new PluginManifest(
            _moduleId,
            _moduleId,
            new Version(1, 0, 0),
            "Test",
            "MIT",
            _moduleId,
            ">=1.0.0",
            Array.Empty<string>(),
            Array.Empty<string>());

        public void RegisterContributions(IPluginContributionSink sink) { }
    }
}
