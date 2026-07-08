using ClubGear.Models;
using ClubGear.Services.Core;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class MemberDiscountResolverTests
{
    private static MembershipTypeField DiscountOverrideField(int id = 1) => new()
    {
        Id = id,
        Key = MemberDiscountResolver.DiscountOverrideFieldKey,
        Label = "Mitgliedsrabatt (Override)",
        FieldType = MemberMetadataFieldType.Number
    };

    [Fact]
    public void GetEffectiveDiscountPercent_OverrideValuePresent_ReturnsOverride()
    {
        var field = DiscountOverrideField();
        var member = new Member
        {
            MembershipType = new MembershipType { Key = "Verein", Name = "Verein", DefaultDiscountPercent = 10 },
            MetadataValues = new List<MemberMetadataValue>
            {
                new() { FieldId = field.Id, Field = field, Value = "25" }
            }
        };

        var result = MemberDiscountResolver.GetEffectiveDiscountPercent(member);

        Assert.Equal(25, result);
    }

    [Fact]
    public void GetEffectiveDiscountPercent_NoOverrideButTypeDefaultPresent_ReturnsTypeDefault()
    {
        var member = new Member
        {
            MembershipType = new MembershipType { Key = "Firma", Name = "Firma", DefaultDiscountPercent = 15 },
            MetadataValues = new List<MemberMetadataValue>()
        };

        var result = MemberDiscountResolver.GetEffectiveDiscountPercent(member);

        Assert.Equal(15, result);
    }

    [Fact]
    public void GetEffectiveDiscountPercent_NeitherOverrideNorTypeDefault_ReturnsZero()
    {
        var member = new Member
        {
            MembershipType = new MembershipType { Key = "Standard", Name = "Standard", DefaultDiscountPercent = null },
            MetadataValues = new List<MemberMetadataValue>()
        };

        var result = MemberDiscountResolver.GetEffectiveDiscountPercent(member);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetEffectiveDiscountPercent_NoMembershipTypeAndNoOverride_ReturnsZero()
    {
        var member = new Member
        {
            MembershipType = null,
            MetadataValues = new List<MemberMetadataValue>()
        };

        var result = MemberDiscountResolver.GetEffectiveDiscountPercent(member);

        Assert.Equal(0, result);
    }

    [Fact]
    public void GetEffectiveDiscountPercent_EmptyOverrideValue_FallsBackToTypeDefault()
    {
        var field = DiscountOverrideField();
        var member = new Member
        {
            MembershipType = new MembershipType { Key = "Familie", Name = "Familie", DefaultDiscountPercent = 5 },
            MetadataValues = new List<MemberMetadataValue>
            {
                new() { FieldId = field.Id, Field = field, Value = "" }
            }
        };

        var result = MemberDiscountResolver.GetEffectiveDiscountPercent(member);

        Assert.Equal(5, result);
    }
}
