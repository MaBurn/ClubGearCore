using System.Security.Claims;
using ClubGear.Controllers;
using ClubGear.Models;
using ClubGear.Models.Feedback;
using ClubGear.Models.MemberFilters;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class MemberFeedbackFlowTests
{
    [Fact]
    public async Task Create_WithValidModel_SetsSuccessFeedbackAndRedirectsToIndex()
    {
        var fakeService = new FakeMemberFeatureService();
        using var sut = new MembersController(fakeService);
        sut.ControllerContext = BuildControllerContext(PermissionKeys.MembersManage);
        sut.TempData = BuildTempData(sut.ControllerContext.HttpContext);

        var result = await sut.Create(new Member
        {
            FirstName = "Anna",
            LastName = "Muster",
            MemberNumber = "M-100"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MembersController.Index), redirect.ActionName);
        Assert.Equal("success", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
        Assert.Equal("Mitglied wurde erfolgreich erstellt.", sut.TempData[ActionFeedbackViewModel.TempDataMessageKey]);
    }

    [Fact]
    public async Task BulkDelete_WithoutValidIds_SetsErrorFeedbackAndPreservesFilterState()
    {
        var fakeService = new FakeMemberFeatureService();
        using var sut = new MembersController(fakeService);
        sut.ControllerContext = BuildControllerContext(PermissionKeys.MembersManage);
        sut.TempData = BuildTempData(sut.ControllerContext.HttpContext);

        var result = await sut.BulkDeleteTerminatedMembers(new BulkMemberActionRequest
        {
            Search = "anna",
            Status = "inactive",
            SelectedMemberIds = new List<int> { 0, -7 }
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MembersController.Index), redirect.ActionName);
        Assert.Equal("anna", redirect.RouteValues?["search"]);
        Assert.Equal("inactive", redirect.RouteValues?["status"]);
        Assert.Equal("error", sut.TempData[ActionFeedbackViewModel.TempDataKindKey]);
        Assert.Equal("Keine gueltigen Mitglieder zum Loeschen ausgewaehlt.", sut.TempData[ActionFeedbackViewModel.TempDataMessageKey]);
    }

    [Fact]
    public async Task Import_WithoutFile_ReturnsViewWithErrorFeedback()
    {
        using var sut = new MembersController(new FakeMemberFeatureService());

        var result = await sut.Import(csvFile: null);

        var view = Assert.IsType<ViewResult>(result);
        var feedback = Assert.IsType<ActionFeedbackViewModel>(view.ViewData[ActionFeedbackViewModel.ViewDataKey]);
        Assert.Equal("error", feedback.Kind);
        Assert.False(view.ViewData.ModelState.IsValid);
    }

    [Fact]
    public void MemberViews_EmbedFeedbackAreaAndBulkFormCarriesSearchFilterState()
    {
        var indexContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "Index.cshtml"));
        var createContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "Create.cshtml"));
        var editContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "Edit.cshtml"));
        var importContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "Import.cshtml"));

        Assert.Contains("<partial name=\"_FeedbackArea\"", indexContent, StringComparison.Ordinal);
        Assert.Contains("name=\"Search\"", indexContent, StringComparison.Ordinal);
        Assert.Contains("name=\"Status\"", indexContent, StringComparison.Ordinal);
        Assert.Contains("<partial name=\"_FeedbackArea\"", createContent, StringComparison.Ordinal);
        Assert.Contains("<partial name=\"_FeedbackArea\"", editContent, StringComparison.Ordinal);
        Assert.Contains("<partial name=\"_FeedbackArea\"", importContent, StringComparison.Ordinal);
    }

    [Fact]
    public void FeedbackPartial_UsesUnifiedFeedbackModelRendering()
    {
        var partialContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "_FeedbackArea.cshtml"));

        Assert.Contains("ActionFeedbackViewModel", partialContent, StringComparison.Ordinal);
        Assert.Contains("data-feedback-kind", partialContent, StringComparison.Ordinal);
        Assert.Contains("AlertCssClass", partialContent, StringComparison.Ordinal);
    }

    private static ControllerContext BuildControllerContext(params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test-user")
        };

        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
    }

    private static ITempDataDictionary BuildTempData(HttpContext httpContext)
    {
        return new TempDataDictionary(httpContext, new TestTempDataProvider());
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

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
            => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
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
            => Task.CompletedTask;

        public Task<MemberMutationStatus> UpdateAsync(Member member, string? actor, CancellationToken cancellationToken = default)
            => Task.FromResult(MemberMutationStatus.Success);

        public Task<MemberMutationStatus> VerifyAsync(int id, string? actor, CancellationToken cancellationToken = default)
            => Task.FromResult(MemberMutationStatus.Success);

        public Task<MemberMutationStatus> DeleteAsync(int id, string? actor, CancellationToken cancellationToken = default)
            => Task.FromResult(MemberMutationStatus.Success);

        public Task<int> BulkDeleteAsync(IReadOnlyCollection<int> ids, string? actor, bool hasManagePermission, CancellationToken cancellationToken = default)
            => Task.FromResult(0);

        public Task<MembersImportResult> ImportCsvAsync(Stream csvStream, string? actor, CancellationToken cancellationToken = default)
            => Task.FromResult(new MembersImportResult(0, 0, 0, Array.Empty<string>()));
    }
}