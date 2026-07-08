using System.Runtime.Loader;
using System.Text.Json;
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

public sealed class PluginAdminNavEntryTests
{
    /// <summary>
    /// PluginAdminStatusViewModel exposes a NavEntryCount property
    /// that holds the number of registered nav entries.
    /// </summary>
    [Fact]
    public void PluginAdminStatusViewModel_HasNavEntryCount()
    {
        var vm = new PluginAdminStatusViewModel(
            "plugin.test",
            "Test Plugin",
            new Version(1, 0, 0),
            "zip",
            DateTimeOffset.UtcNow,
            "Author",
            "MIT",
            ">=1.0.0",
            true,
            true,
            true,
            true,
            null,
            null,
            ["perm.read"],
            ["nav.main"],
            1, 0, 0, 0,
            null,
            "General",
            "HASH",
            0,
            NavEntryCount: 3,
            AuditSinkCount: 0,
            BackgroundJobRunningCount: 0);

        Assert.Equal(3, vm.NavEntryCount);
    }

    /// <summary>
    /// PluginAdminQueryService populates NavEntryCount by counting
    /// NavEntries from the registered plugin runtime.
    /// </summary>
    [Fact]
    public async Task PluginAdminQueryService_IncludesNavEntryCount()
    {
        await using var fixture = await CreateFixtureAsync();

        await fixture.Store.UpsertAsync(new PluginStatusRecord
        {
            Key = "plugin.nav.test",
            DisplayName = "Nav Test Plugin",
            Version = "1.0.0",
            Author = "Test",
            License = "MIT",
            EntryPoint = typeof(NavContributingPluginModule).FullName!,
            RequiredCoreVersion = ">=1.0.0",
            InstallSource = "zip",
            PackageHash = "NAV123",
            PackagePath = "/tmp/nav-test.zip",
            IsActive = true,
            Category = "General",
            PermissionsJson = JsonSerializer.Serialize(new[] { "nav.items.read" }),
            ExtensionPointsJson = JsonSerializer.Serialize(new[] { "nav.main" }),
            InstalledAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        RegisterRuntime(fixture.Registry, new NavContributingPluginModule());

        var result = fixture.Service.GetPluginStatuses();

        var status = Assert.Single(result);
        Assert.Equal("plugin.nav.test", status.ModuleId);
        Assert.Equal(2, status.NavEntryCount);
    }

    /// <summary>
    /// When no runtime is registered for a plugin, NavEntryCount is 0.
    /// </summary>
    [Fact]
    public async Task PluginAdminQueryService_NavEntryCount_IsZero_WhenNoRuntime()
    {
        await using var fixture = await CreateFixtureAsync();

        await fixture.Store.UpsertAsync(new PluginStatusRecord
        {
            Key = "plugin.installed.only",
            DisplayName = "Installed Only Plugin",
            Version = "1.0.0",
            Author = "Test",
            License = "MIT",
            EntryPoint = "Some.Plugin",
            RequiredCoreVersion = ">=1.0.0",
            InstallSource = "zip",
            PackageHash = "ONLYINSTALLED",
            PackagePath = "/tmp/installed-only.zip",
            IsActive = false,
            Category = "General",
            PermissionsJson = "[]",
            ExtensionPointsJson = "[]",
            InstalledAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        // No runtime registered — plugin appears as installed but not active.
        var result = fixture.Service.GetPluginStatuses();

        var status = Assert.Single(result);
        Assert.Equal(0, status.NavEntryCount);
    }

    private static void RegisterRuntime(PluginRegistry registry, IPluginModule module)
    {
        var sink = new RecordingContributionSink();
        module.RegisterContributions(sink);

        var runtime = new RegisteredPluginRuntime(
            module.Manifest.ModuleId,
            module.Manifest.DisplayName,
            module.Manifest.PluginVersion,
            $"test:{module.Manifest.ModuleId}",
            sink.Routes,
            sink.Services,
            sink.MemberProviders,
            sink.BackgroundJobs,
            sink.NavEntries,
            Array.Empty<PluginAuditSinkContribution>(),
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

    private sealed class RecordingContributionSink : IPluginContributionSink
    {
        private readonly List<PluginRouteContribution> _routes = new();
        private readonly List<PluginServiceContribution> _services = new();
        private readonly List<PluginMemberProviderContribution> _memberProviders = new();
        private readonly List<PluginBackgroundJobContribution> _backgroundJobs = new();
        private readonly List<PluginNavEntry> _navEntries = new();

        public IReadOnlyList<PluginRouteContribution> Routes => _routes.ToArray();
        public IReadOnlyList<PluginServiceContribution> Services => _services.ToArray();
        public IReadOnlyList<PluginMemberProviderContribution> MemberProviders => _memberProviders.ToArray();
        public IReadOnlyList<PluginBackgroundJobContribution> BackgroundJobs => _backgroundJobs.ToArray();
        public IReadOnlyList<PluginNavEntry> NavEntries => _navEntries.ToArray();

        public void AddRoute(PluginRouteContribution contribution) => _routes.Add(contribution);
        public void AddService(PluginServiceContribution contribution) => _services.Add(contribution);
        public void AddMemberProvider(PluginMemberProviderContribution contribution) => _memberProviders.Add(contribution);
        public void AddBackgroundJob(PluginBackgroundJobContribution contribution) => _backgroundJobs.Add(contribution);
        public void AddNavEntries(IReadOnlyList<PluginNavEntry> entries) => _navEntries.AddRange(entries);
    }

    /// <summary>
    /// A plugin module that contributes 2 nav entries for testing.
    /// </summary>
    private sealed class NavContributingPluginModule : IPluginModule
    {
        public NavContributingPluginModule()
        {
            Manifest = new PluginManifest(
                "plugin.nav.test",
                "Nav Test Plugin",
                new Version(1, 0, 0),
                "Test",
                "MIT",
                typeof(NavContributingPluginModule).FullName!,
                ">=1.0.0",
                ["nav.items.read"],
                ["nav.main"]);
        }

        public PluginManifest Manifest { get; }

        public void RegisterContributions(IPluginContributionSink sink)
        {
            sink.AddNavEntries(
            [
                new PluginNavEntry("Inventar", "bi-box-seam", "/inventar", "nav.items.read", 10),
                new PluginNavEntry("Berichte", "bi-bar-chart", "/berichte", null, 20)
            ]);
        }
    }
}
