using System.Globalization;
using ClubGear.Models;

namespace ClubGear.Models.MemberFilters;

/// <summary>
/// A single flattened row in the Members overview hierarchy. Parent (container) members
/// appear at <see cref="Depth"/> 0 and are immediately followed by their sub-members at
/// <see cref="Depth"/> 1. Every row in the same group shares the container's
/// <see cref="GroupId"/> and <see cref="GroupType"/>; sub-member rows additionally carry the
/// container type's <see cref="GroupLabel"/> (e.g. "Mitarbeiter").
/// </summary>
public sealed record MemberHierarchyRow(
    Member Member,
    int Depth,
    int GroupId,
    string? GroupLabel,
    string GroupType,
    bool HasSubMembers);

/// <summary>
/// Read model for the Members overview. Produces an ordered, flattened list of
/// <see cref="MemberHierarchyRow"/> where every container parent is immediately followed by
/// its indented sub-members, each member rendered exactly once. The parent link is derived
/// purely from the already eager-loaded <see cref="MemberMetadataValue"/> graph — no extra query.
/// </summary>
public sealed class MemberHierarchyViewModel
{
    private const string UnassignedTypeKey = "unassigned";

    public IReadOnlyList<MemberHierarchyRow> Rows { get; init; } = Array.Empty<MemberHierarchyRow>();

    public static MemberHierarchyViewModel FromMembers(IEnumerable<Member> members)
    {
        var materialized = members?.ToList() ?? new List<Member>();
        return new MemberHierarchyViewModel { Rows = BuildHierarchy(materialized) };
    }

    private static IReadOnlyList<MemberHierarchyRow> BuildHierarchy(IReadOnlyList<Member> members)
    {
        var byId = new Dictionary<int, Member>();
        foreach (var member in members)
        {
            byId[member.Id] = member;
        }

        // Resolve each member's container parent (or null) once, from in-memory data only.
        var parentOf = new Dictionary<int, Member?>();
        foreach (var member in members)
        {
            parentOf[member.Id] = ResolveContainerParent(member, byId);
        }

        // Group children by their resolved container parent id.
        var childrenByParent = members
            .Where(m => parentOf[m.Id] is not null)
            .GroupBy(m => parentOf[m.Id]!.Id)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(m => m.LastName, StringComparer.OrdinalIgnoreCase)
                      .ThenBy(m => m.FirstName, StringComparer.OrdinalIgnoreCase)
                      .ToList());

        var rows = new List<MemberHierarchyRow>();
        var emitted = new HashSet<int>();

        var topLevel = members
            .Where(m => parentOf[m.Id] is null)
            .OrderBy(m => m.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.FirstName, StringComparer.OrdinalIgnoreCase);

        foreach (var parent in topLevel)
        {
            var parentTypeKey = parent.MembershipType?.Key ?? UnassignedTypeKey;
            var hasChildren = childrenByParent.TryGetValue(parent.Id, out var children) && children.Count > 0;

            rows.Add(new MemberHierarchyRow(parent, 0, parent.Id, null, parentTypeKey, hasChildren));
            emitted.Add(parent.Id);

            if (!hasChildren)
            {
                continue;
            }

            foreach (var child in children!)
            {
                rows.Add(new MemberHierarchyRow(
                    child,
                    1,
                    parent.Id,
                    parent.MembershipType?.SubMemberLabel,
                    parentTypeKey,
                    false));
                emitted.Add(child.Id);
            }
        }

        // Orphan safety: a sub-member whose resolved parent is not itself emitted as a top-level
        // parent (e.g. filtered out by status, or the parent is itself nested) is never lost — it
        // is emitted as its own depth-0 row.
        foreach (var member in members
                     .Where(m => parentOf[m.Id] is not null && !emitted.Contains(m.Id))
                     .OrderBy(m => m.LastName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(m => m.FirstName, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(new MemberHierarchyRow(
                member,
                0,
                member.Id,
                null,
                member.MembershipType?.Key ?? UnassignedTypeKey,
                false));
            emitted.Add(member.Id);
        }

        return rows;
    }

    /// <summary>
    /// Returns the first <see cref="MemberMetadataFieldType.MemberReference"/> target (ordered by
    /// <see cref="MembershipTypeField.SortOrder"/>) that exists in the current set and whose
    /// <see cref="MembershipType.AllowsSubMembers"/> is true; otherwise null. In-memory only.
    /// </summary>
    private static Member? ResolveContainerParent(Member member, IReadOnlyDictionary<int, Member> byId)
    {
        var references = member.MetadataValues
            .Where(v => v.Field is not null
                        && v.Field.FieldType == MemberMetadataFieldType.MemberReference
                        && !string.IsNullOrWhiteSpace(v.Value))
            .OrderBy(v => v.Field!.SortOrder);

        foreach (var reference in references)
        {
            if (!int.TryParse(reference.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetId))
            {
                continue;
            }

            if (targetId == member.Id)
            {
                continue;
            }

            if (byId.TryGetValue(targetId, out var target)
                && target.MembershipType?.AllowsSubMembers == true)
            {
                return target;
            }
        }

        return null;
    }
}
