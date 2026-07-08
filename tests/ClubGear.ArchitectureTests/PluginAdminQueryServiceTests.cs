using System.Runtime.Loader;
using System.Text.Json;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Admin;
using ClubGear.Services.Plugins.Runtime;
using ClubGear.Services.Plugins.Status;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginAdminQueryServiceTests
{
    [Fact]
    public async Task GetPluginStatuses_MergesStoreAndRuntimeIntoSingleAdminView()
    {
        await using var fixture = await CreateFixtureAsync();

        await fixture.Store.UpsertAsync(new PluginStatusRecord
        {
            Key = "plugin.runtime.a",
            DisplayName = "Runtime Plugin A",
            Version = "1.2.3",
            Author = "Plugin Tests",
            License = "Commercial",
            EntryPoint = typeof(Plugins.RuntimeLoadedPluginModuleA).FullName!,
            RequiredCoreVersion = ">=1.0.0",
            InstallSource = "zip",
            PackageHash = "ABC123",
            PackagePath = "/tmp/plugin-runtime-a.zip",
            IsActive = true,
            Category = "member-profile",
            PermissionsJson = JsonSerializer.Serialize(new[] { "members.read", "members.manage" }),
            ExtensionPointsJson = JsonSerializer.Serialize(new[] { "member.detail", "member.action" }),
            InstalledAtUtc = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAtUtc = DateTime.UtcNow
        });

        RegisterRuntime(fixture.Registry, new Plugins.RuntimeLoadedPluginModuleA());

        var result = fixture.Service.GetPluginStatuses();

        var status = Assert.Single(result);
        Assert.Equal("plugin.runtime.a", status.ModuleId);
        Assert.True(status.IsInstalled);
        Assert.True(status.IsActive);
        Assert.True(status.IsRuntimeRegistered);
        Assert.True(status.IsContractCompatible);
        Assert.Equal("Commercial", status.License);
        Assert.Equal(1, status.RouteCount);
        Assert.Equal(1, status.ServiceCount);
        Assert.Equal(4, status.MemberProviderCount);
        Assert.Equal(1, status.BackgroundJobCount);
        Assert.Equal("member-profile", status.Category);
        Assert.Equal("ABC123", status.PackageHash);
        Assert.Equal(0, status.AppliedMigrationCount);
    }

    [Fact]
    public async Task GetPluginStatuses_IncludesInactiveFailures_AndRegistryOnlyEntries()
    {
        await using var fixture = await CreateFixtureAsync();

        await fixture.Store.UpsertAsync(new PluginStatusRecord
        {
            Key = "plugin.failed",
            DisplayName = "Broken Plugin",
            Version = "9.0.0",
            Author = "Plugin Tests",
            License = "Proprietary",
            EntryPoint = "Broken.Plugin",
            RequiredCoreVersion = ">=9.0.0",
            InstallSource = "marketplace",
            PackageHash = "XYZ",
            PackagePath = "/tmp/plugin-failed.zip",
            IsActive = false,
            LastError = "Migration failed",
            Category = "General",
            PermissionsJson = "[]",
            ExtensionPointsJson = "[]",
            InstalledAtUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAtUtc = DateTime.UtcNow
        });

        RegisterRuntime(fixture.Registry, new Plugins.RuntimeLoadedPluginModuleB());

        var result = fixture.Service.GetPluginStatuses();

        Assert.Equal(2, result.Count);

        var failed = Assert.Single(result.Where(status => status.ModuleId == "plugin.failed"));
        Assert.False(failed.IsRuntimeRegistered);
        Assert.False(failed.IsContractCompatible);
        Assert.Equal("Migration failed", failed.LastError);
        Assert.Equal("General", failed.Category);

        var runtimeOnly = Assert.Single(result.Where(status => status.ModuleId == "plugin.runtime.b"));
        Assert.False(runtimeOnly.IsInstalled);
        Assert.True(runtimeOnly.IsRuntimeRegistered);
        Assert.True(runtimeOnly.IsActive);
        Assert.Equal("runtime", runtimeOnly.Source);
    }

    [Fact]
    public async Task GetPluginStatus_PopulatesPackageHashAndAppliedMigrationCount()
    {
        await using var fixture = await CreateFixtureAsync();

        await fixture.Store.UpsertAsync(new PluginStatusRecord
        {
            Key = "plugin.with.migrations",
            DisplayName = "Migration Plugin",
            Version = "2.0.0",
            Author = "Plugin Tests",
            License = "MIT",
            EntryPoint = "Migration.Plugin",
            RequiredCoreVersion = ">=1.0.0",
            InstallSource = "zip",
            PackageHash = "DEADBEEF",
            PackagePath = "/tmp/plugin-migrations.zip",
            IsActive = false,
            Category = "General",
            PermissionsJson = "[]",
            ExtensionPointsJson = "[]",
            InstalledAtUtc = DateTime.UtcNow.AddMinutes(-2),
            UpdatedAtUtc = DateTime.UtcNow
        });

        fixture.DbContext.PluginMigrationStates.AddRange(
            new ClubGear.Models.PluginMigrationState { PluginKey = "plugin.with.migrations", MigrationId = "m001", TablePrefix = "mig_" },
            new ClubGear.Models.PluginMigrationState { PluginKey = "plugin.with.migrations", MigrationId = "m002", TablePrefix = "mig_" },
            new ClubGear.Models.PluginMigrationState { PluginKey = "other.plugin", MigrationId = "m001", TablePrefix = "other_" });
        await fixture.DbContext.SaveChangesAsync();

        var result = fixture.Service.GetPluginStatus("plugin.with.migrations");

        Assert.NotNull(result);
        Assert.Equal("DEADBEEF", result.PackageHash);
        Assert.Equal(2, result.AppliedMigrationCount);
    }

    private static void RegisterRuntime(PluginRegistry registry, Plugin.Contracts.IPluginModule module)
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
            Array.Empty<Plugin.Contracts.PluginAuditSinkContribution>(),
            Array.Empty<Plugin.Contracts.PluginIdentityProviderContribution>(),
            Array.Empty<Plugin.Contracts.PluginSelfServiceProfileProviderContribution>());

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
        var compatibility = new FakeContractCompatibilityService();
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

    private sealed class FakeContractCompatibilityService : IContractCompatibilityService
    {
        public ContractCompatibilityResult Validate(Version pluginContractVersion)
            => pluginContractVersion.Major >= 9
                ? new ContractCompatibilityResult(false, "unsupported")
                : new ContractCompatibilityResult(true);
    }

    private sealed class RecordingContributionSink : Plugin.Contracts.IPluginContributionSink
    {
        private readonly List<Plugin.Contracts.PluginRouteContribution> _routes = new();
        private readonly List<Plugin.Contracts.PluginServiceContribution> _services = new();
        private readonly List<Plugin.Contracts.PluginMemberProviderContribution> _memberProviders = new();
        private readonly List<Plugin.Contracts.PluginBackgroundJobContribution> _backgroundJobs = new();
        private readonly List<Plugin.Contracts.PluginNavEntry> _navEntries = new();

        public IReadOnlyList<Plugin.Contracts.PluginRouteContribution> Routes => _routes.ToArray();

        public IReadOnlyList<Plugin.Contracts.PluginServiceContribution> Services => _services.ToArray();

        public IReadOnlyList<Plugin.Contracts.PluginMemberProviderContribution> MemberProviders => _memberProviders.ToArray();

        public IReadOnlyList<Plugin.Contracts.PluginBackgroundJobContribution> BackgroundJobs => _backgroundJobs.ToArray();

        public IReadOnlyList<Plugin.Contracts.PluginNavEntry> NavEntries => _navEntries.ToArray();

        public void AddRoute(Plugin.Contracts.PluginRouteContribution contribution) => _routes.Add(contribution);

        public void AddService(Plugin.Contracts.PluginServiceContribution contribution) => _services.Add(contribution);

        public void AddMemberProvider(Plugin.Contracts.PluginMemberProviderContribution contribution) => _memberProviders.Add(contribution);

        public void AddBackgroundJob(Plugin.Contracts.PluginBackgroundJobContribution contribution) => _backgroundJobs.Add(contribution);

        public void AddNavEntries(IReadOnlyList<Plugin.Contracts.PluginNavEntry> entries) => _navEntries.AddRange(entries);
    }
}