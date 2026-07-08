using ClubGear.Models;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class MemberModelTests
{
    [Fact]
    public void Member_ExposesExtendedClubManagerFields()
    {
        var memberType = typeof(Member);

        Assert.NotNull(memberType.GetProperty(nameof(Member.OauthID)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.OAuthUserName)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.Joined)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.Leaved)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.LastUpdated)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.IsDeceased)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.NotifyViaEmail)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.NotifyViaMatrix)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.DataprivacyAccepted)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.NewsletterConsent)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.ProfileImagePath)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.PendingEmail)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.EmailVerificationToken)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.EmailVerificationTokenExpiry)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.RentalPayoutOptions)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.InitPassword)));
        Assert.NotNull(memberType.GetProperty(nameof(Member.KeycloakUsername)));
    }

    [Fact]
    public void Member_NoLongerExposesLegacyMembershipTypeScalarProperties()
    {
        var memberType = typeof(Member);

        Assert.Null(memberType.GetProperty("IsCompany"));
        Assert.Null(memberType.GetProperty("CompanyName"));
        Assert.Null(memberType.GetProperty("IsClub"));
        Assert.Null(memberType.GetProperty("ClubName"));
        Assert.Null(memberType.GetProperty("MembershipDiscount"));
        Assert.Null(memberType.GetProperty("FamilyMembership"));
        Assert.Null(memberType.GetProperty("MainMemberId"));
    }

    [Fact]
    public void Member_ExposesMembershipTypeAndMetadataValueProperties()
    {
        var memberType = typeof(Member);

        var membershipTypeIdProperty = memberType.GetProperty(nameof(Member.MembershipTypeId));
        Assert.NotNull(membershipTypeIdProperty);
        Assert.Equal(typeof(int?), membershipTypeIdProperty!.PropertyType);

        var membershipTypeProperty = memberType.GetProperty(nameof(Member.MembershipType));
        Assert.NotNull(membershipTypeProperty);
        Assert.Equal(typeof(MembershipType), membershipTypeProperty!.PropertyType);

        var metadataValuesProperty = memberType.GetProperty(nameof(Member.MetadataValues));
        Assert.NotNull(metadataValuesProperty);
        Assert.True(typeof(System.Collections.IEnumerable).IsAssignableFrom(metadataValuesProperty!.PropertyType));

        var member = new Member();
        Assert.NotNull(member.MetadataValues);
        Assert.Empty(member.MetadataValues);
    }
}
