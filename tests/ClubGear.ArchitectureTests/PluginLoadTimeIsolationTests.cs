using System.Reflection;
using ClubGear.ArchitectureTests.Fixtures;
using ClubGear.Data;
using ClubGear.Models.PluginAdmin;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins;
using ClubGear.Services.Plugins.Admin;
using ClubGear.Services.Plugins.Installation;
using ClubGear.Services.Plugins.Persistence;
using ClubGear.Services.Plugins.Runtime;
using ClubGear.Services.Plugins.Status;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// CORE-16. An isolation violation refuses the plugin at load time — before any plugin code runs —
/// instead of only when a route handler or capability delegate happens to be invoked.
/// </summary>
public sealed class PluginLoadTimeIsolationTests
{
    [Fact]
    public void ForbiddenNamespaces_AreReadableFromTests_WithoutReflection()
    {
        // Accessibility decision for CORE-16: PluginIsolationScanner stays internal and the test
        // assembly reads it through the InternalsVisibleTo entry ClubGear.csproj already declares.
        // Slice 2's source guardrail over plugins/** references exactly this list.
        IReadOnlyList<string> forbidden = PluginIsolationScanner.ForbiddenNamespaces;

        Assert.Equal(
            new[] { "ClubGear.Services", "ClubGear.Data", "ClubGear.Controllers", "ClubGear.Models" },
            forbidden);
    }

    [Fact]
    public void Scanner_RejectsConstructorParameter_EvenWhenItIsNeverStoredInAField()
    {
        var found = PluginIsolationScanner.TryFindViolation(
            typeof(ForbiddenPlugin.ForbiddenDbContextConstructorCapability),
            out var violation);

        Assert.True(found);
        Assert.Equal(typeof(ApplicationDbContext).FullName, violation);
    }

    [Fact]
    public void Scanner_DoesNotTrip_OnAllowedFixtures_OrOnTheReferencedPluginAssemblies()
    {
        // Exported types only, on purpose: the load-time gate sees the entry point and the declared
        // contribution types, both of which a plugin must make public. Compiler-generated async
        // state machines and closure classes are not declared contributions — they are reached only
        // as delegate targets by PluginRuntimeAdapter.EnsureIsolated, which is a different surface.
        var cleanTypes = typeof(Plugins.RuntimeLoadedPluginModuleA).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace is not null
                && type.Namespace.StartsWith("ClubGear.ArchitectureTests.Plugins", StringComparison.Ordinal))
            .Concat(PublicTypesOf(typeof(ClubGear.Plugin.CarInfo.CarInfoPluginModule).Assembly))
            .Concat(PublicTypesOf(typeof(ClubGear.Plugin.Inventar.InventarPluginModule).Assembly))
            .Concat(PublicTypesOf(typeof(ClubGear.Plugin.ServiceBook.ServiceBookPluginModule).Assembly))
            .Concat(PublicTypesOf(typeof(ClubGear.Plugin.Finance.FinancePluginModule).Assembly))
            .ToArray();

        Assert.NotEmpty(cleanTypes);

        var violations = cleanTypes
            .Where(type => PluginIsolationScanner.TryFindViolation(type, out _))
            .Select(type =>
            {
                PluginIsolationScanner.TryFindViolation(type, out var violation);
                return $"{type.FullName} -> {violation}";
            })
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "The load-time isolation scan reports a violation on code that is allowed: "
                + string.Join(", ", violations));
    }

    [Fact]
    public async Task Loader_RejectsForbiddenEntryPoint_BeforeRegisterContributionsRuns()
    {
        var markerPath = ClubGear.Services.ForbiddenPlugin.ForbiddenEntryPointPluginModule.RegisterContributionsMarkerPath;
        if (File.Exists(markerPath))
        {
            File.Delete(markerPath);
        }

        var packageStore = new FileSystemPluginPackageStore(
            Path.Combine(Path.GetTempPath(), "clubgear-plugin-loadtime-isolation-tests", Guid.NewGuid().ToString("N")));

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            packageStore,
            "plugin.forbidden.entrypoint",
            "Forbidden Entry Point Plugin",
            typeof(ClubGear.Services.ForbiddenPlugin.ForbiddenEntryPointPluginModule).FullName!);

        var loader = new PluginLoader(packageStore, NullLogger<PluginLoader>.Instance);
        var loadResult = await loader.LoadAsync(record);

        Assert.False(loadResult.Success);
        Assert.Null(loadResult.LoadedPlugin);
        Assert.Contains("plugin.forbidden.entrypoint", loadResult.Error, StringComparison.Ordinal);
        Assert.Contains(
            typeof(ClubGear.Services.ForbiddenPlugin.ForbiddenEntryPointPluginModule).FullName!,
            loadResult.Error!,
            StringComparison.Ordinal);
        Assert.False(File.Exists(markerPath), "RegisterContributions ran even though the entry point was refused.");
    }

    [Fact]
    public async Task Loader_RejectsContributionType_WithForbiddenConstructorParameter()
    {
        var packageStore = new FileSystemPluginPackageStore(
            Path.Combine(Path.GetTempPath(), "clubgear-plugin-loadtime-isolation-tests", Guid.NewGuid().ToString("N")));

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            packageStore,
            "plugin.forbidden.contribution",
            "Forbidden Contribution Plugin",
            typeof(ForbiddenPlugin.ForbiddenContributionPluginModule).FullName!);

        var loader = new PluginLoader(packageStore, NullLogger<PluginLoader>.Instance);
        var loadResult = await loader.LoadAsync(record);

        Assert.False(loadResult.Success);
        Assert.Contains("plugin.forbidden.contribution", loadResult.Error, StringComparison.Ordinal);
        Assert.Contains(
            typeof(ForbiddenPlugin.ForbiddenDbContextConstructorCapability).FullName!,
            loadResult.Error!,
            StringComparison.Ordinal);
        Assert.Contains(typeof(ApplicationDbContext).FullName!, loadResult.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Loader_RejectsContributionType_ThatIsNotInTheModuleAssembly()
    {
        var packageStore = new FileSystemPluginPackageStore(
            Path.Combine(Path.GetTempPath(), "clubgear-plugin-loadtime-isolation-tests", Guid.NewGuid().ToString("N")));

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            packageStore,
            "plugin.missing.contribution",
            "Missing Contribution Type Plugin",
            typeof(ForbiddenPlugin.MissingContributionTypePluginModule).FullName!);

        var loader = new PluginLoader(packageStore, NullLogger<PluginLoader>.Instance);
        var loadResult = await loader.LoadAsync(record);

        Assert.False(loadResult.Success);
        Assert.Contains("plugin.missing.contribution", loadResult.Error, StringComparison.Ordinal);
        Assert.Contains("TypeThatDoesNotExist", loadResult.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadActivated_KeepsActivatingTheOtherPlugins_WhenOneIsRefused()
    {
        await using var fixture = await CreateFixtureAsync();

        var forbidden = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.forbidden.entrypoint",
            "Forbidden Entry Point Plugin",
            typeof(ClubGear.Services.ForbiddenPlugin.ForbiddenEntryPointPluginModule).FullName!);
        await fixture.Store.UpsertAsync(forbidden);

        var allowed = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.a",
            "Runtime Plugin A",
            typeof(Plugins.RuntimeLoadedPluginModuleA).FullName!);
        await fixture.Store.UpsertAsync(allowed);

        var results = await fixture.LifecycleService.LoadActivatedAsync();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, result => !result.Success && result.Status == "load-failed");

        // The clean plugin is unaffected by its neighbour's rejection.
        Assert.NotNull(fixture.Registry.GetByModuleId("plugin.runtime.a"));
        Assert.Null(fixture.Registry.GetByModuleId("plugin.forbidden.entrypoint"));

        // The reason reaches the administrator: LastError is what Detail.cshtml and
        // _PluginStatusTable.cshtml render.
        var refused = fixture.QueryService.GetPluginStatus("plugin.forbidden.entrypoint");
        Assert.NotNull(refused);
        Assert.False(refused!.IsRuntimeRegistered);
        Assert.Contains("Core-Namensraum", refused.LastError, StringComparison.Ordinal);
        Assert.Contains(
            typeof(ClubGear.Services.ForbiddenPlugin.ForbiddenEntryPointPluginModule).FullName!,
            refused.LastError!,
            StringComparison.Ordinal);
    }

    private static IEnumerable<Type> PublicTypesOf(Assembly assembly)
        => assembly.GetExportedTypes();

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var store = new DbPluginStatusStore(dbContext);
        var registry = new PluginRegistry();
        var packageRootPath = Path.Combine(Path.GetTempPath(), "clubgear-plugin-loadtime-isolation", Guid.NewGuid().ToString("N"));
        var packageStore = new FileSystemPluginPackageStore(packageRootPath);
        var lifecycleService = new PluginLifecycleService(
            store,
            registry,
            new PluginEndpointRegistrar(new NoOpRuntimeAdapter(), registry),
            new PluginLoader(packageStore, NullLogger<PluginLoader>.Instance),
            new PluginMigrationRunner(dbContext, new PluginSchemaNamePolicy(), NullLogger<PluginMigrationRunner>.Instance),
            new NoOpPluginBackgroundJobRunner(),
            NullLogger<PluginLifecycleService>.Instance);
        var queryService = new PluginAdminQueryService(
            store,
            registry,
            new ContractCompatibilityService(),
            dbContext,
            new NoOpPluginBackgroundJobRunner());

        return new Fixture(connection, dbContext, store, registry, packageStore, packageRootPath, lifecycleService, queryService);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(
            SqliteConnection connection,
            ApplicationDbContext dbContext,
            DbPluginStatusStore store,
            PluginRegistry registry,
            FileSystemPluginPackageStore packageStore,
            string packageRootPath,
            PluginLifecycleService lifecycleService,
            PluginAdminQueryService queryService)
        {
            Connection = connection;
            DbContext = dbContext;
            Store = store;
            Registry = registry;
            PackageStore = packageStore;
            PackageRootPath = packageRootPath;
            LifecycleService = lifecycleService;
            QueryService = queryService;
        }

        public SqliteConnection Connection { get; }

        public ApplicationDbContext DbContext { get; }

        public DbPluginStatusStore Store { get; }

        public PluginRegistry Registry { get; }

        public FileSystemPluginPackageStore PackageStore { get; }

        public string PackageRootPath { get; }

        public PluginLifecycleService LifecycleService { get; }

        public PluginAdminQueryService QueryService { get; }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();

            if (Directory.Exists(PackageRootPath))
            {
                Directory.Delete(PackageRootPath, recursive: true);
            }
        }
    }

    private sealed class NoOpRuntimeAdapter : IPluginRuntimeAdapter
    {
        public IPluginRuntimeBridge CreateBridge(Plugin.Contracts.IPluginModule pluginModule, System.Security.Claims.ClaimsPrincipal user)
            => throw new NotSupportedException();

        public Task<TResult> InvokeAsync<TResult>(Plugin.Contracts.IPluginModule pluginModule, System.Security.Claims.ClaimsPrincipal user, Func<IPluginRuntimeBridge, CancellationToken, Task<TResult>> capability, string? requiredPermissionKey = null, Delegate? isolatedDelegate = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RunAsync(Plugin.Contracts.IPluginModule pluginModule, System.Security.Claims.ClaimsPrincipal user, Func<IPluginRuntimeBridge, CancellationToken, Task> capability, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void EnsureIsolated(Delegate pluginDelegate)
        {
        }
    }

    private sealed class NoOpPluginBackgroundJobRunner : IPluginBackgroundJobRunner
    {
        public Task StartJobsForModuleAsync(string moduleId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopJobsForModuleAsync(string moduleId)
            => Task.CompletedTask;

        public IReadOnlyList<PluginJobStatus> GetJobStatuses(string moduleId)
            => Array.Empty<PluginJobStatus>();
    }
}
