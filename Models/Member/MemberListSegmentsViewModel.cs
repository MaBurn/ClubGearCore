using ClubGear.Models;

namespace ClubGear.Models.MemberFilters;

public sealed class MemberListSegmentsViewModel
{
    public IReadOnlyList<Member> ActiveMembers { get; init; } = Array.Empty<Member>();

    public IReadOnlyList<Member> InactiveMembers { get; init; } = Array.Empty<Member>();

    public IReadOnlyList<Member> SpecialMembers { get; init; } = Array.Empty<Member>();

    public int TotalCount => ActiveMembers.Count + InactiveMembers.Count;

    public static MemberListSegmentsViewModel FromMembers(IEnumerable<Member> members)
    {
        var materialized = members.ToList();

        return new MemberListSegmentsViewModel
        {
            ActiveMembers = materialized.Where(m => m.IsActive).ToList(),
            InactiveMembers = materialized.Where(m => !m.IsActive).ToList(),
            SpecialMembers = materialized.Where(IsSpecialMember).ToList()
        };
    }

    private static bool IsSpecialMember(Member member)
    {
        var membershipTypeKey = member.MembershipType?.Key ?? "Standard";
        return membershipTypeKey != "Standard" || member.IsDeceased;
    }
}