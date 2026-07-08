using System.Runtime.Loader;
using System.Security.Claims;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using ClubGear.Services.Core;
using ClubGear.Services.Plugins.Runtime;
using ClubGear.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 2 — View model projection: MemberPluginEditTabView GroupKey/GroupTitle
/// Verifies that CollectEditTabsAsync copies GroupKey and GroupTitle from each
/// MemberEditTabSlot into the corresponding MemberPluginEditTabView.
/// </summary>
public sealed class MemberPluginEditTabViewGroupTests
{
    // -----------------------------------------------------------------------
    // Test: slot with GroupKey/GroupTitle produces view with same values
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CollectEditTabsAsync_CopiesGroupKeyAndGroupTitle_WhenSlotHasGroupProperties()
    {
        var registry = new PluginRegistry();
        RegisterRuntime(registry, new GroupedEditTabPluginModule());

        var memberService = new StubMemberFeatureService();
        var slotService = new MemberPluginSlotService(
            registry,
            CreateRuntimeAdapter(memberService),
            memberService,
            NullLogger<MemberPluginSlotService>.Instance);

        var result = await slotService.GetSlotsAsync(memberService.Member, BuildUser());

        Assert.Single(result.EditTabs);
        var view = result.EditTabs[0];
        Assert.Equal("fahrzeuge", view.GroupKey);
        Assert.Equal("Fahrzeuge", view.GroupTitle);
    }

    // -----------------------------------------------------------------------
    // Test: slot with null group properties produces view with null group properties
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CollectEditTabsAsync_ProducesNullGroupProperties_WhenSlotHasNoGroupProperties()
    {
        var registry = new PluginRegistry();
        RegisterRuntime(registry, new UngroupedEditTabPluginModule());

        var memberService = new StubMemberFeatureService();
        var slotService = new MemberPluginSlotService(
            registry,
            CreateRuntimeAdapter(memberService),
            memberService,
            NullLogger<MemberPluginSlotService>.Instance);

        var result = await slotService.GetSlotsAsync(memberService.Member, BuildUser());

        Assert.Single(result.EditTabs);
        var view = result.EditTabs[0];
        Assert.Null(view.GroupKey);
        Assert.Null(view.GroupTitle);
    }

    // -----------------------------------------------------------------------
    // Test: MemberPluginEditTabView positional construction still works (arity unchanged)
    // -----------------------------------------------------------------------

    [Fact]
    public void MemberPluginEditTabView_PositionalConstructor_StillWorksWithFourArgs()
    {
        var slot = new MemberEditTabSlot("key", "title", "content", 0);
        var view = new MemberPluginEditTabView("module.id", "Plugin Name", slot, 10);

        Assert.Equal("module.id", view.ModuleId);
        Assert.Equal("Plugin Name", view.PluginDisplayName);
        Assert.Same(slot, view.Tab);
        Assert.Equal(10, view.SortOrder);
        Assert.Null(view.GroupKey);
        Assert.Null(view.GroupTitle);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static PluginRuntimeAdapter CreateRuntimeAdapter(StubMemberFeatureService memberService)
        => new PluginRuntimeAdapter(
            new AlwaysAllowPermissionFacade(),
            new NoOpAuditFacade(),
            new NoOpNotificationFacade(),
            memberService,
            NullLogger<PluginRuntimeAdapter>.Instance);

    private static ClaimsPrincipal BuildUser()
        => new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "group-tab-tester")],
            "TestAuth"));

    private static void RegisterRuntime(PluginRegistry registry, IPluginModule module)
    {
        var sink = new MinimalContributionSink();
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

    // -----------------------------------------------------------------------
    // Plugin module: returns a slot with GroupKey="fahrzeuge", GroupTitle="Fahrzeuge"
    // -----------------------------------------------------------------------

    private sealed class GroupedEditTabPluginModule : IPluginModule
    {
        public GroupedEditTabPluginModule()
        {
            Manifest = new PluginManifest(
                "plugin.test.grouped-tab",
                "Grouped Tab Plugin",
                new Version(1, 0, 0),
                "Tests",
                "Proprietary",
                typeof(GroupedEditTabPluginModule).FullName!,
                ">=1.0.0",
                [],
                ["member.edit"]);
        }

        public PluginManifest Manifest { get; }

        public void RegisterContributions(IPluginContributionSink sink)
            => sink.AddMemberProvider(new PluginMemberProviderContribution(
                PluginMemberSlotKind.EditTab,
                typeof(GroupedEditTabProvider).FullName!,
                0));
    }

    private sealed class GroupedEditTabProvider : IMemberEditTabProvider
    {
        public Task<IReadOnlyList<MemberEditTabSlot>> GetTabsAsync(
            PluginMemberDetail member,
            IPluginHostContext hostContext,
            CancellationToken cancellationToken = default)
        {
            var slot = new MemberEditTabSlot("fahrzeuge-tab", "Fahrzeuge Tab", "Fahrzeuginhalt", 0)
            {
                GroupKey = "fahrzeuge",
                GroupTitle = "Fahrzeuge",
            };
            return Task.FromResult<IReadOnlyList<MemberEditTabSlot>>([slot]);
        }
    }

    // -----------------------------------------------------------------------
    // Plugin module: returns a slot with null group properties
    // -----------------------------------------------------------------------

    private sealed class UngroupedEditTabPluginModule : IPluginModule
    {
        public UngroupedEditTabPluginModule()
        {
            Manifest = new PluginManifest(
                "plugin.test.ungrouped-tab",
                "Ungrouped Tab Plugin",
                new Version(1, 0, 0),
                "Tests",
                "Proprietary",
                typeof(UngroupedEditTabPluginModule).FullName!,
                ">=1.0.0",
                [],
                ["member.edit"]);
        }

        public PluginManifest Manifest { get; }

        public void RegisterContributions(IPluginContributionSink sink)
            => sink.AddMemberProvider(new PluginMemberProviderContribution(
                PluginMemberSlotKind.EditTab,
                typeof(UngroupedEditTabProvider).FullName!,
                0));
    }

    private sealed class UngroupedEditTabProvider : IMemberEditTabProvider
    {
        public Task<IReadOnlyList<MemberEditTabSlot>> GetTabsAsync(
            PluginMemberDetail member,
            IPluginHostContext hostContext,
            CancellationToken cancellationToken = default)
        {
            var slot = new MemberEditTabSlot("solo-tab", "Solo Tab", "Solo Inhalt", 0);
            return Task.FromResult<IReadOnlyList<MemberEditTabSlot>>([slot]);
        }
    }

    // -----------------------------------------------------------------------
    // Stubs and fakes
    // -----------------------------------------------------------------------

    private sealed class MinimalContributionSink : IPluginContributionSink
    {
        private readonly List<PluginRouteContribution> _routes = [];
        private readonly List<PluginServiceContribution> _services = [];
        private readonly List<PluginMemberProviderContribution> _memberProviders = [];
        private readonly List<PluginBackgroundJobContribution> _backgroundJobs = [];
        private readonly List<PluginNavEntry> _navEntries = [];

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

    private sealed class StubMemberFeatureService : IMemberFeatureService
    {
        public Member Member { get; } = new()
        {
            Id = 42,
            MemberNumber = "M-042",
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.org",
            PhoneNumber = "+49-0",
            IsActive = true,
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

    private sealed class AlwaysAllowPermissionFacade : IExtensionPermissionFacade
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
}
