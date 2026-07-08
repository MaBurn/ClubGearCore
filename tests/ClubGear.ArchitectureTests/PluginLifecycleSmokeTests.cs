using ClubGear.ArchitectureTests.Fixtures;
using ClubGear.Controllers;
using ClubGear.Controllers.Api;
using ClubGear.Data;
using ClubGear.Models.PluginAdmin;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins;
using ClubGear.Services.Plugins.Admin;
using ClubGear.Services.Plugins.Installation;
using ClubGear.Services.Plugins.Persistence;
using ClubGear.Services.Plugins.Runtime;
using ClubGear.Services.Plugins.Status;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginLifecycleSmokeTests
{
    [Fact]
    public async Task InstalledPlugin_StaysConsistentAcrossQueryService_Api_AndMvc_DuringActivationCycle()
    {
        await using var fixture = await CreateFixtureAsync();
        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.a",
            "Runtime Plugin A",
            typeof(Plugins.RuntimeLoadedPluginModuleA).FullName!,
            isActive: false);
        await fixture.Store.UpsertAsync(record);

        var api = new PluginsController(new FakePluginInstallerService(), fixture.LifecycleService, fixture.QueryService);
        using var mvc = CreateMvcController(fixture);

        var initial = fixture.QueryService.GetPluginStatus("plugin.runtime.a");
        Assert.NotNull(initial);
        Assert.True(initial!.IsInstalled);
        Assert.False(initial.IsActive);
        Assert.False(initial.IsRuntimeRegistered);

        var initialPayload = Assert.IsAssignableFrom<IReadOnlyList<PluginAdminStatusViewModel>>(((OkObjectResult)api.GetInstalled()).Value);
        Assert.False(initialPayload.Single().IsRuntimeRegistered);

        var activate = await api.Activate("plugin.runtime.a");
        Assert.IsType<OkObjectResult>(activate);

        var active = fixture.QueryService.GetPluginStatus("plugin.runtime.a");
        Assert.NotNull(active);
        Assert.True(active!.IsActive);
        Assert.True(active.IsRuntimeRegistered);

        var indexAfterActivate = Assert.IsType<ViewResult>(mvc.Index());
        var activeModel = Assert.IsAssignableFrom<IReadOnlyList<PluginAdminStatusViewModel>>(indexAfterActivate.Model);
        Assert.True(activeModel.Single(status => status.ModuleId == "plugin.runtime.a").IsRuntimeRegistered);

        var deactivate = await mvc.Deactivate("plugin.runtime.a");
        Assert.IsType<RedirectToActionResult>(deactivate);

        var inactiveAgain = fixture.QueryService.GetPluginStatus("plugin.runtime.a");
        Assert.NotNull(inactiveAgain);
        Assert.False(inactiveAgain!.IsActive);
        Assert.False(inactiveAgain.IsRuntimeRegistered);

        var finalPayload = Assert.IsAssignableFrom<IReadOnlyList<PluginAdminStatusViewModel>>(((OkObjectResult)api.GetInstalled()).Value);
        Assert.False(finalPayload.Single().IsRuntimeRegistered);
    }

    [Fact]
    public async Task FailedPluginActivation_PropagatesSameErrorToQueryService_Api_AndMvc()
    {
        await using var fixture = await CreateFixtureAsync();
        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.badmigration",
            "Runtime Plugin With Bad Migration",
            typeof(Plugins.BadMigratingPluginModule).FullName!,
            isActive: false);
        await fixture.Store.UpsertAsync(record);

        var api = new PluginsController(new FakePluginInstallerService(), fixture.LifecycleService, fixture.QueryService);
        using var mvc = CreateMvcController(fixture);

        var activate = await api.Activate("plugin.runtime.badmigration");

        var badRequest = Assert.IsType<BadRequestObjectResult>(activate);
        var payload = Assert.IsType<PluginLifecycleOperationResult>(badRequest.Value);
        Assert.Equal("migration-failed", payload.Status);

        var status = fixture.QueryService.GetPluginStatus("plugin.runtime.badmigration");
        Assert.NotNull(status);
        Assert.False(status!.IsActive);
        Assert.False(status.IsRuntimeRegistered);
        Assert.Contains("Praefix", status.LastError, StringComparison.Ordinal);

        var index = Assert.IsType<ViewResult>(mvc.Index());
        var model = Assert.IsAssignableFrom<IReadOnlyList<PluginAdminStatusViewModel>>(index.Model);
        var failed = model.Single(entry => entry.ModuleId == "plugin.runtime.badmigration");
        Assert.Equal(status.LastError, failed.LastError);
    }

    private static PluginAdminController CreateMvcController(Fixture fixture)
    {
        var controller = new PluginAdminController(new FakePluginInstallerService(), fixture.LifecycleService, fixture.QueryService, new FakePluginUninstallService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, new TestTempDataProvider());
        return controller;
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
        var packageRootPath = Path.Combine(Path.GetTempPath(), "clubgear-plugin-admin-smoke", Guid.NewGuid().ToString("N"));
        var packageStore = new FileSystemPluginPackageStore(packageRootPath);
        var lifecycleService = new PluginLifecycleService(
            store,
            registry,
            new PluginEndpointRegistrar(new NoOpRuntimeAdapter(), registry),
            new PluginLoader(packageStore, NullLogger<PluginLoader>.Instance),
            new PluginMigrationRunner(dbContext, new PluginSchemaNamePolicy(), NullLogger<PluginMigrationRunner>.Instance),
            new NoOpPluginBackgroundJobRunner(),
            NullLogger<PluginLifecycleService>.Instance);
        var queryService = new PluginAdminQueryService(store, registry, new ContractCompatibilityService(), dbContext, new NoOpPluginBackgroundJobRunner());

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

    private sealed class FakePluginInstallerService : IPluginInstallerService
    {
        public Task<PluginInstallOperationResult> InstallOrUpgradeFromMarketplaceAsync(string moduleId, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginInstallOperationResult(false, "not-configured", "not-configured"));

        public Task<PluginInstallOperationResult> InstallOrUpgradeFromZipAsync(string fileName, byte[] zipBytes, string expectedSha256Hex, string signatureBase64, string signerPublicKeyPem, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginInstallOperationResult(false, "not-configured", "not-configured"));

        public IReadOnlyList<InstalledPluginRecord> GetInstalledPlugins()
            => Array.Empty<InstalledPluginRecord>();
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
            => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
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

    private sealed class FakePluginUninstallService : IPluginUninstallService
    {
        public Task<PluginUninstallResult> UninstallAsync(string moduleId, bool removeData, CancellationToken ct = default)
            => Task.FromResult(new PluginUninstallResult(true, "uninstalled", "ok"));
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