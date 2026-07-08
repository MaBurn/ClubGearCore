using System.Globalization;
using ClubGear.Models;

namespace ClubGear.Services.Core;

/// <summary>
/// Resolves a member's effective discount percentage, replacing the removed
/// <c>Member.MembershipDiscount</c> scalar column. Precedence:
/// 1. A per-member <see cref="MemberMetadataValue"/> override for the well-known
///    <see cref="DiscountOverrideFieldKey"/> field (encoded as an invariant-culture
///    decimal string by <see cref="MemberMetadataService"/>), if present and non-empty.
/// 2. Otherwise, the member's <see cref="MembershipType.DefaultDiscountPercent"/>.
/// 3. Otherwise, zero.
/// </summary>
public static class MemberDiscountResolver
{
    /// <summary>
    /// The <see cref="MembershipTypeField.Key"/> under which a per-member discount
    /// override is stored (seeded on every system-defined Mitgliedsart, see
    /// <c>Data/Migrations/202607070101_AddMembershipTypeModel.cs</c>).
    /// </summary>
    public const string DiscountOverrideFieldKey = "membership_discount_override";

    public static int GetEffectiveDiscountPercent(Member member)
    {
        ArgumentNullException.ThrowIfNull(member);

        var overrideValue = member.MetadataValues
            .FirstOrDefault(v => v.Field is not null
                && string.Equals(v.Field.Key, DiscountOverrideFieldKey, StringComparison.Ordinal))
            ?.Value;

        if (!string.IsNullOrWhiteSpace(overrideValue)
            && decimal.TryParse(overrideValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var overridePercent))
        {
            return (int)Math.Round(overridePercent, MidpointRounding.AwayFromZero);
        }

        return member.MembershipType?.DefaultDiscountPercent ?? 0;
    }
}
