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

public sealed class PluginAdminQueryServiceAuditSinkTests
{
    /// <summary>
    /// PluginAdminQueryService.CreateStatus populates AuditSinkCount
    /// from the registered plugin runtime's AuditSinks collection.
    /// </summary>
    [Fact]
    public async Task PluginAdminQueryService_IncludesAuditSinkCount()
    {
        await using var fixture = await CreateFixtureAsync();

        RegisterRuntime(fixture.Registry, new AuditSinkContributingPluginModule());

        var result = fixture.Service.GetPluginStatuses();

        var status = Assert.Single(result);
        Assert.Equal("plugin.auditsink.test", status.ModuleId);
        Assert.Equal(2, status.AuditSinkCount);
    }

    private static void RegisterRuntime(PluginRegistry registry, IPluginModule module)
    {
        var auditSinks = new[]
        {
            new PluginAuditSinkContribution("Provider.One"),
            new PluginAuditSinkContribution("Provider.Two")
        };

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
            auditSinks,
            Array.Empty<PluginIdentityProviderContribution>(),
            Array.Empty<PluginSelfServiceProfileProviderContribution>());

        registry.Register(runtime, module, AssemblyLoadContext.GetLoadContext(module.GetType().Assembly)!);
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
        var compatibility = new AlwaysCompatibleService();
        var service = new PluginAdminQueryService(store, registry, compatibility, dbContext, new NoOpPluginBackgroundJobRunner());

        return new Fixture(connection, dbContext, store, registry, service);
    }

    private sealed class NoOpPluginBackgroundJobRunner : IPluginBackgroundJobRunner
    {
        public Task StartJobsForModuleAsync(string moduleId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopJobsForModuleAsync(string moduleId) => Task.CompletedTask;

        public IReadOnlyList<PluginJobStatus> GetJobStatuses(string moduleId)
            => Array.Empty<PluginJobStatus>();
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
    /// A minimal plugin module used to anchor the runtime to a real assembly load context.
    /// </summary>
    private sealed class AuditSinkContributingPluginModule : IPluginModule
    {
        public AuditSinkContributingPluginModule()
        {
            Manifest = new PluginManifest(
                "plugin.auditsink.test",
                "AuditSink Test Plugin",
                new Version(1, 0, 0),
                "Test",
                "MIT",
                typeof(AuditSinkContributingPluginModule).FullName!,
                ">=1.0.0",
                [],
                []);
        }

        public PluginManifest Manifest { get; }

        public void RegisterContributions(IPluginContributionSink sink)
        {
            // Audit sinks are injected directly into RegisteredPluginRuntime by the test;
            // no contributions registered here.
        }
    }
}
