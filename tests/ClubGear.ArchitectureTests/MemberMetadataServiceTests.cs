using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Core;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class MemberMetadataServiceTests
{
    private readonly IMemberMetadataService _sut = new MemberMetadataService();

    [Fact]
    public void ValidateAndEncode_ValidValuesPerFieldType_EncodesEachValue()
    {
        var fields = new List<MembershipTypeField>
        {
            new() { Id = 1, Key = "club_name", Label = "Vereinsname", FieldType = MemberMetadataFieldType.Text },
            new() { Id = 2, Key = "membership_fee", Label = "Mitgliedsbeitrag", FieldType = MemberMetadataFieldType.Number },
            new() { Id = 3, Key = "club_magazine", Label = "Vereinszeitschrift", FieldType = MemberMetadataFieldType.Boolean },
            new() { Id = 4, Key = "joined_on", Label = "Beigetreten am", FieldType = MemberMetadataFieldType.Date },
            new() { Id = 5, Key = "main_member", Label = "Hauptmitglied", FieldType = MemberMetadataFieldType.MemberReference }
        };

        var postedValues = new Dictionary<string, string?>
        {
            ["club_name"] = "Modellbauverein e.V.",
            ["membership_fee"] = "42.50",
            ["club_magazine"] = "ja",
            ["joined_on"] = "2024-03-15",
            ["main_member"] = "7"
        };

        var outcome = _sut.ValidateAndEncode(fields, postedValues, existingMemberIds: new[] { 7 });

        Assert.True(outcome.IsValid);
        Assert.Empty(outcome.Errors);
        Assert.Equal("Modellbauverein e.V.", outcome.EncodedValuesByFieldKey["club_name"]);
        Assert.Equal("42.50", outcome.EncodedValuesByFieldKey["membership_fee"]);
        Assert.Equal("true", outcome.EncodedValuesByFieldKey["club_magazine"]);
        Assert.Equal("2024-03-15", outcome.EncodedValuesByFieldKey["joined_on"]);
        Assert.Equal("7", outcome.EncodedValuesByFieldKey["main_member"]);
    }

    [Fact]
    public void ValidateAndEncode_MissingRequiredField_ReturnsInvalidWithError()
    {
        var fields = new List<MembershipTypeField>
        {
            new() { Id = 1, Key = "club_name", Label = "Vereinsname", FieldType = MemberMetadataFieldType.Text, IsRequired = true }
        };

        var outcome = _sut.ValidateAndEncode(fields, new Dictionary<string, string?>());

        Assert.False(outcome.IsValid);
        Assert.Contains(outcome.Errors, e => e.Contains("Vereinsname"));
    }

    [Fact]
    public void ValidateAndEncode_NonNumericNumberField_ReturnsInvalidWithError()
    {
        var fields = new List<MembershipTypeField>
        {
            new() { Id = 1, Key = "membership_fee", Label = "Mitgliedsbeitrag", FieldType = MemberMetadataFieldType.Number }
        };

        var postedValues = new Dictionary<string, string?> { ["membership_fee"] = "not-a-number" };

        var outcome = _sut.ValidateAndEncode(fields, postedValues);

        Assert.False(outcome.IsValid);
        Assert.Contains(outcome.Errors, e => e.Contains("Mitgliedsbeitrag"));
    }

    [Fact]
    public void ValidateAndEncode_MemberReferenceToNonExistingMember_ReturnsInvalidWithError()
    {
        var fields = new List<MembershipTypeField>
        {
            new() { Id = 1, Key = "main_member", Label = "Hauptmitglied", FieldType = MemberMetadataFieldType.MemberReference }
        };

        var postedValues = new Dictionary<string, string?> { ["main_member"] = "99" };

        var outcome = _sut.ValidateAndEncode(fields, postedValues, existingMemberIds: new[] { 1, 2, 3 });

        Assert.False(outcome.IsValid);
        Assert.Contains(outcome.Errors, e => e.Contains("Hauptmitglied"));
    }

    [Fact]
    public void ValidateAndEncode_MemberReferenceToSelf_ReturnsInvalidWithSelfError()
    {
        var fields = new List<MembershipTypeField>
        {
            new() { Id = 1, Key = "main_member", Label = "Hauptmitglied", FieldType = MemberMetadataFieldType.MemberReference }
        };

        var postedValues = new Dictionary<string, string?> { ["main_member"] = "5" };
        var context = new MemberReferenceIntegrityContext(
            SelfId: 5,
            ExistingSubMemberIds: new HashSet<int>(),
            ExistingParentIds: new HashSet<int>());

        var outcome = _sut.ValidateAndEncode(fields, postedValues, existingMemberIds: new[] { 5, 7 }, referenceContext: context);

        Assert.False(outcome.IsValid);
        Assert.Contains(outcome.Errors, e => e.Contains("darf nicht auf sich selbst verweisen"));
    }

    [Fact]
    public void ValidateAndEncode_MemberReferenceToExistingSubMember_RejectsGrandchild()
    {
        var fields = new List<MembershipTypeField>
        {
            new() { Id = 1, Key = "main_member", Label = "Hauptmitglied", FieldType = MemberMetadataFieldType.MemberReference }
        };

        // Member 1 tries to link to member 2, but member 2 is itself already a sub-member.
        var postedValues = new Dictionary<string, string?> { ["main_member"] = "2" };
        var context = new MemberReferenceIntegrityContext(
            SelfId: 1,
            ExistingSubMemberIds: new HashSet<int> { 2 },
            ExistingParentIds: new HashSet<int>());

        var outcome = _sut.ValidateAndEncode(fields, postedValues, existingMemberIds: new[] { 1, 2, 3 }, referenceContext: context);

        Assert.False(outcome.IsValid);
        Assert.Contains(outcome.Errors, e => e.Contains("das selbst ein Untermitglied ist"));
    }

    [Fact]
    public void ValidateAndEncode_SelfAlreadyHasSubMembers_RejectsTwoCycle()
    {
        var fields = new List<MembershipTypeField>
        {
            new() { Id = 1, Key = "main_member", Label = "Hauptmitglied", FieldType = MemberMetadataFieldType.MemberReference }
        };

        // Member 2 already acts as a parent (has sub-members) and now tries to become a sub-member of 3.
        var postedValues = new Dictionary<string, string?> { ["main_member"] = "3" };
        var context = new MemberReferenceIntegrityContext(
            SelfId: 2,
            ExistingSubMemberIds: new HashSet<int>(),
            ExistingParentIds: new HashSet<int> { 2 });

        var outcome = _sut.ValidateAndEncode(fields, postedValues, existingMemberIds: new[] { 2, 3 }, referenceContext: context);

        Assert.False(outcome.IsValid);
        Assert.Contains(outcome.Errors, e => e.Contains("hat bereits Untermitglieder und kann daher nicht selbst zugeordnet werden"));
    }

    [Fact]
    public void ValidateAndEncode_ValidParentReference_IsValidAndEncodesTarget()
    {
        var fields = new List<MembershipTypeField>
        {
            new() { Id = 1, Key = "main_member", Label = "Hauptmitglied", FieldType = MemberMetadataFieldType.MemberReference }
        };

        // Member 4 links to a top-level container 7: not self, 7 is not a sub-member, 4 is not a parent.
        var postedValues = new Dictionary<string, string?> { ["main_member"] = "7" };
        var context = new MemberReferenceIntegrityContext(
            SelfId: 4,
            ExistingSubMemberIds: new HashSet<int>(),
            ExistingParentIds: new HashSet<int>());

        var outcome = _sut.ValidateAndEncode(fields, postedValues, existingMemberIds: new[] { 4, 7 }, referenceContext: context);

        Assert.True(outcome.IsValid);
        Assert.Empty(outcome.Errors);
        Assert.Equal("7", outcome.EncodedValuesByFieldKey["main_member"]);
    }

    [Fact]
    public void ValidateAndEncode_UnrequiredMissingField_IsValidAndEncodesNull()
    {
        var fields = new List<MembershipTypeField>
        {
            new() { Id = 1, Key = "club_name", Label = "Vereinsname", FieldType = MemberMetadataFieldType.Text, IsRequired = false }
        };

        var outcome = _sut.ValidateAndEncode(fields, new Dictionary<string, string?>());

        Assert.True(outcome.IsValid);
        Assert.Null(outcome.EncodedValuesByFieldKey["club_name"]);
    }

    [Fact]
    public void ValidateAndEncode_UncheckedBooleanField_EncodesFalseEvenWhenRequired()
    {
        var fields = new List<MembershipTypeField>
        {
            new() { Id = 1, Key = "club_magazine", Label = "Vereinszeitschrift", FieldType = MemberMetadataFieldType.Boolean, IsRequired = true }
        };

        var outcome = _sut.ValidateAndEncode(fields, new Dictionary<string, string?>());

        Assert.True(outcome.IsValid);
        Assert.Equal("false", outcome.EncodedValuesByFieldKey["club_magazine"]);
    }
}
