using System.Security.Claims;
using System.Text.Json;
using ClubGear.ArchitectureTests.Fixtures;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services;
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

public sealed class PluginPermissionEnforcementTests
{
    [Fact]
    public async Task RuntimeAdapter_InvokeAsync_ThrowsPluginPermissionDeniedException_WhenPermissionIsNotGranted()
    {
        var runtimeAdapter = CreateRuntimeAdapter((_, _, _, _) => Task.FromResult(false));
        var module = new TestPluginModule("plugin.permissions", ["members.manage"]);
        var capability = new ProtectedCapability();

        var ex = await Assert.ThrowsAsync<PluginPermissionDeniedException>(
            () => runtimeAdapter.InvokeAsync(module, new ClaimsPrincipal(new ClaimsIdentity()), capability.ExecuteAsync, "members.manage"));

        Assert.Equal("plugin.permissions", ex.ModuleId);
        Assert.Equal("members.manage", ex.PermissionKey);
        Assert.False(capability.Executed);
    }

    [Fact]
    public async Task EndpointRegistrar_ReturnsForbidden_WhenCentralDispatchRejectsPermission()
    {
        var runtimeAdapter = CreateRuntimeAdapter((_, _, _, _) => Task.FromResult(false));
        var registrar = new PluginEndpointRegistrar(runtimeAdapter);
        var module = new TestPluginModule("plugin.routes", ["members.manage"]);

        registrar.RegisterGet(module, "/secure", "members.manage", new ProtectedEndpoint().HandleAsync);

        var result = await registrar.InvokeGetAsync(module, "/secure", new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Equal(403, result.StatusCode);
        Assert.Equal("Forbidden", result.Body);
    }

    [Fact]
    public async Task PluginLifecycleService_ActivatesPlugin_WithoutCheckingRuntimePermissionGrant()
    {
        await using var fixture = await CreateLifecycleFixtureAsync();
        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.a",
            "Runtime Plugin A",
            typeof(Plugins.RuntimeLoadedPluginModuleA).FullName!,
            isActive: false);

        record.PermissionsJson = JsonSerializer.Serialize(new[] { "members.manage" });
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

        var result = await lifecycleService.ActivateAsync("plugin.runtime.a");

        Assert.True(result.Success);
        Assert.Equal("activated", result.Status);
        Assert.True(result.Plugin!.IsActive);
        Assert.NotNull(registry.GetByModuleId("plugin.runtime.a"));
    }

    private static PluginRuntimeAdapter CreateRuntimeAdapter(
        Func<ClaimsPrincipal, string, IReadOnlyCollection<string>, CancellationToken, Task<bool>> permissionHandler)
    {
        return new PluginRuntimeAdapter(
            new FakePermissionFacade { OnHasPermissionAsync = permissionHandler },
            new FakeAuditFacade(),
            new FakeNotificationFacade(),
            new FakeMemberFeatureService(),
            NullLogger<PluginRuntimeAdapter>.Instance);
    }

    private sealed class TestPluginModule : IPluginModule
    {
        public TestPluginModule(string moduleId, IReadOnlyList<string> permissions)
        {
            Manifest = new PluginManifest(
                moduleId,
                "Permissions Test Plugin",
                new Version(1, 0, 0),
                "Plugin Tests",
                "Proprietary",
                "Plugin.EntryPoint",
                ">=1.0.0",
                permissions,
                Array.Empty<string>());
        }

        public PluginManifest Manifest { get; }
    }

    private sealed class ProtectedCapability
    {
        public bool Executed { get; private set; }

        public Task<string> ExecuteAsync(IPluginRuntimeBridge runtime, CancellationToken cancellationToken)
        {
            Executed = true;
            return Task.FromResult("ok");
        }
    }

    private sealed class ProtectedEndpoint
    {
        public Task<PluginEndpointResult> HandleAsync(IPluginRuntimeBridge runtime, CancellationToken cancellationToken)
            => Task.FromResult(new PluginEndpointResult(200, "ok"));
    }

    private sealed class FakePermissionFacade : IExtensionPermissionFacade
    {
        public Func<ClaimsPrincipal, string, IReadOnlyCollection<string>, CancellationToken, Task<bool>> OnHasPermissionAsync { get; set; }
            = (_, _, _, _) => Task.FromResult(false);

        public Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string permissionKey,
            IReadOnlyCollection<string> declaredPermissions,
            CancellationToken cancellationToken = default)
            => OnHasPermissionAsync(user, permissionKey, declaredPermissions, cancellationToken);
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
            Path.Combine(Path.GetTempPath(), "clubgear-plugin-permission-tests", Guid.NewGuid().ToString("N")));

        return new LifecycleFixture(connection, dbContext, new DbPluginStatusStore(dbContext), packageStore);
    }

    private sealed class FakeAuditFacade : IExtensionAuditFacade
    {
        public Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task LogChangeAsync(
            string action,
            object? before,
            object? after,
            string? actor = null,
            string? source = null,
            string? targetType = null,
            string? targetId = null,
            object? metadata = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeNotificationFacade : IExtensionNotificationFacade
    {
        public Task<NotificationResult> NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default)
            => Task.FromResult(new NotificationResult(true, message.Channel, message.Recipient));
    }

    private sealed class FakeMemberFeatureService : IMemberFeatureService
    {
        public Task<IReadOnlyList<Member>> GetListAsync(string? search = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Member>>(Array.Empty<Member>());

        public Task<IReadOnlyList<Member>> GetInactiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Member>>(Array.Empty<Member>());

        public Task<Member?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<Member?>(null);

        public Task CreateAsync(Member member, string? actor, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemberMutationStatus> UpdateAsync(Member member, string? actor, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemberMutationStatus> VerifyAsync(int id, string? actor, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemberMutationStatus> DeleteAsync(int id, string? actor, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> BulkDeleteAsync(IReadOnlyCollection<int> ids, string? actor, bool hasManagePermission, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MembersImportResult> ImportCsvAsync(Stream csvStream, string? actor, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
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

    private sealed class NoOpRuntimeAdapter : IPluginRuntimeAdapter
    {
        public IPluginRuntimeBridge CreateBridge(IPluginModule pluginModule, ClaimsPrincipal user)
            => throw new NotSupportedException();

        public Task<TResult> InvokeAsync<TResult>(
            IPluginModule pluginModule,
            ClaimsPrincipal user,
            Func<IPluginRuntimeBridge, CancellationToken, Task<TResult>> capability,
            string? requiredPermissionKey = null,
            Delegate? isolatedDelegate = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RunAsync(
            IPluginModule pluginModule,
            ClaimsPrincipal user,
            Func<IPluginRuntimeBridge, CancellationToken, Task> capability,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void EnsureIsolated(Delegate pluginDelegate)
        {
        }
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