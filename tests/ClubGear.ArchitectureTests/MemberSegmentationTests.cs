using ClubGear.Controllers;
using ClubGear.Models;
using ClubGear.Models.MemberFilters;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class MemberSegmentationTests
{
    [Fact]
    public async Task MembersIndex_Get_MapsFilteredMembersToActiveInactiveAndSpecialSegments()
    {
        var members = new[]
        {
            new Member { Id = 1, MemberNumber = "M-001", FirstName = "Anna", LastName = "Aktiv", IsActive = true },
            new Member { Id = 2, MemberNumber = "M-002", FirstName = "Ingo", LastName = "Inaktiv", IsActive = false },
            new Member
            {
                Id = 3,
                MemberNumber = "M-003",
                FirstName = "Clara",
                LastName = "Club",
                IsActive = true,
                MembershipType = new MembershipType { Id = 2, Key = "Verein", Name = "Verein" }
            }
        };

        using var sut = new MembersController(new FakeMemberFeatureService { Members = members });

        var result = await sut.Index(status: "active");

        var view = Assert.IsType<ViewResult>(result);
        var listSegments = Assert.IsType<MemberListSegmentsViewModel>(view.ViewData["MemberListSegments"]);

        Assert.Equal(2, listSegments.ActiveMembers.Count);
        Assert.Empty(listSegments.InactiveMembers);
        Assert.Single(listSegments.SpecialMembers);
        Assert.Equal(3, listSegments.SpecialMembers[0].Id);
    }

    [Fact]
    public void ListSegmentsPartial_DefinesEmptyStatesForAllSegments()
    {
        var content = File.ReadAllText(GetProjectFilePath("Views", "Members", "_ListSegments.cshtml"));

        Assert.Contains("data-segment-empty=\"active\"", content, StringComparison.Ordinal);
        Assert.Contains("data-segment-empty=\"inactive\"", content, StringComparison.Ordinal);
        Assert.Contains("data-segment-empty=\"special\"", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Standard", false, false)]
    [InlineData("Standard", true, true)]
    [InlineData("Verein", false, true)]
    [InlineData("Verein", true, true)]
    [InlineData("Firma", false, true)]
    [InlineData("Familie", false, true)]
    [InlineData("Foerderer", false, true)]
    public void FromMembers_ClassifiesSpecialMembers_AcrossSeededMitgliedsartenAndDeceasedFlag(
        string membershipTypeKey,
        bool isDeceased,
        bool expectedSpecial)
    {
        var member = new Member
        {
            Id = 1,
            MemberNumber = "M-001",
            FirstName = "Test",
            LastName = "Member",
            IsActive = true,
            IsDeceased = isDeceased,
            MembershipType = new MembershipType { Id = 1, Key = membershipTypeKey, Name = membershipTypeKey }
        };

        var segments = MemberListSegmentsViewModel.FromMembers(new[] { member });

        Assert.Equal(expectedSpecial, segments.SpecialMembers.Contains(member));
    }

    [Fact]
    public void ListSegmentsPartial_RendersMetadataValueLabelsForEachSegment()
    {
        var content = File.ReadAllText(GetProjectFilePath("Views", "Members", "_ListSegments.cshtml"));

        Assert.Contains("ResolveMetadataValues", content, StringComparison.Ordinal);
        Assert.Contains("metadataValue.Field!.Label", content, StringComparison.Ordinal);
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
}
