using System.Globalization;
using ClubGear.Models;
using ClubGear.Models.MemberFilters;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class MemberHierarchyTests
{
    private static MembershipType ContainerType(string key = "Firma", string label = "Mitarbeiter")
        => new() { Id = 10, Key = key, Name = key, AllowsSubMembers = true, SubMemberLabel = label };

    private static MembershipType PlainType(string key = "Standard")
        => new() { Id = 20, Key = key, Name = key, AllowsSubMembers = false };

    private static Member ParentMember(int id, string last, string first, MembershipType type)
        => new() { Id = id, MemberNumber = $"M-{id:000}", FirstName = first, LastName = last, MembershipType = type };

    private static Member SubMember(int id, string last, string first, int parentId, MembershipType ownType, int sortOrder = 0)
    {
        var member = new Member
        {
            Id = id,
            MemberNumber = $"M-{id:000}",
            FirstName = first,
            LastName = last,
            MembershipType = ownType
        };

        member.MetadataValues.Add(new MemberMetadataValue
        {
            MemberId = id,
            Value = parentId.ToString(CultureInfo.InvariantCulture),
            Field = new MembershipTypeField
            {
                Key = "main_member",
                Label = "Hauptmitglied",
                FieldType = MemberMetadataFieldType.MemberReference,
                SortOrder = sortOrder
            }
        });

        return member;
    }

    [Fact]
    public void BuildHierarchy_EmitsEachContainerParentImmediatelyFollowedByItsSubMembers()
    {
        var container = ContainerType();
        var parent = ParentMember(1, "Zeta", "Anton", container);
        var childB = SubMember(2, "Berg", "Bea", parentId: 1, ownType: PlainType());
        var childA = SubMember(3, "Anger", "Cara", parentId: 1, ownType: PlainType());

        var rows = MemberHierarchyViewModel.FromMembers(new[] { parent, childB, childA }).Rows;

        Assert.Equal(3, rows.Count);
        // Parent first (depth 0), then its children ordered by (LastName, FirstName).
        Assert.Equal(1, rows[0].Member.Id);
        Assert.Equal(0, rows[0].Depth);
        Assert.Equal(3, rows[1].Member.Id); // Anger before Berg
        Assert.Equal(1, rows[1].Depth);
        Assert.Equal(2, rows[2].Member.Id); // Berg
        Assert.Equal(1, rows[2].Depth);
    }

    [Fact]
    public void BuildHierarchy_TopLevelMembers_AreOrderedByLastNameThenFirstName()
    {
        var plain = PlainType();
        var m1 = ParentMember(1, "Mueller", "Zoe", plain);
        var m2 = ParentMember(2, "Abel", "Yara", plain);
        var m3 = ParentMember(3, "Abel", "Xaver", plain);

        var rows = MemberHierarchyViewModel.FromMembers(new[] { m1, m2, m3 }).Rows;

        Assert.Equal(new[] { 3, 2, 1 }, rows.Select(r => r.Member.Id).ToArray());
        Assert.All(rows, r => Assert.Equal(0, r.Depth));
    }

    [Fact]
    public void BuildHierarchy_RendersEachMemberExactlyOnce()
    {
        var container = ContainerType();
        var parent = ParentMember(1, "Alpha", "Anton", container);
        var child1 = SubMember(2, "Beta", "Bea", parentId: 1, ownType: PlainType());
        var child2 = SubMember(3, "Gamma", "Cara", parentId: 1, ownType: PlainType());
        var unrelated = ParentMember(4, "Delta", "Dora", PlainType());

        var rows = MemberHierarchyViewModel.FromMembers(new[] { parent, child1, child2, unrelated }).Rows;

        var ids = rows.Select(r => r.Member.Id).ToList();
        Assert.Equal(4, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void BuildHierarchy_SubMemberRow_TakesLabelFromParentContainerType()
    {
        var container = ContainerType(key: "Firma", label: "Mitarbeiter");
        var parent = ParentMember(1, "Alpha", "Anton", container);
        var child = SubMember(2, "Beta", "Bea", parentId: 1, ownType: PlainType());

        var rows = MemberHierarchyViewModel.FromMembers(new[] { parent, child }).Rows;

        var childRow = Assert.Single(rows, r => r.Member.Id == 2);
        Assert.Equal("Mitarbeiter", childRow.GroupLabel);
        Assert.Equal("Firma", childRow.GroupType);
        Assert.Equal(1, childRow.GroupId); // grouped under the parent
        Assert.Null(rows.Single(r => r.Member.Id == 1).GroupLabel); // parent has no label
        Assert.True(rows.Single(r => r.Member.Id == 1).HasSubMembers);
    }

    [Fact]
    public void BuildHierarchy_ReferenceToNonContainerType_IsNotNested()
    {
        // The target exists but its type does not allow sub-members -> the referring member stays top-level.
        var nonContainer = PlainType("Standard");
        var target = ParentMember(1, "Alpha", "Anton", nonContainer);
        var referrer = SubMember(2, "Beta", "Bea", parentId: 1, ownType: PlainType());

        var rows = MemberHierarchyViewModel.FromMembers(new[] { target, referrer }).Rows;

        Assert.All(rows, r => Assert.Equal(0, r.Depth));
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void BuildHierarchy_OrphanSubMember_WhoseParentIsFilteredOut_IsEmittedAsDepthZero()
    {
        // Parent id 1 is NOT part of the set (e.g. filtered out by status). The sub-member
        // must not be lost; it is emitted as its own top-level row.
        var orphan = SubMember(2, "Beta", "Bea", parentId: 1, ownType: PlainType());

        var rows = MemberHierarchyViewModel.FromMembers(new[] { orphan }).Rows;

        var only = Assert.Single(rows);
        Assert.Equal(2, only.Member.Id);
        Assert.Equal(0, only.Depth);
        Assert.Null(only.GroupLabel);
    }

    [Fact]
    public void BuildHierarchy_MultipleReferences_NestsUnderFirstContainerBySortOrder()
    {
        var container = ContainerType(key: "Firma", label: "Mitarbeiter");
        var firstParent = ParentMember(1, "Alpha", "Anton", container);
        var secondParent = ParentMember(2, "Beta", "Bea", ContainerType(key: "Verein", label: "Mitglied"));

        var child = new Member { Id = 3, MemberNumber = "M-003", FirstName = "Cara", LastName = "Gamma", MembershipType = PlainType() };
        // Higher SortOrder points at parent 2; lower SortOrder (winner) points at parent 1.
        child.MetadataValues.Add(new MemberMetadataValue
        {
            Value = "2",
            Field = new MembershipTypeField { Key = "b", Label = "B", FieldType = MemberMetadataFieldType.MemberReference, SortOrder = 5 }
        });
        child.MetadataValues.Add(new MemberMetadataValue
        {
            Value = "1",
            Field = new MembershipTypeField { Key = "a", Label = "A", FieldType = MemberMetadataFieldType.MemberReference, SortOrder = 1 }
        });

        var rows = MemberHierarchyViewModel.FromMembers(new[] { firstParent, secondParent, child }).Rows;

        var childRow = Assert.Single(rows, r => r.Member.Id == 3);
        Assert.Equal(1, childRow.GroupId);
        Assert.Equal("Mitarbeiter", childRow.GroupLabel);
        Assert.Equal(1, childRow.Depth);
    }

    [Fact]
    public void MembersIndexView_RendersNestedIndentedRowsFromHierarchyModel()
    {
        var indexContent = File.ReadAllText(GetProjectFilePath("Views", "Members", "Index.cshtml"));

        // Reads the hierarchy read model produced by the controller.
        Assert.Contains("ViewData[\"MemberHierarchy\"]", indexContent, StringComparison.Ordinal);
        Assert.Contains("MemberHierarchyViewModel", indexContent, StringComparison.Ordinal);
        // Iterates hierarchy rows rather than raw member lists.
        Assert.Contains("verifiedRows", indexContent, StringComparison.Ordinal);
        Assert.Contains("unverifiedRows", indexContent, StringComparison.Ordinal);
        // Sub-member rows expose their depth, are indented, and carry the group label.
        Assert.Contains("data-depth=\"@row.Depth\"", indexContent, StringComparison.Ordinal);
        Assert.Contains("row.Depth > 0", indexContent, StringComparison.Ordinal);
        Assert.Contains("padding-left: 2.5rem;", indexContent, StringComparison.Ordinal);
        Assert.Contains("member-submember-label", indexContent, StringComparison.Ordinal);
        Assert.Contains("@row.GroupLabel", indexContent, StringComparison.Ordinal);
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
