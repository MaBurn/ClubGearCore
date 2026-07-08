using System.Security.Claims;
using ClubGear.Controllers;
using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class Slice6VerificationTests
{
    [Fact]
    public async Task SelfServiceProfile_PopulatesGenericPluginSlots_AndUsesSelfServiceEndpoint()
    {
        var member = new Member { Id = 42, MemberNumber = "M-042", FirstName = "Ada", LastName = "Lovelace", IsActive = true };
        var profile = new SelfServiceProfileViewModel { FullName = "Ada Lovelace", Email = "ada@example.com", MemberLinked = true };
        var slots = new MemberPluginSlotSnapshot(
            [new MemberPluginStatusBadgeView("plugin.runtime.a", "Runtime Plugin A", new MemberStatusBadgeSlot("Aktiv", "success"), 0)],
            [new MemberPluginDetailCardView("plugin.runtime.a", "Runtime Plugin A", new MemberDetailCardSlot("vehicles", "Fahrzeuge", "1 Eintrag"), 0)],
            Array.Empty<MemberPluginEditTabView>(),
            [new MemberPluginActionView(
                "plugin.runtime.a",
                "Runtime Plugin A",
                new MemberActionSlot(
                    "sync-a",
                    "Synchronisieren",
                    "members.manage",
                    ArgumentSchema:
                    [new PluginFieldSchema("licensePlate", "Kennzeichen", PluginSchemaFieldType.Text, true, 0)]),
                0)]);

        using var sut = CreateController(
            new FakeSelfServiceFeatureService
            {
                Dashboard = new SelfServiceDashboardOutcome(false, member, true),
                Profile = new SelfServiceProfileOutcome(false, profile)
            },
            new FakeMemberPluginSlotService { SlotSnapshot = slots });

        var result = await sut.Profile();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(profile, view.Model);
        var snapshot = Assert.IsType<MemberPluginSlotSnapshot>(view.ViewData["MemberPluginSlots"]);
        Assert.Single(snapshot.Actions);
        Assert.Equal("/api/self-service/plugin-actions", view.ViewData["PluginActionEndpoint"]);
        Assert.Equal("edit-cards", view.ViewData["PluginSlotMode"]);
    }

    [Fact]
    public void SelfServiceProfileView_RendersGenericPluginHost_WithSharedPartials()
    {
        var profileContent = File.ReadAllText(GetProjectFilePath("Views", "SelfService", "Profile.cshtml"));

        Assert.Contains("data-selfservice-plugin-host", profileContent, StringComparison.Ordinal);
        Assert.Contains("~/Views/Members/_PluginSlots.cshtml", profileContent, StringComparison.Ordinal);
        Assert.Contains("~/Views/Members/_PluginActionModal.cshtml", profileContent, StringComparison.Ordinal);
        Assert.Contains("ViewData[\"PluginActionEndpoint\"] = \"/api/self-service/plugin-actions\"", profileContent, StringComparison.Ordinal);
        Assert.Contains("ViewData[\"PluginSlotMode\"] = \"edit-cards\"", profileContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Plugin-Erweiterungen", profileContent, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericHostFiles_DoNotContainCarInfoSpecificBranching()
    {
        foreach (var relativePath in new[]
                 {
                     Path.Combine("Controllers", "Member", "Member_Controller.cs"),
                     Path.Combine("Controllers", "SelfService", "SelfService_Controller.cs"),
                     Path.Combine("Controllers", "SelfService", "SelfService_API.cs"),
                     Path.Combine("Controllers", "Api", "PluginAdminCommandsController.cs"),
                     Path.Combine("Views", "Members", "_HeaderActions.cshtml"),
                     Path.Combine("Views", "Members", "_PluginActionModal.cshtml"),
                     Path.Combine("Views", "SelfService", "Profile.cshtml"),
                     Path.Combine("Views", "Admin", "Functions.cshtml"),
                     Path.Combine("wwwroot", "js", "admin-functions.js")
                 })
        {
            var content = File.ReadAllText(GetProjectFilePath(relativePath.Split(Path.DirectorySeparatorChar)));
            Assert.DoesNotContain("carinfo", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LegacyCompatibilityCoverage_RemainsPresent_ForSchemaOptionalActions()
    {
        var slotServiceTests = File.ReadAllText(GetProjectFilePath("tests", "ClubGear.ArchitectureTests", "MemberPluginSlotServiceTests.cs"));
        var contractTests = File.ReadAllText(GetProjectFilePath("tests", "ClubGear.ArchitectureTests", "PluginContractSchemaFoundationTests.cs"));

        Assert.Contains("AllowsLegacyActionDescriptors_WithoutArgumentSchema", slotServiceTests, StringComparison.Ordinal);
        Assert.Contains("DeserializesLegacyPayload_WithoutArgumentSchema", contractTests, StringComparison.Ordinal);
        Assert.Contains("DeserializesLegacyPayload_WithoutFieldErrors", contractTests, StringComparison.Ordinal);
    }

    private static SelfServiceController CreateController(ISelfServiceFeatureService selfServiceFeatureService, IMemberPluginSlotService slotService)
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

    private static string GetProjectFilePath(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var csprojPath = Path.Combine(current.FullName, "ClubGear.csproj");
            if (File.Exists(csprojPath))
            {
                return Path.Combine(new[] { current.FullName }.Concat(segments).ToArray());
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Projektwurzel mit ClubGear.csproj wurde nicht gefunden.");
    }

    private sealed class FakeSelfServiceFeatureService : ISelfServiceFeatureService
    {
        public SelfServiceDashboardOutcome Dashboard { get; set; } = new(false, null, false);

        public SelfServiceProfileOutcome Profile { get; set; } = new(false, new SelfServiceProfileViewModel());

        public Task<SelfServiceDashboardOutcome> GetDashboardAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
            => Task.FromResult(Dashboard);

        public Task<SelfServiceProfileOutcome> GetProfileAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
            => Task.FromResult(Profile);

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
