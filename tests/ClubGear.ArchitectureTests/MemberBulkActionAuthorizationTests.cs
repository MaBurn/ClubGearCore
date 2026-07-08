using System.Security.Claims;
using ClubGear.Controllers;
using ClubGear.Models;
using ClubGear.Models.MemberFilters;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class MemberBulkActionAuthorizationTests
{
    [Fact]
    public async Task BulkDeleteTerminatedMembers_WithoutPermission_ReturnsForbidAndDoesNotCallService()
    {
        var fakeService = new FakeMemberFeatureService();
        using var sut = new MembersController(fakeService);
        sut.ControllerContext = BuildControllerContext();
        sut.TempData = BuildTempData(sut.ControllerContext.HttpContext);

        var result = await sut.BulkDeleteTerminatedMembers(new BulkMemberActionRequest
        {
            SelectedMemberIds = new List<int> { 3 }
        });

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, fakeService.BulkDeleteCallCount);
    }

    [Fact]
    public async Task BulkDeleteTerminatedMembers_WithPermission_ProcessesValidDistinctIds()
    {
        var fakeService = new FakeMemberFeatureService { DeletedCount = 1 };
        using var sut = new MembersController(fakeService);
        sut.ControllerContext = BuildControllerContext(PermissionKeys.MembersManage);
        sut.TempData = BuildTempData(sut.ControllerContext.HttpContext);

        var result = await sut.BulkDeleteTerminatedMembers(new BulkMemberActionRequest
        {
            SelectedMemberIds = new List<int> { 2, 2, -5, 0 }
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MembersController.Index), redirect.ActionName);

        Assert.Equal(1, fakeService.BulkDeleteCallCount);
        Assert.Equal(new[] { 2 }, fakeService.LastIds);
        Assert.True(fakeService.LastHasManagePermission);
    }

    [Fact]
    public void BulkSelectionPartials_ExposeCheckboxAndSelectAllMarkers()
    {
        var listSegments = File.ReadAllText(GetProjectFilePath("Views", "Members", "_ListSegments.cshtml"));
        var bulkActions = File.ReadAllText(GetProjectFilePath("Views", "Members", "_BulkActions.cshtml"));

        Assert.Contains("name=\"SelectedMemberIds\"", listSegments, StringComparison.Ordinal);
        Assert.Contains("data-member-bulk-select", listSegments, StringComparison.Ordinal);
        Assert.Contains("id=\"member-select-all\"", bulkActions, StringComparison.Ordinal);
        Assert.Contains("data-member-select-all", bulkActions, StringComparison.Ordinal);
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

    private static ITempDataDictionary BuildTempData(HttpContext httpContext)
    {
        return new TempDataDictionary(httpContext, new TestTempDataProvider());
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
        public int BulkDeleteCallCount { get; private set; }

        public IReadOnlyList<int> LastIds { get; private set; } = Array.Empty<int>();

        public bool LastHasManagePermission { get; private set; }

        public int DeletedCount { get; set; }

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
        {
            BulkDeleteCallCount++;
            LastIds = ids.OrderBy(id => id).ToArray();
            LastHasManagePermission = hasManagePermission;
            return Task.FromResult(DeletedCount);
        }

        public Task<MembersImportResult> ImportCsvAsync(Stream csvStream, string? actor, CancellationToken cancellationToken = default)
            => Task.FromResult(new MembersImportResult(0, 0, 0, Array.Empty<string>()));
    }
}
