using ClubGear.Models;
using ClubGear.Models.MemberFilters;

namespace ClubGear.Services.Abstractions;

public sealed record MembersImportResult(
    int Created,
    int Updated,
    int Skipped,
    IReadOnlyList<string> Errors);

public sealed record MemberReferenceOption(int Id, string Label);

public enum MemberMutationStatus
{
    Success,
    NotFound
}

public interface IMemberFeatureService
{
    Task<IReadOnlyList<Member>> GetListAsync(string? search = null, CancellationToken cancellationToken = default);

    MemberListSegmentsViewModel BuildListSegments(IReadOnlyList<Member> members)
    {
        return MemberListSegmentsViewModel.FromMembers(members);
    }

    MemberHierarchyViewModel BuildHierarchy(IEnumerable<Member> members)
    {
        return MemberHierarchyViewModel.FromMembers(members);
    }

    Task<IReadOnlyList<Member>> GetInactiveAsync(CancellationToken cancellationToken = default);

    Task<Member?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MemberReferenceOption>> SearchForReferenceAsync(string? query, int limit = 10, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<MemberReferenceOption>>(Array.Empty<MemberReferenceOption>());
    }

    Task<IReadOnlyDictionary<int, string>> GetReferenceLabelsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyDictionary<int, string>>(new Dictionary<int, string>());
    }

    Task CreateAsync(Member member, string? actor, CancellationToken cancellationToken = default);

    Task<MemberMutationStatus> UpdateAsync(Member member, string? actor, CancellationToken cancellationToken = default);

    Task<MemberMutationStatus> VerifyAsync(int id, string? actor, CancellationToken cancellationToken = default);

    Task<MemberMutationStatus> DeleteAsync(int id, string? actor, CancellationToken cancellationToken = default);

    Task<int> BulkDeleteAsync(IReadOnlyCollection<int> ids, string? actor, bool hasManagePermission, CancellationToken cancellationToken = default);

    Task<MembersImportResult> ImportCsvAsync(Stream csvStream, string? actor, CancellationToken cancellationToken = default);
}
