using System.Reflection;
using ClubGear.Controllers;
using ClubGear.Controllers.Api;
using ClubGear.Models;
using ClubGear.Models.MemberFilters;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class MemberSearchFilterFlowTests
{
    [Fact]
    public void MembersIndex_Post_RedirectsWithNormalizedFilterState()
    {
        using var sut = new MembersController(new FakeMemberFeatureService());

        var result = sut.Index(new MemberSearchFilterViewModel
        {
            Search = "  anna@example.org ",
            Status = "inactive"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(MembersController.Index), redirect.ActionName);
        Assert.Equal("anna@example.org", redirect.RouteValues?["search"]);
        Assert.Equal("inactive", redirect.RouteValues?["status"]);
    }

    [Fact]
    public async Task MembersIndex_Get_PopulatesFilterStateAndAppliesStatusFilter()
    {
        var members = new[]
        {
            new Member { Id = 1, MemberNumber = "M-001", FirstName = "Anna", LastName = "Aktiv", IsActive = true },
            new Member { Id = 2, MemberNumber = "M-002", FirstName = "Ingo", LastName = "Inaktiv", IsActive = false }
        };

        using var sut = new MembersController(new FakeMemberFeatureService { Members = members });

        var result = await sut.Index(search: "Anna", status: "inactive");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<IEnumerable<Member>>(view.Model);
        var list = model.ToList();

        Assert.Single(list);
        Assert.False(list[0].IsActive);

        var searchFilter = Assert.IsType<MemberSearchFilterViewModel>(view.ViewData["SearchFilter"]);
        Assert.Equal("Anna", searchFilter.Search);
        Assert.Equal("inactive", searchFilter.Status);
    }

    [Fact]
    public async Task MemberApi_GetList_KeepsLegacyContractWhenNoStatusProvided()
    {
        var members = new[]
        {
            new Member { Id = 1, MemberNumber = "M-001", FirstName = "Anna", LastName = "Aktiv", IsActive = true },
            new Member { Id = 2, MemberNumber = "M-002", FirstName = "Ingo", LastName = "Inaktiv", IsActive = false }
        };

        var sut = new MemberApiController(new FakeMemberFeatureService { Members = members });

        var result = await sut.GetList(search: "Anna");

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<Member>>(ok.Value);
        Assert.Equal(2, payload.Count);

        var route = typeof(MemberApiController).GetCustomAttribute<RouteAttribute>();
        Assert.Equal("api/member", route?.Template);
    }

    [Fact]
    public async Task MemberApi_GetList_AppliesStatusFilterWhenProvided()
    {
        var members = new[]
        {
            new Member { Id = 1, MemberNumber = "M-001", FirstName = "Anna", LastName = "Aktiv", IsActive = true },
            new Member { Id = 2, MemberNumber = "M-002", FirstName = "Ingo", LastName = "Inaktiv", IsActive = false }
        };

        var sut = new MemberApiController(new FakeMemberFeatureService { Members = members });

        var result = await sut.GetList(status: "active");

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsAssignableFrom<IEnumerable<Member>>(ok.Value).ToList();

        Assert.Single(payload);
        Assert.True(payload[0].IsActive);
    }

    private sealed class FakeMemberFeatureService : IMemberFeatureService
    {
        public IReadOnlyList<Member> Members { get; set; } = Array.Empty<Member>();

        public Task<IReadOnlyList<Member>> GetListAsync(string? search = null, CancellationToken cancellationToken = default)
            => Task.FromResult(Members);

        public Task<IReadOnlyList<Member>> GetInactiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Member>>(Members.Where(m => !m.IsActive).ToList());

        public Task<Member?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(Members.FirstOrDefault(m => m.Id == id));

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