using System.Runtime.Loader;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Models.PluginAdmin;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Admin;
using ClubGear.Services.Plugins.Runtime;
using ClubGear.Services.Plugins.Status;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginAdminQueryServiceBackgroundJobTests
{
    /// <summary>
    /// PluginAdminQueryService.CreateStatus populates BackgroundJobRunningCount
    /// by counting job statuses with State == Running for the given module.
    /// </summary>
    [Fact]
    public async Task PluginAdminQueryService_IncludesBackgroundJobRunningCount()
    {
        const string moduleId = "plugin.bgjob.test";

        await using var fixture = await CreateFixtureAsync(new RecordingJobRunner(moduleId,
        [
            new PluginJobStatus(moduleId, "job.a", "JobTypeA", PluginJobRunState.Running, DateTimeOffset.UtcNow, null),
            new PluginJobStatus(moduleId, "job.b", "JobTypeB", PluginJobRunState.Running, DateTimeOffset.UtcNow, null),
            new PluginJobStatus(moduleId, "job.c", "JobTypeC", PluginJobRunState.Completed, DateTimeOffset.UtcNow, null),
            new PluginJobStatus(moduleId, "job.d", "JobTypeD", PluginJobRunState.Faulted, null, "Some error"),
        ]));

        RegisterRuntime(fixture.Registry, new BackgroundJobTestPluginModule());

        var result = fixture.Service.GetPluginStatuses();

        var status = Assert.Single(result);
        Assert.Equal(moduleId, status.ModuleId);
        Assert.Equal(2, status.BackgroundJobRunningCount);
    }

    /// <summary>
    /// When no jobs are running, BackgroundJobRunningCount is 0.
    /// </summary>
    [Fact]
    public async Task PluginAdminQueryService_BackgroundJobRunningCount_IsZero_WhenNoRunningJobs()
    {
        const string moduleId = "plugin.bgjob.test";

        await using var fixture = await CreateFixtureAsync(new RecordingJobRunner(moduleId,
        [
            new PluginJobStatus(moduleId, "job.a", "JobTypeA", PluginJobRunState.Completed, DateTimeOffset.UtcNow, null),
            new PluginJobStatus(moduleId, "job.b", "JobTypeB", PluginJobRunState.Stopped, null, null),
        ]));

        RegisterRuntime(fixture.Registry, new BackgroundJobTestPluginModule());

        var result = fixture.Service.GetPluginStatuses();

        var status = Assert.Single(result);
        Assert.Equal(0, status.BackgroundJobRunningCount);
    }

    /// <summary>
    /// When no runtime is registered for a plugin, BackgroundJobRunningCount is 0.
    /// </summary>
    [Fact]
    public async Task PluginAdminQueryService_BackgroundJobRunningCount_IsZero_WhenNoRuntime()
    {
        const string moduleId = "plugin.installed.only";

        await using var fixture = await CreateFixtureAsync(new RecordingJobRunner(moduleId, []));

        await fixture.Store.UpsertAsync(new PluginStatusRecord
        {
            Key = moduleId,
            DisplayName = "Installed Only Plugin",
            Version = "1.0.0",
            Author = "Test",
            License = "MIT",
            EntryPoint = "Some.Plugin",
            RequiredCoreVersion = ">=1.0.0",
            InstallSource = "zip",
            PackageHash = "HASH",
            PackagePath = "/tmp/installed-only.zip",
            IsActive = false,
            Category = "General",
            PermissionsJson = "[]",
            ExtensionPointsJson = "[]",
            InstalledAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        var result = fixture.Service.GetPluginStatuses();

        var status = Assert.Single(result);
        Assert.Equal(0, status.BackgroundJobRunningCount);
    }

    private static void RegisterRuntime(PluginRegistry registry, IPluginModule module)
    {
        var runtime = new RegisteredPluginRuntime(
            module.Manifest.ModuleId,
            module.Manifest.DisplayName,
            module.Manifest.PluginVersion,
            $"test:{module.Manifest.ModuleId}",
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

    private static async Task<Fixture> CreateFixtureAsync(IPluginBackgroundJobRunner jobRunner)
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
        var compatibility = new AlwaysCompatibleService();
        var service = new PluginAdminQueryService(store, registry, compatibility, dbContext, jobRunner);

        return new Fixture(connection, dbContext, store, registry, service);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(
            SqliteConnection connection,
            ApplicationDbContext dbContext,
            DbPluginStatusStore store,
            PluginRegistry registry,
            PluginAdminQueryService service)
        {
            Connection = connection;
            DbContext = dbContext;
            Store = store;
            Registry = registry;
            Service = service;
        }

        public SqliteConnection Connection { get; }
        public ApplicationDbContext DbContext { get; }
        public DbPluginStatusStore Store { get; }
        public PluginRegistry Registry { get; }
        public PluginAdminQueryService Service { get; }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class AlwaysCompatibleService : IContractCompatibilityService
    {
        public ContractCompatibilityResult Validate(Version pluginContractVersion)
            => new ContractCompatibilityResult(true);
    }

    /// <summary>
    /// A stub that returns a pre-configured list of job statuses for a specific module.
    /// </summary>
    private sealed class RecordingJobRunner : IPluginBackgroundJobRunner
    {
        private readonly string _moduleId;
        private readonly IReadOnlyList<PluginJobStatus> _statuses;

        public RecordingJobRunner(string moduleId, IReadOnlyList<PluginJobStatus> statuses)
        {
            _moduleId = moduleId;
            _statuses = statuses;
        }

        public Task StartJobsForModuleAsync(string moduleId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopJobsForModuleAsync(string moduleId) => Task.CompletedTask;

        public IReadOnlyList<PluginJobStatus> GetJobStatuses(string moduleId)
            => string.Equals(moduleId, _moduleId, StringComparison.OrdinalIgnoreCase)
                ? _statuses
                : Array.Empty<PluginJobStatus>();
    }

    /// <summary>
    /// A minimal plugin module for testing background job count surfacing.
    /// </summary>
    private sealed class BackgroundJobTestPluginModule : IPluginModule
    {
        public BackgroundJobTestPluginModule()
        {
            Manifest = new PluginManifest(
                "plugin.bgjob.test",
                "Background Job Test Plugin",
                new Version(1, 0, 0),
                "Test",
                "MIT",
                typeof(BackgroundJobTestPluginModule).FullName!,
                ">=1.0.0",
                [],
                []);
        }

        public PluginManifest Manifest { get; }

        public void RegisterContributions(IPluginContributionSink sink) { }
    }
}
