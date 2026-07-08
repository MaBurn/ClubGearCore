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
/// Verifies call ordering requirements for Slice 4:
/// - On activation: Register must be called BEFORE StartJobsForModuleAsync.
/// - On deactivation: StopJobsForModuleAsync must be called BEFORE Unregister.
/// </summary>
public sealed class PluginLifecycleServiceOrderingTests
{
    // -----------------------------------------------------------------------
    // Test 1: On activation, Register is called before StartJobsForModuleAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Activation_Register_IsCalledBeforeStartJobsForModuleAsync()
    {
        // Arrange
        await using var fixture = await CreateFixtureAsync();

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.a",
            "Runtime Plugin A",
            typeof(Plugins.RuntimeLoadedPluginModuleA).FullName!,
            isActive: false);
        await fixture.Store.UpsertAsync(record);

        var callOrder = new List<string>();
        var registry = new RecordingPluginRegistry(callOrder);
        var jobRunner = new RecordingJobRunner(callOrder);

        var lifecycleService = BuildLifecycleService(fixture, registry, jobRunner);

        // Act
        var result = await lifecycleService.ActivateAsync("plugin.runtime.a");

        // Assert — activation must succeed for ordering to be meaningful
        Assert.True(result.Success, $"Activation failed unexpectedly: [{result.Status}] {result.Message}");

        var registerIndex = callOrder.IndexOf("Register");
        var startIndex = callOrder.IndexOf("StartJobsForModuleAsync");

        Assert.True(registerIndex >= 0, "Register was never called.");
        Assert.True(startIndex >= 0, "StartJobsForModuleAsync was never called.");
        Assert.True(registerIndex < startIndex,
            $"Expected Register (pos {registerIndex}) before StartJobsForModuleAsync (pos {startIndex}), but order was: [{string.Join(", ", callOrder)}]");
    }

    // -----------------------------------------------------------------------
    // Test 2: On deactivation, StopJobsForModuleAsync is called before Unregister
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Deactivation_StopJobsForModuleAsync_IsCalledBeforeUnregister()
    {
        // Arrange — activate with a recording registry/runner so we have a live runtime
        await using var fixture = await CreateFixtureAsync();

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.a",
            "Runtime Plugin A",
            typeof(Plugins.RuntimeLoadedPluginModuleA).FullName!,
            isActive: false);
        await fixture.Store.UpsertAsync(record);

        var callOrder = new List<string>();
        var registry = new RecordingPluginRegistry(callOrder);
        var jobRunner = new RecordingJobRunner(callOrder);

        var lifecycleService = BuildLifecycleService(fixture, registry, jobRunner);

        // Activate first so there is a registered runtime to deactivate
        var activateResult = await lifecycleService.ActivateAsync("plugin.runtime.a");
        Assert.True(activateResult.Success, $"Activation failed unexpectedly: [{activateResult.Status}] {activateResult.Message}");
        Assert.True(registry.GetByModuleId("plugin.runtime.a") is not null, "Plugin should be registered after activation.");

        // Reset the call log; now only record deactivation calls
        callOrder.Clear();

        // Act
        var deactivateResult = await lifecycleService.DeactivateAsync("plugin.runtime.a");
        Assert.True(deactivateResult.Success, $"Deactivation failed unexpectedly: [{deactivateResult.Status}] {deactivateResult.Message}");

        // Assert — StopJobsForModuleAsync must appear before Unregister
        var stopIndex = callOrder.IndexOf("StopJobsForModuleAsync");
        var unregisterIndex = callOrder.IndexOf("Unregister");

        Assert.True(stopIndex >= 0, "StopJobsForModuleAsync was never called.");
        Assert.True(unregisterIndex >= 0, "Unregister was never called.");
        Assert.True(stopIndex < unregisterIndex,
            $"Expected StopJobsForModuleAsync (pos {stopIndex}) before Unregister (pos {unregisterIndex}), but order was: [{string.Join(", ", callOrder)}]");
    }

    // -----------------------------------------------------------------------
    // Test 3: On activation failure (migration fails), StartJobsForModuleAsync is NOT called
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ActivationFailure_StartJobsForModuleAsync_IsNotCalled()
    {
        // Arrange
        await using var fixture = await CreateFixtureAsync();

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.badmigration",
            "Runtime Plugin With Bad Migration",
            typeof(Plugins.BadMigratingPluginModule).FullName!,
            isActive: false);
        await fixture.Store.UpsertAsync(record);

        var callOrder = new List<string>();
        var registry = new RecordingPluginRegistry(callOrder);
        var jobRunner = new RecordingJobRunner(callOrder);

        var lifecycleService = BuildLifecycleService(fixture, registry, jobRunner);

        // Act — activation fails at migration step
        var result = await lifecycleService.ActivateAsync("plugin.runtime.badmigration");

        // Assert — activation failed; StartJobsForModuleAsync must not have been called
        Assert.False(result.Success);
        Assert.DoesNotContain("StartJobsForModuleAsync", callOrder);
    }

    // -----------------------------------------------------------------------
    // helpers
    // -----------------------------------------------------------------------

    private static PluginLifecycleService BuildLifecycleService(
        Fixture fixture,
        IPluginRuntimeRegistry registry,
        IPluginBackgroundJobRunner jobRunner)
    {
        return new PluginLifecycleService(
            fixture.Store,
            registry,
            new PluginEndpointRegistrar(new NoOpRuntimeAdapter(), registry),
            new PluginLoader(fixture.PackageStore, NullLogger<PluginLoader>.Instance),
            new PluginMigrationRunner(fixture.DbContext, new PluginSchemaNamePolicy(), NullLogger<PluginMigrationRunner>.Instance),
            jobRunner,
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
        var packageRootPath = Path.Combine(Path.GetTempPath(), "clubgear-lifecycle-order", Guid.NewGuid().ToString("N"));
        var packageStore = new FileSystemPluginPackageStore(packageRootPath);

        return new Fixture(connection, dbContext, store, packageStore, packageRootPath);
    }

    // -----------------------------------------------------------------------
    // Recording mocks
    // -----------------------------------------------------------------------

    /// <summary>
    /// A registry that records "Register" and "Unregister" calls into a shared log,
    /// delegating all work to an inner PluginRegistry.
    /// </summary>
    private sealed class RecordingPluginRegistry : IPluginRuntimeRegistry
    {
        private readonly List<string> _log;
        private readonly PluginRegistry _inner = new();

        public RecordingPluginRegistry(List<string> log) => _log = log;

        public IReadOnlyList<RegisteredPluginRuntime> GetRegisteredPlugins() => _inner.GetRegisteredPlugins();

        public RegisteredPluginRuntime? GetByModuleId(string moduleId) => _inner.GetByModuleId(moduleId);

        public IPluginModule? GetModule(string moduleId) => _inner.GetModule(moduleId);

        public TProvider? CreateMemberProvider<TProvider>(string moduleId, string providerType) where TProvider : class
            => _inner.CreateMemberProvider<TProvider>(moduleId, providerType);

        public void AddOrReplaceRoute(string moduleId, PluginRouteContribution route)
            => _inner.AddOrReplaceRoute(moduleId, route);

        public void Register(RegisteredPluginRuntime runtime, IPluginModule module, AssemblyLoadContext loadContext)
        {
            _log.Add("Register");
            _inner.Register(runtime, module, loadContext);
        }

        public bool Unregister(string moduleId)
        {
            _log.Add("Unregister");
            return _inner.Unregister(moduleId);
        }
    }

    /// <summary>
    /// Records StartJobsForModuleAsync and StopJobsForModuleAsync calls into a shared log.
    /// </summary>
    private sealed class RecordingJobRunner : IPluginBackgroundJobRunner
    {
        private readonly List<string> _log;

        public RecordingJobRunner(List<string> log) => _log = log;

        public Task StartJobsForModuleAsync(string moduleId, CancellationToken cancellationToken = default)
        {
            _log.Add("StartJobsForModuleAsync");
            return Task.CompletedTask;
        }

        public Task StopJobsForModuleAsync(string moduleId)
        {
            _log.Add("StopJobsForModuleAsync");
            return Task.CompletedTask;
        }

        public IReadOnlyList<PluginJobStatus> GetJobStatuses(string moduleId)
            => Array.Empty<PluginJobStatus>();
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

    // -----------------------------------------------------------------------
    // Fixture
    // -----------------------------------------------------------------------

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _packageRootPath;

        public Fixture(
            SqliteConnection connection,
            ApplicationDbContext dbContext,
            DbPluginStatusStore store,
            FileSystemPluginPackageStore packageStore,
            string packageRootPath)
        {
            Connection = connection;
            DbContext = dbContext;
            Store = store;
            PackageStore = packageStore;
            _packageRootPath = packageRootPath;
        }

        public SqliteConnection Connection { get; }
        public ApplicationDbContext DbContext { get; }
        public DbPluginStatusStore Store { get; }
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
