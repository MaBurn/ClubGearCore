using System.Security.Claims;
using ClubGear.ArchitectureTests.Fixtures;
using ClubGear.Controllers;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Core;
using ClubGear.Services.Plugins.Installation;
using ClubGear.Services.Plugins.Persistence;
using ClubGear.Services.Plugins.Runtime;
using ClubGear.Services.Plugins.Status;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class MemberPluginPersistenceIntegrationTests
{
    [Fact]
    public async Task GetSlotsAsync_ReturnsCardFromPluginOwnedTable_AfterSuccessfulActivation()
    {
        await using var fixture = await CreateFixtureAsync();

        var registry = new PluginRegistry();
        var lifecycleService = CreateLifecycleService(fixture, registry);
        var slotService = new MemberPluginSlotService(
            registry,
            CreateRuntimeAdapter(fixture.DbContext, fixture.MemberService),
            fixture.MemberService,
            NullLogger<MemberPluginSlotService>.Instance);

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.migrating",
            "Runtime Plugin With Migration",
            typeof(Plugins.MigratingPluginModule).FullName!,
            isActive: false);

        await fixture.Store.UpsertAsync(record);

        var activation = await lifecycleService.ActivateAsync("plugin.runtime.migrating");
        Assert.True(activation.Success);

        var snapshot = await slotService.GetSlotsAsync(fixture.MemberService.Member, BuildUser());

        var card = Assert.Single(snapshot.DetailCards);
        Assert.Equal("Plugin-Notiz", card.Card.Title);
        Assert.Contains("Persistierte Plugin-Notiz", card.Card.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MembersController_Details_OnlyShowsPersistedSlot_ForSuccessfullyMigratedPlugin()
    {
        await using var fixture = await CreateFixtureAsync();

        var registry = new PluginRegistry();
        var lifecycleService = CreateLifecycleService(fixture, registry);
        var slotService = new MemberPluginSlotService(
            registry,
            CreateRuntimeAdapter(fixture.DbContext, fixture.MemberService),
            fixture.MemberService,
            NullLogger<MemberPluginSlotService>.Instance);

        var validRecord = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.migrating",
            "Runtime Plugin With Migration",
            typeof(Plugins.MigratingPluginModule).FullName!,
            isActive: false);
        var invalidRecord = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            fixture.PackageStore,
            "plugin.runtime.badmigration",
            "Runtime Plugin With Bad Migration",
            typeof(Plugins.BadMigratingPluginModule).FullName!,
            isActive: false);

        await fixture.Store.UpsertAsync(validRecord);
        await fixture.Store.UpsertAsync(invalidRecord);

        Assert.True((await lifecycleService.ActivateAsync("plugin.runtime.migrating")).Success);
        Assert.False((await lifecycleService.ActivateAsync("plugin.runtime.badmigration")).Success);

        using var sut = new MembersController(fixture.MemberService, slotService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = BuildUser()
                }
            }
        };

        var result = await sut.Details(1);

        var view = Assert.IsType<ViewResult>(result);
        var snapshot = Assert.IsType<MemberPluginSlotSnapshot>(view.ViewData["MemberPluginSlots"]);
        var card = Assert.Single(snapshot.DetailCards);
        Assert.Equal("plugin.runtime.migrating", card.ModuleId);
    }

    private static PluginLifecycleService CreateLifecycleService(Fixture fixture, PluginRegistry registry)
    {
        return new PluginLifecycleService(
            fixture.Store,
            registry,
            new PluginEndpointRegistrar(new NoOpRuntimeAdapter(), registry),
            new PluginLoader(fixture.PackageStore, NullLogger<PluginLoader>.Instance),
            new PluginMigrationRunner(fixture.DbContext, fixture.SchemaNamePolicy, NullLogger<PluginMigrationRunner>.Instance),
            new NoOpPluginBackgroundJobRunner(),
            NullLogger<PluginLifecycleService>.Instance);
    }

    private static PluginRuntimeAdapter CreateRuntimeAdapter(ApplicationDbContext dbContext, FakeMemberFeatureService memberService)
    {
        return new PluginRuntimeAdapter(
            new AllowAllPermissionFacade(),
            new NoOpAuditFacade(),
            new NoOpNotificationFacade(),
            memberService,
            NullLogger<PluginRuntimeAdapter>.Instance,
            dbContext,
            new PluginSchemaNamePolicy());
    }

    private static ClaimsPrincipal BuildUser()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "plugin-persistence-tester")], "TestAuth"));

    private static async Task<Fixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var packageStore = new FileSystemPluginPackageStore(
            Path.Combine(Path.GetTempPath(), "clubgear-plugin-persistence-integration", Guid.NewGuid().ToString("N")));

        return new Fixture(connection, dbContext, new DbPluginStatusStore(dbContext), packageStore, new PluginSchemaNamePolicy(), new FakeMemberFeatureService());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Fixture(
            SqliteConnection connection,
            ApplicationDbContext dbContext,
            DbPluginStatusStore store,
            FileSystemPluginPackageStore packageStore,
            PluginSchemaNamePolicy schemaNamePolicy,
            FakeMemberFeatureService memberService)
        {
            Connection = connection;
            DbContext = dbContext;
            Store = store;
            PackageStore = packageStore;
            SchemaNamePolicy = schemaNamePolicy;
            MemberService = memberService;
        }

        public SqliteConnection Connection { get; }

        public ApplicationDbContext DbContext { get; }

        public DbPluginStatusStore Store { get; }

        public FileSystemPluginPackageStore PackageStore { get; }

        public PluginSchemaNamePolicy SchemaNamePolicy { get; }

        public FakeMemberFeatureService MemberService { get; }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class NoOpRuntimeAdapter : IPluginRuntimeAdapter
    {
        public IPluginRuntimeBridge CreateBridge(Plugin.Contracts.IPluginModule pluginModule, ClaimsPrincipal user)
            => throw new NotSupportedException();

        public Task<TResult> InvokeAsync<TResult>(
            Plugin.Contracts.IPluginModule pluginModule,
            ClaimsPrincipal user,
            Func<IPluginRuntimeBridge, CancellationToken, Task<TResult>> capability,
            string? requiredPermissionKey = null,
            Delegate? isolatedDelegate = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RunAsync(
            Plugin.Contracts.IPluginModule pluginModule,
            ClaimsPrincipal user,
            Func<IPluginRuntimeBridge, CancellationToken, Task> capability,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void EnsureIsolated(Delegate pluginDelegate)
        {
        }
    }

    private sealed class FakeMemberFeatureService : IMemberFeatureService
    {
        public Member Member { get; } = new()
        {
            Id = 1,
            MemberNumber = "M-001",
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.org",
            PhoneNumber = "+49-111",
            IsActive = true
        };

        public Task<IReadOnlyList<Member>> GetListAsync(string? search = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Member>>([Member]);

        public Task<IReadOnlyList<Member>> GetInactiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Member>>(Array.Empty<Member>());

        public Task<Member?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<Member?>(id == Member.Id ? Member : null);

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

    private sealed class AllowAllPermissionFacade : IExtensionPermissionFacade
    {
        public Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string permissionKey,
            IReadOnlyCollection<string> declaredPermissions,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class NoOpAuditFacade : IExtensionAuditFacade
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

    private sealed class NoOpNotificationFacade : IExtensionNotificationFacade
    {
        public Task<NotificationResult> NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default)
            => Task.FromResult(new NotificationResult(true, message.Channel, message.Recipient));
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