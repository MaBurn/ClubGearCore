using System.Security.Claims;
using ClubGear.Controllers.Api;
using ClubGear.Models;
using ClubGear.Models.MemberActions;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using MemberPluginActionRequestModel = ClubGear.Models.MemberActions.PluginMemberActionRequest;

namespace ClubGear.ArchitectureTests;

public sealed class SelfServicePluginActionApiTests
{
    [Fact]
    public async Task GetPluginSlots_ReturnsSnapshot_WhenMemberIsLinked()
    {
        var member = new Member { Id = 42, MemberNumber = "M-042", FirstName = "Ada", LastName = "Lovelace", IsActive = true };
        var slotService = new FakeMemberPluginSlotService
        {
            SlotSnapshot = new MemberPluginSlotSnapshot(
                Array.Empty<MemberPluginStatusBadgeView>(),
                Array.Empty<MemberPluginDetailCardView>(),
                Array.Empty<MemberPluginEditTabView>(),
                [
                    new MemberPluginActionView(
                        "plugin.runtime.a",
                        "Runtime Plugin A",
                        new MemberActionSlot("sync-a", "Synchronisieren", "members.manage"),
                        0)
                ])
        };

        var sut = CreateController(new FakeSelfServiceFeatureService { Dashboard = new SelfServiceDashboardOutcome(false, member, true) }, slotService);

        var response = await sut.GetPluginSlots();

        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<MemberPluginSlotSnapshot>(ok.Value);
        Assert.Single(payload.Actions);
    }

    [Fact]
    public async Task ExecutePluginAction_UsesLinkedMemberId_ForDispatch()
    {
        var member = new Member { Id = 7, MemberNumber = "M-007", FirstName = "Grace", LastName = "Hopper", IsActive = true };
        var slotService = new FakeMemberPluginSlotService
        {
            ActionResult = new PluginMemberActionResult(true, "executed", "ok")
        };

        var sut = CreateController(new FakeSelfServiceFeatureService { Dashboard = new SelfServiceDashboardOutcome(false, member, true) }, slotService);

        var response = await sut.ExecutePluginAction(new SelfServicePluginActionRequest("plugin.runtime.a", "sync-a"));

        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<PluginMemberActionResult>(ok.Value);
        Assert.True(payload.Success);
        Assert.NotNull(slotService.LastActionRequest);
        Assert.Equal(7, slotService.LastActionRequest!.MemberId);
    }

    [Fact]
    public async Task ExecutePluginAction_ReturnsNotFound_WhenNoMemberIsLinked()
    {
        var sut = CreateController(
            new FakeSelfServiceFeatureService { Dashboard = new SelfServiceDashboardOutcome(false, null, false) },
            new FakeMemberPluginSlotService());

        var response = await sut.ExecutePluginAction(new SelfServicePluginActionRequest("plugin.runtime.a", "sync-a"));

        var notFound = Assert.IsType<NotFoundObjectResult>(response);
        var payload = Assert.IsType<PluginMemberActionResult>(notFound.Value);
        Assert.Equal("member-not-linked", payload.Status);
    }

    private static SelfServiceApiController CreateController(ISelfServiceFeatureService selfServiceFeatureService, IMemberPluginSlotService slotService)
    {
        return new SelfServiceApiController(selfServiceFeatureService, slotService, new FakeSelfServiceSectionService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "selfservice-tester")], "TestAuth"))
                }
            }
        };
    }

    private sealed class FakeSelfServiceFeatureService : ISelfServiceFeatureService
    {
        public SelfServiceDashboardOutcome Dashboard { get; set; } = new(false, null, false);

        public Task<SelfServiceDashboardOutcome> GetDashboardAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
            => Task.FromResult(Dashboard);

        public Task<SelfServiceProfileOutcome> GetProfileAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SelfServiceProfileUpdateOutcome> UpdateProfileAsync(ClaimsPrincipal principal, SelfServiceProfileViewModel model, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SelfServiceProfileImageOutcome> UploadProfileImageAsync(ClaimsPrincipal principal, string fileName, string contentType, Stream content, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SelfServiceProfileImageOutcome> DeleteProfileImageAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeMemberPluginSlotService : IMemberPluginSlotService
    {
        public MemberPluginSlotSnapshot SlotSnapshot { get; set; } = MemberPluginSlotSnapshot.Empty;

        public PluginMemberActionResult ActionResult { get; set; } = new(false, "not-configured");

        public MemberPluginActionRequestModel? LastActionRequest { get; private set; }

        public Task<MemberPluginSlotSnapshot> GetSlotsAsync(Member member, ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult(SlotSnapshot);

        public Task<PluginMemberActionResult> ExecuteActionAsync(MemberPluginActionRequestModel request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            LastActionRequest = request;
            return Task.FromResult(ActionResult);
        }
    }

    private sealed class FakeSelfServiceSectionService : ISelfServiceSectionService
    {
        public Task<IReadOnlyList<SelfServicePluginSectionView>> GetSelfServiceSectionsAsync(Member member, ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SelfServicePluginSectionView>>(Array.Empty<SelfServicePluginSectionView>());

        public Task<PluginMemberActionResult> ExecuteSelfServiceActionAsync(SelfServiceSectionActionRequest request, Member member, ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginMemberActionResult(false, "not-configured"));
    }
}
