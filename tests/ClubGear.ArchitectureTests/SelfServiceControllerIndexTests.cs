using System.Security.Claims;
using ClubGear.Controllers;
using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class SelfServiceControllerIndexTests
{
    [Fact]
    public async Task Index_LinkedMember_SetsAllThreeViewDataKeys()
    {
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M-001",
            FirstName = "Test",
            LastName = "User",
            IsActive = true
        };

        var snapshot = new MemberPluginSlotSnapshot(
            Array.Empty<MemberPluginStatusBadgeView>(),
            [new MemberPluginDetailCardView("plugin.test", "Test Plugin", new MemberDetailCardSlot("key", "Title", "Value"), 0)],
            Array.Empty<MemberPluginEditTabView>(),
            Array.Empty<MemberPluginActionView>());

        using var sut = CreateController(
            new FakeSelfServiceFeatureService
            {
                Dashboard = new SelfServiceDashboardOutcome(false, member, true)
            },
            new FakeMemberPluginSlotService { SlotSnapshot = snapshot });

        var result = await sut.Index();

        var view = Assert.IsType<ViewResult>(result);
        var returnedSnapshot = Assert.IsType<MemberPluginSlotSnapshot>(view.ViewData["MemberPluginSlots"]);
        Assert.Single(returnedSnapshot.DetailCards);
        Assert.Equal("/api/self-service/plugin-actions", view.ViewData["PluginActionEndpoint"]);
        Assert.Equal("details", view.ViewData["PluginSlotMode"]);
    }

    [Fact]
    public async Task Index_UnlinkedMember_DoesNotSetPluginViewDataKeys()
    {
        using var sut = CreateController(
            new FakeSelfServiceFeatureService
            {
                Dashboard = new SelfServiceDashboardOutcome(false, null, false)
            },
            new FakeMemberPluginSlotService());

        var result = await sut.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.False(view.ViewData.ContainsKey("MemberPluginSlots"));
        Assert.False(view.ViewData.ContainsKey("PluginActionEndpoint"));
        Assert.False(view.ViewData.ContainsKey("PluginSlotMode"));
    }

    private static SelfServiceController CreateController(
        ISelfServiceFeatureService selfServiceFeatureService,
        IMemberPluginSlotService slotService)
    {
        return new SelfServiceController(selfServiceFeatureService, slotService, new FakeSelfServiceSectionService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };
    }

    private sealed class FakeSelfServiceSectionService : ISelfServiceSectionService
    {
        public Task<IReadOnlyList<SelfServicePluginSectionView>> GetSelfServiceSectionsAsync(Member member, ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SelfServicePluginSectionView>>(Array.Empty<SelfServicePluginSectionView>());

        public Task<PluginMemberActionResult> ExecuteSelfServiceActionAsync(SelfServiceSectionActionRequest request, Member member, ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
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

        public Task<MemberPluginSlotSnapshot> GetSlotsAsync(Member member, ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult(SlotSnapshot);

        public Task<PluginMemberActionResult> ExecuteActionAsync(Models.MemberActions.PluginMemberActionRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
