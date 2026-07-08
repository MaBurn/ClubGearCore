using System.Runtime.Loader;
using ClubGear.ArchitectureTests.Fixtures;
using ClubGear.Data;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
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
/// Verifies Slice 4's activation dependency guard: PluginLifecycleService.ActivateAsync must
/// consult the stored DependenciesJson and refuse to activate a plugin whose declared
/// dependencies are not satisfied by the runtime registry.
/// </summary>
public sealed class PluginActivationDependencyGuardTests
{
    [Fact]
    public async Task ActivateAsync_NoDependencies_ActivatesNormally()
    {
        await using var fixture = await CreateFixtureAsync();

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.a",
            "Dependent Plugin (no deps)",
            typeof(Plugins.RuntimeLoadedPluginModuleA).FullName!,
            isActive: false);
        record.DependenciesJson = "[]";
        await fixture.Store.UpsertAsync(record);

        var lifecycleService = BuildLifecycleService(fixture);

        var result = await lifecycleService.ActivateAsync("plugin.runtime.a");

        Assert.True(result.Success, $"Expected activation success but got [{result.Status}] {result.Message}");
        Assert.Equal("activated", result.Status);
    }

    [Fact]
    public async Task ActivateAsync_DependencyPresentAtExactMinVersion_ActivatesNormally()
    {
        await using var fixture = await CreateFixtureAsync();

        RegisterDependencyRuntime(fixture.Registry, "plugin.dependency.exact", new Version(1, 0, 5));

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.a",
            "Dependent Plugin (exact version)",
            typeof(Plugins.RuntimeLoadedPluginModuleA).FullName!,
            isActive: false);
        record.DependenciesJson = """["plugin.dependency.exact@>=1.0.5"]""";
        await fixture.Store.UpsertAsync(record);

        var lifecycleService = BuildLifecycleService(fixture);

        var result = await lifecycleService.ActivateAsync("plugin.runtime.a");

        Assert.True(result.Success, $"Expected activation success but got [{result.Status}] {result.Message}");
        Assert.Equal("activated", result.Status);
    }

    [Fact]
    public async Task ActivateAsync_DependencyAbsentFromRegistry_ReturnsDependencyNotMet()
    {
        await using var fixture = await CreateFixtureAsync();

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.a",
            "Dependent Plugin (missing dependency)",
            typeof(Plugins.RuntimeLoadedPluginModuleA).FullName!,
            isActive: false);
        record.DependenciesJson = """["plugin.missing@>=1.0.0"]""";
        await fixture.Store.UpsertAsync(record);

        var lifecycleService = BuildLifecycleService(fixture);

        var result = await lifecycleService.ActivateAsync("plugin.runtime.a");

        Assert.False(result.Success);
        Assert.Equal("dependency-not-met", result.Status);
        Assert.Contains("plugin.missing", result.Message);
    }

    [Fact]
    public async Task ActivateAsync_DependencyBelowMinVersion_ReturnsDependencyNotMet()
    {
        await using var fixture = await CreateFixtureAsync();

        RegisterDependencyRuntime(fixture.Registry, "plugin.dependency.low", new Version(1, 0, 4));

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.a",
            "Dependent Plugin (version too low)",
            typeof(Plugins.RuntimeLoadedPluginModuleA).FullName!,
            isActive: false);
        record.DependenciesJson = """["plugin.dependency.low@>=1.0.5"]""";
        await fixture.Store.UpsertAsync(record);

        var lifecycleService = BuildLifecycleService(fixture);

        var result = await lifecycleService.ActivateAsync("plugin.runtime.a");

        Assert.False(result.Success);
        Assert.Equal("dependency-not-met", result.Status);
        Assert.Contains("1.0.4", result.Message);
        Assert.Contains("1.0.5", result.Message);
    }

    // -----------------------------------------------------------------------
    // helpers
    // -----------------------------------------------------------------------

    private static void RegisterDependencyRuntime(PluginRegistry registry, string moduleId, Version version)
    {
        var module = new Plugins.RuntimeLoadedPluginModuleA();
        var runtime = new RegisteredPluginRuntime(
            moduleId,
            moduleId,
            version,
            $"test:{moduleId}",
            Array.Empty<PluginRouteContribution>(),
            Array.Empty<PluginServiceContribution>(),
            Array.Empty<PluginMemberProviderContribution>(),
            Array.Empty<PluginBackgroundJobContribution>(),
            Array.Empty<PluginNavEntry>(),
            Array.Empty<PluginAuditSinkContribution>(),
            Array.Empty<PluginIdentityProviderContribution>(),
            Array.Empty<PluginSelfServiceProfileProviderContribution>());

        registry.Register(runtime, module, AssemblyLoadContext.GetLoadContext(module.GetType().Assembly)!);
    }

    private static PluginLifecycleService BuildLifecycleService(Fixture fixture)
    {
        return new PluginLifecycleService(
            fixture.Store,
            fixture.Registry,
            new PluginEndpointRegistrar(new NoOpRuntimeAdapter(), fixture.Registry),
            new PluginLoader(fixture.PackageStore, NullLogger<PluginLoader>.Instance),
            new PluginMigrationRunner(fixture.DbContext, new PluginSchemaNamePolicy(), NullLogger<PluginMigrationRunner>.Instance),
            new NoOpJobRunner(),
            NullLogger<PluginLifecycleService>.Instance);
    }

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
        var packageRootPath = Path.Combine(Path.GetTempPath(), "clubgear-dependency-guard", Guid.NewGuid().ToString("N"));
        var packageStore = new FileSystemPluginPackageStore(packageRootPath);

        return new Fixture(connection, dbContext, store, registry, packageStore, packageRootPath);
    }

    private sealed class NoOpRuntimeAdapter : IPluginRuntimeAdapter
    {
        public IPluginRuntimeBridge CreateBridge(IPluginModule pluginModule, System.Security.Claims.ClaimsPrincipal user)
            => throw new NotSupportedException();

        public Task<TResult> InvokeAsync<TResult>(IPluginModule pluginModule, System.Security.Claims.ClaimsPrincipal user, Func<IPluginRuntimeBridge, CancellationToken, Task<TResult>> capability, string? requiredPermissionKey = null, Delegate? isolatedDelegate = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RunAsync(IPluginModule pluginModule, System.Security.Claims.ClaimsPrincipal user, Func<IPluginRuntimeBridge, CancellationToken, Task> capability, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void EnsureIsolated(Delegate pluginDelegate) { }
    }

    private sealed class NoOpJobRunner : IPluginBackgroundJobRunner
    {
        public Task StartJobsForModuleAsync(string moduleId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopJobsForModuleAsync(string moduleId) => Task.CompletedTask;

        public IReadOnlyList<PluginJobStatus> GetJobStatuses(string moduleId) => Array.Empty<PluginJobStatus>();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _packageRootPath;

        public Fixture(
            SqliteConnection connection,
            ApplicationDbContext dbContext,
            DbPluginStatusStore store,
            PluginRegistry registry,
            FileSystemPluginPackageStore packageStore,
            string packageRootPath)
        {
            Connection = connection;
            DbContext = dbContext;
            Store = store;
            Registry = registry;
            PackageStore = packageStore;
            _packageRootPath = packageRootPath;
        }

        public SqliteConnection Connection { get; }
        public ApplicationDbContext DbContext { get; }
        public DbPluginStatusStore Store { get; }
        public PluginRegistry Registry { get; }
        public FileSystemPluginPackageStore PackageStore { get; }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();

            if (Directory.Exists(_packageRootPath))
            {
                Directory.Delete(_packageRootPath, recursive: true);
            }
        }
    }
}
