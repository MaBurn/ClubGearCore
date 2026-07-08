using System.Runtime.Loader;
using System.Security.Claims;
using ClubGear.Controllers;
using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using ClubGear.Services.Core;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class MemberPluginSlotServiceTests
{
    [Fact]
    public async Task GetSlotsAsync_ReturnsOrderedCardsTabsBadgesAndActions_FromRegistry()
    {
        var registry = new PluginRegistry();
        RegisterRuntime(registry, new Plugins.RuntimeLoadedPluginModuleA());
        RegisterRuntime(registry, new Plugins.RuntimeLoadedPluginModuleB());

        var memberService = new FakeMemberFeatureService();
        var slotService = new MemberPluginSlotService(
            registry,
            CreateRuntimeAdapter(memberService),
            memberService,
            NullLogger<MemberPluginSlotService>.Instance);

        var result = await slotService.GetSlotsAsync(memberService.Member, BuildUser(PermissionKeys.MembersManage));

        Assert.Equal(2, result.StatusBadges.Count);
        Assert.Equal(2, result.DetailCards.Count);
        Assert.Equal(2, result.EditTabs.Count);
        Assert.Equal(2, result.Actions.Count);
        Assert.Equal("plugin.runtime.a", result.StatusBadges[0].ModuleId);
        Assert.Equal("Plugin Karte A", result.DetailCards[0].Card.Title);
        Assert.Equal("Plugin Tab A", result.EditTabs[0].Tab.Title);
        Assert.Equal("Synchronisieren A", result.Actions[0].Action.Label);
    }

    [Fact]
    public async Task GetSlotsAsync_ReturnsEmptyAfterPluginDeactivation()
    {
        var registry = new PluginRegistry();
        var module = new Plugins.RuntimeLoadedPluginModuleA();
        RegisterRuntime(registry, module);

        var memberService = new FakeMemberFeatureService();
        var slotService = new MemberPluginSlotService(
            registry,
            CreateRuntimeAdapter(memberService),
            memberService,
            NullLogger<MemberPluginSlotService>.Instance);

        registry.Unregister(module.Manifest.ModuleId);

        var result = await slotService.GetSlotsAsync(memberService.Member, BuildUser(PermissionKeys.MembersManage));

        Assert.Same(MemberPluginSlotSnapshot.Empty.StatusBadges.GetType(), result.StatusBadges.GetType());
        Assert.Empty(result.StatusBadges);
        Assert.Empty(result.DetailCards);
        Assert.Empty(result.EditTabs);
        Assert.Empty(result.Actions);
    }

    [Fact]
    public async Task GetSlotsAsync_HidesActions_WhenPermissionIsMissing()
    {
        var registry = new PluginRegistry();
        RegisterRuntime(registry, new Plugins.RuntimeLoadedPluginModuleA());

        var memberService = new FakeMemberFeatureService();
        var slotService = new MemberPluginSlotService(
            registry,
            CreateRuntimeAdapter(memberService),
            memberService,
            NullLogger<MemberPluginSlotService>.Instance);

        var result = await slotService.GetSlotsAsync(memberService.Member, BuildUser());

        Assert.Empty(result.Actions);
    }

    [Fact]
    public async Task MembersController_Details_StoresResolvedPluginSlotsInViewData()
    {
        var registry = new PluginRegistry();
        RegisterRuntime(registry, new Plugins.RuntimeLoadedPluginModuleA());

        var memberService = new FakeMemberFeatureService();
        var slotService = new MemberPluginSlotService(
            registry,
            CreateRuntimeAdapter(memberService),
            memberService,
            NullLogger<MemberPluginSlotService>.Instance);

        using var sut = new MembersController(memberService, slotService);
        sut.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = BuildUser(PermissionKeys.MembersManage) } };

        var result = await sut.Details(1);

        var view = Assert.IsType<ViewResult>(result);
        var slots = Assert.IsType<MemberPluginSlotSnapshot>(view.ViewData["MemberPluginSlots"]);
        Assert.NotEmpty(slots.DetailCards);
        Assert.NotEmpty(slots.Actions);
    }

    [Fact]
    public async Task ExecuteActionAsync_ReturnsSuccess_ForRegisteredPluginAction()
    {
        var registry = new PluginRegistry();
        RegisterRuntime(registry, new Plugins.RuntimeLoadedPluginModuleA());

        var memberService = new FakeMemberFeatureService();
        var slotService = new MemberPluginSlotService(
            registry,
            CreateRuntimeAdapter(memberService),
            memberService,
            NullLogger<MemberPluginSlotService>.Instance);

        var result = await slotService.ExecuteActionAsync(
            new Models.MemberActions.PluginMemberActionRequest("plugin.runtime.a", "sync-a", 1),
            BuildUser(PermissionKeys.MembersManage));

        Assert.True(result.Success);
        Assert.Equal("executed", result.Status);
    }

    [Fact]
    public async Task ExecuteActionAsync_AllowsLegacyActionDescriptors_WithoutArgumentSchema()
    {
        var registry = new PluginRegistry();
        RegisterRuntime(registry, new Plugins.RuntimeLoadedPluginModuleA());

        var memberService = new FakeMemberFeatureService();
        var slotService = new MemberPluginSlotService(
            registry,
            CreateRuntimeAdapter(memberService),
            memberService,
            NullLogger<MemberPluginSlotService>.Instance);

        var slots = await slotService.GetSlotsAsync(memberService.Member, BuildUser(PermissionKeys.MembersManage));
        var action = Assert.Single(slots.Actions.Where(candidate => candidate.Action.Key == "sync-a"));
        Assert.Null(action.Action.ArgumentSchema);

        var result = await slotService.ExecuteActionAsync(
            new Models.MemberActions.PluginMemberActionRequest("plugin.runtime.a", "sync-a", 1, null),
            BuildUser(PermissionKeys.MembersManage));

        Assert.True(result.Success);
        Assert.Equal("executed", result.Status);
    }

    [Fact]
    public async Task ExecuteActionAsync_ReturnsForbidden_WhenPermissionIsMissing()
    {
        var registry = new PluginRegistry();
        RegisterRuntime(registry, new Plugins.RuntimeLoadedPluginModuleA());

        var memberService = new FakeMemberFeatureService();
        var slotService = new MemberPluginSlotService(
            registry,
            CreateRuntimeAdapter(memberService),
            memberService,
            NullLogger<MemberPluginSlotService>.Instance);

        var result = await slotService.ExecuteActionAsync(
            new Models.MemberActions.PluginMemberActionRequest("plugin.runtime.a", "sync-a", 1),
            BuildUser());

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.Status);
    }

    private static PluginRuntimeAdapter CreateRuntimeAdapter(FakeMemberFeatureService memberService)
    {
        return new PluginRuntimeAdapter(
            new ClaimsBackedPermissionFacade(),
            new NoOpAuditFacade(),
            new NoOpNotificationFacade(),
            memberService,
            NullLogger<PluginRuntimeAdapter>.Instance);
    }

    private static ClaimsPrincipal BuildUser(params string[] permissions)
    {
        var claims = permissions.Select(permission => new Claim("permission", permission)).ToList();
        claims.Add(new Claim(ClaimTypes.Name, "slot-tester"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
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

    private sealed class ClaimsBackedPermissionFacade : IExtensionPermissionFacade
    {
        public Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string permissionKey,
            IReadOnlyCollection<string> declaredPermissions,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                declaredPermissions.Contains(permissionKey, StringComparer.OrdinalIgnoreCase)
                && user.Claims.Any(claim => claim.Type == "permission" && string.Equals(claim.Value, permissionKey, StringComparison.OrdinalIgnoreCase)));
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
}
