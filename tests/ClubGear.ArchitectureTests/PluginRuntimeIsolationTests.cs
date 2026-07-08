using ClubGear.ArchitectureTests.Fixtures;
using ClubGear.Data;
using System.Security.Claims;
using ClubGear.Plugin.Contracts;
using ClubGear.Models;
using ClubGear.Services.Plugins.Installation;
using ClubGear.Services;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Runtime;
using ClubGear.Services.Plugins.Persistence;
using ClubGear.Services.Plugins.Status;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginRuntimeIsolationTests
{
    [Fact]
    public async Task RuntimeAdapter_ExecutesCapability_ThroughFacades()
    {
        var permissionFacade = new FakePermissionFacade
        {
            OnHasPermissionAsync = (_, permissionKey, _, _) => Task.FromResult(permissionKey == "members.read")
        };

        var auditFacade = new FakeAuditFacade();
        var notificationFacade = new FakeNotificationFacade();

        var runtimeAdapter = new PluginRuntimeAdapter(
            permissionFacade,
            auditFacade,
            notificationFacade,
            new FakeMemberFeatureService(),
            NullLogger<PluginRuntimeAdapter>.Instance);

        var module = new TestPluginModule("plugin.slice4");
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "u1") }, "Test"));
        var capability = new Plugins.AllowedPluginCapability("members.read");

        await runtimeAdapter.RunAsync(module, user, capability.ExecuteAsync);

        Assert.True(capability.WasAllowed);
        Assert.Single(auditFacade.Records);
        Assert.Equal("plugin.capability.executed", auditFacade.Records[0].Action);
        Assert.Equal("plugin:plugin.slice4", auditFacade.Records[0].Source);
        Assert.Single(notificationFacade.Messages);
        Assert.Equal("member-1", notificationFacade.Messages[0].Recipient);
    }

    [Fact]
    public async Task RuntimeAdapter_ExposesHostContext_WithPluginMetadata_AndMemberDtos()
    {
        var runtimeAdapter = new PluginRuntimeAdapter(
            new FakePermissionFacade(),
            new FakeAuditFacade(),
            new FakeNotificationFacade(),
            new FakeMemberFeatureService(),
            NullLogger<PluginRuntimeAdapter>.Instance);

        var module = new TestPluginModule("plugin.host", ["members.read"]);
        var capability = new Plugins.HostContextCapability();

        await runtimeAdapter.RunAsync(module, new ClaimsPrincipal(new ClaimsIdentity()), capability.ExecuteAsync);

        Assert.NotNull(capability.Metadata);
        Assert.Equal("plugin.host", capability.Metadata!.ModuleId);
        Assert.Equal("Test Plugin", capability.Metadata.DisplayName);
        Assert.Single(capability.Members);
        Assert.Equal("M-001", capability.Members[0].MemberNumber);
        Assert.NotNull(capability.Member);
        Assert.Equal("Ada Lovelace", capability.Member!.FullName);
    }

    [Fact]
    public async Task RuntimeAdapter_DeniesUndeclaredManifestPermission_WithoutExecutingProtectedWork()
    {
        var permissionFacade = new FakePermissionFacade
        {
            OnHasPermissionAsync = (_, permissionKey, declaredPermissions, _) => Task.FromResult(
                permissionKey == "members.manage"
                && declaredPermissions.Contains(permissionKey, StringComparer.OrdinalIgnoreCase))
        };

        var auditFacade = new FakeAuditFacade();
        var notificationFacade = new FakeNotificationFacade();
        var runtimeAdapter = new PluginRuntimeAdapter(
            permissionFacade,
            auditFacade,
            notificationFacade,
            new FakeMemberFeatureService(),
            NullLogger<PluginRuntimeAdapter>.Instance);

        var module = new TestPluginModule("plugin.slice4", ["members.read"]);
        var capability = new Plugins.AllowedPluginCapability("members.manage");

        await runtimeAdapter.RunAsync(module, new ClaimsPrincipal(new ClaimsIdentity()), capability.ExecuteAsync);

        Assert.False(capability.WasAllowed);
        Assert.Empty(auditFacade.Records);
        Assert.Empty(notificationFacade.Messages);
    }

    [Fact]
    public async Task EndpointRegistrar_RegistersAndInvokesRoute_WhenAuthorized()
    {
        var permissionFacade = new FakePermissionFacade
        {
            OnHasPermissionAsync = (_, permissionKey, _, _) => Task.FromResult(permissionKey == "members.read")
        };

        var runtimeAdapter = new PluginRuntimeAdapter(
            permissionFacade,
            new FakeAuditFacade(),
            new FakeNotificationFacade(),
            new FakeMemberFeatureService(),
            NullLogger<PluginRuntimeAdapter>.Instance);

        var registrar = new PluginEndpointRegistrar(runtimeAdapter);
        var module = new TestPluginModule("plugin.routes");
        var endpoint = new Plugins.AllowedPluginEndpoint();

        registrar.RegisterGet(module, "/health", "members.read", endpoint.HandleAsync);

        var result = await registrar.InvokeGetAsync(module, "/health", new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("ok", result.Body);
        Assert.Single(registrar.Registrations);
    }

    [Fact]
    public async Task EndpointRegistrar_ReturnsForbidden_WhenPermissionMissing()
    {
        var permissionFacade = new FakePermissionFacade
        {
            OnHasPermissionAsync = (_, _, _, _) => Task.FromResult(false)
        };

        var runtimeAdapter = new PluginRuntimeAdapter(
            permissionFacade,
            new FakeAuditFacade(),
            new FakeNotificationFacade(),
            new FakeMemberFeatureService(),
            NullLogger<PluginRuntimeAdapter>.Instance);

        var registrar = new PluginEndpointRegistrar(runtimeAdapter);
        var module = new TestPluginModule("plugin.routes");
        var endpoint = new Plugins.AllowedPluginEndpoint();

        registrar.RegisterGet(module, "/secure", "members.manage", endpoint.HandleAsync);

        var result = await registrar.InvokeGetAsync(module, "/secure", new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.Equal(403, result.StatusCode);
        Assert.Equal("Forbidden", result.Body);
    }

    [Fact]
    public async Task RuntimeAdapter_BlocksForbiddenDirectCoreAccess()
    {
        var runtimeAdapter = new PluginRuntimeAdapter(
            new FakePermissionFacade(),
            new FakeAuditFacade(),
            new FakeNotificationFacade(),
            new FakeMemberFeatureService(),
            NullLogger<PluginRuntimeAdapter>.Instance);

        var module = new TestPluginModule("plugin.forbidden");
        var forbidden = new ClubGear.Services.ForbiddenPlugin.ForbiddenDirectCoreAccessCapability();

        var ex = await Assert.ThrowsAsync<UserFriendlyException>(
            () => runtimeAdapter.RunAsync(module, new ClaimsPrincipal(new ClaimsIdentity()), forbidden.ExecuteAsync));

        Assert.Equal("Direkter Zugriff auf Core-Namensraeume ist fuer Plugins nicht erlaubt.", ex.Message);
    }

    [Fact]
    public void EndpointRegistrar_BlocksForbiddenDirectCoreAccess()
    {
        var runtimeAdapter = new PluginRuntimeAdapter(
            new FakePermissionFacade(),
            new FakeAuditFacade(),
            new FakeNotificationFacade(),
            new FakeMemberFeatureService(),
            NullLogger<PluginRuntimeAdapter>.Instance);

        var registrar = new PluginEndpointRegistrar(runtimeAdapter);
        var module = new TestPluginModule("plugin.forbidden");
        var forbiddenEndpoint = new ClubGear.Controllers.ForbiddenPlugin.ForbiddenDirectCoreEndpoint();

        var ex = Assert.Throws<UserFriendlyException>(
            () => registrar.RegisterGet(module, "/forbidden", "members.read", forbiddenEndpoint.HandleAsync));

        Assert.Equal("Direkter Zugriff auf Core-Namensraeume ist fuer Plugins nicht erlaubt.", ex.Message);
    }

    [Fact]
    public async Task RuntimeAdapter_BlocksCapturedApplicationDbContextAccess()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var runtimeAdapter = new PluginRuntimeAdapter(
            new FakePermissionFacade(),
            new FakeAuditFacade(),
            new FakeNotificationFacade(),
            new FakeMemberFeatureService(),
            NullLogger<PluginRuntimeAdapter>.Instance);

        var module = new TestPluginModule("plugin.dbcontext");
        var forbidden = new ClubGear.ArchitectureTests.ForbiddenPlugin.ForbiddenDbContextCapability(dbContext);

        var ex = await Assert.ThrowsAsync<UserFriendlyException>(
            () => runtimeAdapter.RunAsync(module, new ClaimsPrincipal(new ClaimsIdentity()), forbidden.ExecuteAsync));

        Assert.Equal("Direkter Zugriff auf Core-Namensraeume ist fuer Plugins nicht erlaubt.", ex.Message);
    }

    [Fact]
    public async Task PluginLoader_LoadsEachPlugin_IntoDedicatedAssemblyLoadContexts()
    {
        var packageStore = new FileSystemPluginPackageStore(
            Path.Combine(Path.GetTempPath(), "clubgear-plugin-loader-tests", Guid.NewGuid().ToString("N")));
        var loader = new PluginLoader(packageStore, NullLogger<PluginLoader>.Instance);

        var pluginA = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            packageStore,
            "plugin.runtime.a",
            "Runtime Plugin A",
            typeof(Plugins.RuntimeLoadedPluginModuleA).FullName!);
        var pluginB = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            packageStore,
            "plugin.runtime.b",
            "Runtime Plugin B",
            typeof(Plugins.RuntimeLoadedPluginModuleB).FullName!);

        var loadedA = await loader.LoadAsync(pluginA);
        var loadedB = await loader.LoadAsync(pluginB);

        Assert.True(loadedA.Success, loadedA.Error);
        Assert.True(loadedB.Success, loadedB.Error);
        Assert.NotNull(loadedA.LoadedPlugin);
        Assert.NotNull(loadedB.LoadedPlugin);
        Assert.NotSame(loadedA.LoadedPlugin!.LoadContext, loadedB.LoadedPlugin!.LoadContext);
        Assert.True(loadedA.LoadedPlugin.LoadContext.IsCollectible);
        Assert.True(loadedB.LoadedPlugin.LoadContext.IsCollectible);
        Assert.NotEqual(loadedA.LoadedPlugin.Runtime.LoadContextName, loadedB.LoadedPlugin.Runtime.LoadContextName);
    }

    [Fact]
    public async Task PluginLifecycleService_LeavesValidPluginRegistered_WhenAnotherEntryPointFails()
    {
        await using var fixture = await CreateLifecycleFixtureAsync();

        var validRecord = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.a",
            "Runtime Plugin A",
            typeof(Plugins.RuntimeLoadedPluginModuleA).FullName!);
        var invalidRecord = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.invalid",
            "Broken Runtime Plugin",
            "ClubGear.ArchitectureTests.Plugins.DoesNotExist");

        await fixture.Store.UpsertAsync(validRecord);
        await fixture.Store.UpsertAsync(invalidRecord);

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

        Assert.Equal(2, results.Count);
        Assert.Contains(results, result => result.Success && result.Plugin?.ModuleId == "plugin.runtime.a");
        Assert.Contains(results, result => !result.Success && result.Plugin?.ModuleId == "plugin.runtime.invalid");
        Assert.NotNull(registry.GetByModuleId("plugin.runtime.a"));
        Assert.Null(registry.GetByModuleId("plugin.runtime.invalid"));

        var failedRecord = fixture.Store.GetByKey("plugin.runtime.invalid");
        Assert.NotNull(failedRecord);
        Assert.True(failedRecord!.IsActive);
        Assert.Contains("wurde im Plugin-Paket nicht gefunden", failedRecord.LastError, StringComparison.Ordinal);
    }

    private sealed class TestPluginModule : IPluginModule
    {
        public TestPluginModule(string moduleId, IReadOnlyList<string>? permissions = null)
        {
            Manifest = new PluginManifest(
                moduleId,
                "Test Plugin",
                new Version(1, 0, 0),
                "ClubGear",
                "Test",
                "Test.EntryPoint",
                ContractVersion.Current.ToString(),
            permissions ?? ["members.read", "members.manage"],
                Array.Empty<string>());
        }

        public PluginManifest Manifest { get; }
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

    private sealed class FakeAuditFacade : IExtensionAuditFacade
    {
        public List<AuditLogRecord> Records { get; } = new();

        public Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }

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
        public List<NotificationMessage> Messages { get; } = new();

        public Task<NotificationResult> NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.FromResult(new NotificationResult(true, message.Channel, message.Recipient));
        }
    }

    private sealed class FakeMemberFeatureService : IMemberFeatureService
    {
        private readonly Member[] _members =
        [
            new()
            {
                Id = 1,
                MemberNumber = "M-001",
                FirstName = "Ada",
                LastName = "Lovelace",
                Email = "ada@example.org",
                PhoneNumber = "+49-123",
                IsActive = true
            }
        ];

        public Task<IReadOnlyList<Member>> GetListAsync(string? search = null, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Member> result = string.IsNullOrWhiteSpace(search)
                ? _members
                : _members.Where(member => member.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)).ToArray();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<Member>> GetInactiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Member>>(_members.Where(member => !member.IsActive).ToArray());

        public Task<Member?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<Member?>(_members.SingleOrDefault(member => member.Id == id));

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
            Path.Combine(Path.GetTempPath(), "clubgear-plugin-lifecycle-tests", Guid.NewGuid().ToString("N")));

        return new LifecycleFixture(connection, dbContext, new DbPluginStatusStore(dbContext), packageStore);
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
}