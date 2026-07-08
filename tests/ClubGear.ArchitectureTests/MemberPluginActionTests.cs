using System.Security.Claims;
using ClubGear.Controllers.Api;
using ClubGear.Models;
using ClubGear.Models.MemberActions;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MemberPluginActionRequestModel = ClubGear.Models.MemberActions.PluginMemberActionRequest;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class MemberPluginActionTests
{
    [Fact]
    public async Task ExecutePluginAction_ReturnsOk_WhenPluginActionSucceeds()
    {
        var slotService = new FakeMemberPluginSlotService
        {
            ActionResult = new PluginMemberActionResult(true, "executed", "Aktion erfolgreich ausgefuehrt.")
        };

        var sut = new MemberApiController(new FakeMemberFeatureService(), slotService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = BuildUser()
                }
            }
        };

        var response = await sut.ExecutePluginAction(new MemberPluginActionRequestModel("plugin.runtime.a", "sync-a", 1));

        var ok = Assert.IsType<OkObjectResult>(response);
        var payload = Assert.IsType<PluginMemberActionResult>(ok.Value);
        Assert.True(payload.Success);
        Assert.Equal("sync-a", slotService.LastRequest!.ActionKey);
    }

    [Fact]
    public async Task ExecutePluginAction_Returns403_WhenPluginActionIsForbidden()
    {
        var sut = new MemberApiController(
            new FakeMemberFeatureService(),
            new FakeMemberPluginSlotService
            {
                ActionResult = new PluginMemberActionResult(false, "forbidden", "Zugriff verweigert.")
            })
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = BuildUser()
                }
            }
        };

        var response = await sut.ExecutePluginAction(new MemberPluginActionRequestModel("plugin.runtime.a", "sync-a", 1));

        var objectResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public void MemberViews_RenderPluginSlotPartials_AndPluginActionHook()
    {
        var detailsContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "Details.cshtml"));
        var editContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "Edit.cshtml"));
        var headerContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "_HeaderActions.cshtml"));
        var modalContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "_PluginActionModal.cshtml"));
        var slotPartialContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "_PluginSlots.cshtml"));

        Assert.Contains("<partial name=\"_HeaderActions\" model=\"Model\" />", detailsContent, StringComparison.Ordinal);
        Assert.Contains("<partial name=\"_PluginSlots\" model=\"pluginSlots\" />", detailsContent, StringComparison.Ordinal);
        Assert.Contains("<partial name=\"_HeaderActions\" model=\"Model\" />", editContent, StringComparison.Ordinal);
        Assert.Contains("<partial name=\"_PluginSlots\" model=\"pluginSlots\" />", editContent, StringComparison.Ordinal);
        Assert.Contains("data-plugin-member-action", headerContent, StringComparison.Ordinal);
        Assert.Contains("data-plugin-action-schema", headerContent, StringComparison.Ordinal);
        Assert.Contains("<partial name=\"_PluginActionModal\" model=\"pluginSlots\" />", headerContent, StringComparison.Ordinal);
        Assert.Contains("member-plugin-action-modal", modalContent, StringComparison.Ordinal);
        Assert.Contains("data-plugin-action-field", modalContent, StringComparison.Ordinal);
        Assert.Contains("fieldErrors", modalContent, StringComparison.Ordinal);
        Assert.Contains("bootstrap.Modal", modalContent, StringComparison.Ordinal);
        Assert.Contains("getModalInstance()?.show()", modalContent, StringComparison.Ordinal);
        Assert.Contains("const actionEndpoint = '@actionEndpoint';", modalContent, StringComparison.Ordinal);
        Assert.Contains("fetch(actionEndpoint", modalContent, StringComparison.Ordinal);
        Assert.Contains("if (memberId)", modalContent, StringComparison.Ordinal);
        Assert.Contains("data-plugin-slot=\"detail-cards\"", slotPartialContent, StringComparison.Ordinal);
        Assert.Contains("data-plugin-slot=\"edit-tabs\"", slotPartialContent, StringComparison.Ordinal);
        Assert.Contains("data-plugin-slot=\"edit-cards\"", slotPartialContent, StringComparison.Ordinal);
    }

    private static ClaimsPrincipal BuildUser()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "api-tester")], "TestAuth"));

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

    private sealed class FakeMemberPluginSlotService : IMemberPluginSlotService
    {
        public PluginMemberActionResult ActionResult { get; set; } = new(false, "not-configured", "Kein Ergebnis konfiguriert.");

        public MemberPluginActionRequestModel? LastRequest { get; private set; }

        public Task<MemberPluginSlotSnapshot> GetSlotsAsync(Member member, ClaimsPrincipal user, CancellationToken cancellationToken = default)
            => Task.FromResult(MemberPluginSlotSnapshot.Empty);

        public Task<PluginMemberActionResult> ExecuteActionAsync(MemberPluginActionRequestModel request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(ActionResult);
        }
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
}
