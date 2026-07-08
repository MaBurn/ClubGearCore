using ClubGear.ArchitectureTests.Fixtures;
using ClubGear.Data;
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

public sealed class PluginActivationMigrationTests
{
    [Fact]
    public async Task ActivateAsync_AppliesPendingPluginMigration_AndRegistersRuntime()
    {
        await using var fixture = await CreateLifecycleFixtureAsync();
        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.migrating",
            "Runtime Plugin With Migration",
            typeof(Plugins.MigratingPluginModule).FullName!,
            isActive: false);

        await fixture.Store.UpsertAsync(record);

        var registry = new PluginRegistry();
        var lifecycleService = new PluginLifecycleService(
            fixture.Store,
            registry,
            new PluginEndpointRegistrar(new NoOpRuntimeAdapter(), registry),
            new PluginLoader(fixture.PackageStore, NullLogger<PluginLoader>.Instance),
            new PluginMigrationRunner(fixture.DbContext, new PluginSchemaNamePolicy(), NullLogger<PluginMigrationRunner>.Instance),
            new NoOpPluginBackgroundJobRunner(),
            NullLogger<PluginLifecycleService>.Instance);

        var result = await lifecycleService.ActivateAsync("plugin.runtime.migrating");

        Assert.True(result.Success);
        Assert.Equal("activated", result.Status);
        Assert.NotNull(registry.GetByModuleId("plugin.runtime.migrating"));

        var states = await fixture.DbContext.PluginMigrationStates
            .AsNoTracking()
            .Where(state => state.PluginKey == "plugin.runtime.migrating")
            .ToListAsync();

        Assert.Single(states);
        Assert.Equal("001_create_notes", states[0].MigrationId);

        var tableExists = await TableExistsAsync(fixture.DbContext, "plugin_plugin_runtime_migrating_notes");
        Assert.True(tableExists);
    }

    [Fact]
    public async Task LoadActivatedAsync_DeactivatesOnlyFailingPlugin_WhenMigrationBreaks()
    {
        await using var fixture = await CreateLifecycleFixtureAsync();

        var validRecord = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.migrating",
            "Runtime Plugin With Migration",
            typeof(Plugins.MigratingPluginModule).FullName!);
        var failingRecord = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.badmigration",
            "Runtime Plugin With Bad Migration",
            typeof(Plugins.BadMigratingPluginModule).FullName!);

        await fixture.Store.UpsertAsync(validRecord);
        await fixture.Store.UpsertAsync(failingRecord);

        var registry = new PluginRegistry();
        var lifecycleService = new PluginLifecycleService(
            fixture.Store,
            registry,
            new PluginEndpointRegistrar(new NoOpRuntimeAdapter(), registry),
            new PluginLoader(fixture.PackageStore, NullLogger<PluginLoader>.Instance),
            new PluginMigrationRunner(fixture.DbContext, new PluginSchemaNamePolicy(), NullLogger<PluginMigrationRunner>.Instance),
            new NoOpPluginBackgroundJobRunner(),
            NullLogger<PluginLifecycleService>.Instance);

        var results = await lifecycleService.LoadActivatedAsync();

        Assert.Contains(results, candidate => candidate.Success && candidate.Plugin?.ModuleId == "plugin.runtime.migrating");
        Assert.Contains(results, candidate => !candidate.Success && candidate.Status == "migration-failed" && candidate.Plugin?.ModuleId == "plugin.runtime.badmigration");
        Assert.NotNull(registry.GetByModuleId("plugin.runtime.migrating"));
        Assert.Null(registry.GetByModuleId("plugin.runtime.badmigration"));

        var failedRecord = fixture.Store.GetByKey("plugin.runtime.badmigration");
        Assert.NotNull(failedRecord);
        Assert.False(failedRecord!.IsActive);
        Assert.Contains("Praefix", failedRecord.LastError, StringComparison.Ordinal);
    }

    private static async Task<bool> TableExistsAsync(ApplicationDbContext dbContext, string tableName)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        if (command.Connection!.State != System.Data.ConnectionState.Open)
        {
            await command.Connection.OpenAsync();
        }

        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) == 1;
    }

    private static async Task<LifecycleFixture> CreateLifecycleFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var packageStore = new FileSystemPluginPackageStore(
            Path.Combine(Path.GetTempPath(), "clubgear-plugin-migration-tests", Guid.NewGuid().ToString("N")));

        return new LifecycleFixture(connection, dbContext, new DbPluginStatusStore(dbContext), packageStore);
    }

    private sealed class NoOpRuntimeAdapter : IPluginRuntimeAdapter
    {
        public IPluginRuntimeBridge CreateBridge(Plugin.Contracts.IPluginModule pluginModule, System.Security.Claims.ClaimsPrincipal user)
            => throw new NotSupportedException();

        public Task<TResult> InvokeAsync<TResult>(
            Plugin.Contracts.IPluginModule pluginModule,
            System.Security.Claims.ClaimsPrincipal user,
            Func<IPluginRuntimeBridge, CancellationToken, Task<TResult>> capability,
            string? requiredPermissionKey = null,
            Delegate? isolatedDelegate = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RunAsync(
            Plugin.Contracts.IPluginModule pluginModule,
            System.Security.Claims.ClaimsPrincipal user,
            Func<IPluginRuntimeBridge, CancellationToken, Task> capability,
            CancellationToken cancellationToken = default)
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

    private sealed class LifecycleFixture : IAsyncDisposable
    {
        public LifecycleFixture(
            SqliteConnection connection,
            ApplicationDbContext dbContext,
            DbPluginStatusStore store,
            FileSystemPluginPackageStore packageStore)
        {
            Connection = connection;
            DbContext = dbContext;
            Store = store;
            PackageStore = packageStore;
        }

        public SqliteConnection Connection { get; }

        public ApplicationDbContext DbContext { get; }

        public DbPluginStatusStore Store { get; }

        public FileSystemPluginPackageStore PackageStore { get; }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}