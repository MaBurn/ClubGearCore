using System.Security.Claims;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Core;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// CORE-29 (contract 1.16.0). <see cref="IPluginHostContext.Actor"/> tells a plugin which member is
/// acting. The id must come only from the principal the host built the context for, through the
/// explicit <see cref="Member.ApplicationUserId"/> link, and must never be guessed.
/// </summary>
public sealed class PluginActorFacadeTests
{
    private const string LinkedUserId = "user-linked";

    [Fact]
    public async Task Actor_ReturnsLinkedMember_ForSignedInUser()
    {
        await using var fixture = await Fixture.CreateAsync();
        var memberId = await fixture.AddMemberAsync(applicationUserId: LinkedUserId);

        var actor = await fixture.ActorFor(SignedIn(LinkedUserId)).GetMemberIdAsync();

        Assert.Equal(memberId, actor);
    }

    [Fact]
    public async Task Actor_IsNull_ForSignedInUserWithoutLinkedMember_AndDoesNotLinkByEmail()
    {
        await using var fixture = await Fixture.CreateAsync();
        // Self-service would link this member by e-mail on first visit; the plugin path must not.
        var memberId = await fixture.AddMemberAsync(applicationUserId: null, email: "someone@example.org");

        var actor = await fixture.ActorFor(SignedIn("user-unlinked")).GetMemberIdAsync();

        Assert.Null(actor);
        var stored = await fixture.Db.Members.AsNoTracking().SingleAsync(m => m.Id == memberId);
        Assert.Null(stored.ApplicationUserId);
    }

    [Fact]
    public async Task Actor_IsNull_ForUnauthenticatedIdentity_EvenWithAUserIdClaim()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddMemberAsync(applicationUserId: LinkedUserId);

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, LinkedUserId)]));

        Assert.Null(await fixture.ActorFor(anonymous).GetMemberIdAsync());
    }

    [Fact]
    public async Task Actor_IsNull_ForSystemPrincipal_WithoutUserId()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddMemberAsync(applicationUserId: LinkedUserId);

        // The shape PluginContributionActivator uses when no user is involved.
        var system = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "system.plugin-contribution-activator")], "System"));

        Assert.Null(await fixture.ActorFor(system).GetMemberIdAsync());
    }

    [Fact]
    public async Task Actor_DefaultInterfaceImplementation_ReturnsNull()
    {
        IPluginHostContext context = new ContextWrittenAgainst115();

        Assert.Null(await context.Actor.GetMemberIdAsync());
    }

    private static ClaimsPrincipal SignedIn(string userId)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "TestAuth"));

    private static PluginManifest Manifest() => new(
        "plugin.actor.tests",
        "Actor Tests",
        new Version(1, 0, 0),
        "Plugin Tests",
        "MIT",
        "Plugin.EntryPoint",
        ">=1.16.0",
        Array.Empty<string>(),
        Array.Empty<string>());

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly PluginRuntimeAdapter _adapter;

        private Fixture(SqliteConnection connection, ApplicationDbContext db)
        {
            _connection = connection;
            Db = db;
            _adapter = new PluginRuntimeAdapter(
                new DenyAllPermissions(),
                new NoOpAudit(),
                new NoOpNotifications(),
                new MemberFeatureService(db, new NoOpAuditLog()),
                NullLogger<PluginRuntimeAdapter>.Instance);
        }

        public ApplicationDbContext Db { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options);
            await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, db);
        }

        public async Task<int> AddMemberAsync(string? applicationUserId, string? email = null)
        {
            if (applicationUserId is not null)
            {
                Db.Users.Add(new ApplicationUser { Id = applicationUserId, UserName = applicationUserId });
            }

            var member = new Member
            {
                MemberNumber = $"M-{Guid.NewGuid():N}",
                FirstName = "Ada",
                LastName = "Lovelace",
                Email = email,
                IsActive = true,
                ApplicationUserId = applicationUserId
            };
            Db.Members.Add(member);
            await Db.SaveChangesAsync();
            return member.Id;
        }

        public IPluginActorFacade ActorFor(ClaimsPrincipal user)
            => _adapter.CreateBridge(new ActorTestModule(), user).Host.Actor;

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class ActorTestModule : IPluginModule
    {
        public PluginManifest Manifest => PluginActorFacadeTests.Manifest();

        public void RegisterContributions(IPluginContributionSink sink)
        {
        }
    }

    /// <summary>A plugin test double written before 1.16.0: it does not implement Actor.</summary>
    private sealed class ContextWrittenAgainst115 : IPluginHostContext
    {
        public IPluginMetadataFacade Metadata => throw new NotSupportedException();
        public IPluginMemberReader Members => throw new NotSupportedException();
        public IPluginMemberActionFacade MemberActions => throw new NotSupportedException();
        public IPluginDataStore Persistence => throw new NotSupportedException();
        public IPluginPermissionFacade Permissions => throw new NotSupportedException();
    }

    private sealed class DenyAllPermissions : IExtensionPermissionFacade
    {
        public Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string permissionKey,
            IReadOnlyCollection<string> declaredPermissions,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }

    private sealed class NoOpAudit : IExtensionAuditFacade
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

    private sealed class NoOpNotifications : IExtensionNotificationFacade
    {
        public Task<NotificationResult> NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default)
            => Task.FromResult(new NotificationResult(true, "none", string.Empty));
    }

    private sealed class NoOpAuditLog : IAuditLogService
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
}
