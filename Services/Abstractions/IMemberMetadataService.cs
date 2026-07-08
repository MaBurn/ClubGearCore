using ClubGear.Models;

namespace ClubGear.Services.Abstractions;

/// <summary>
/// Result of validating a set of raw, form-posted metadata values against the
/// <see cref="MembershipTypeField"/> definitions of a member's chosen <see cref="MembershipType"/>.
/// </summary>
public sealed record MemberMetadataValidationOutcome(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyDictionary<string, string?> EncodedValuesByFieldKey)
{
    public static MemberMetadataValidationOutcome Ok(IReadOnlyDictionary<string, string?> encodedValuesByFieldKey)
        => new(true, Array.Empty<string>(), encodedValuesByFieldKey);

    public static MemberMetadataValidationOutcome Failed(IReadOnlyList<string> errors)
        => new(false, errors, new Dictionary<string, string?>());
}

/// <summary>
/// Id-only snapshot used to enforce the single-level parent/sub-member hierarchy when validating
/// <see cref="MemberMetadataFieldType.MemberReference"/> values. Carries no entities so the
/// validating service stays stateless/pure.
/// </summary>
/// <param name="SelfId">The member currently being saved (<c>null</c> for a new member).</param>
/// <param name="ExistingSubMemberIds">
/// Member ids that already carry a parent link (i.e. are themselves sub-members).
/// A reference target contained here would create a grandchild.
/// </param>
/// <param name="ExistingParentIds">
/// Member ids currently referenced by at least one sub-member (i.e. already act as parents).
/// If <see cref="SelfId"/> is contained here it cannot itself be assigned to a parent (2-cycle).
/// </param>
public sealed record MemberReferenceIntegrityContext(
    int? SelfId,
    IReadOnlySet<int> ExistingSubMemberIds,
    IReadOnlySet<int> ExistingParentIds);

/// <summary>
/// Validates and encodes/decodes <see cref="MemberMetadataValue.Value"/> per
/// <see cref="MemberMetadataFieldType"/> (Text/Number/Boolean/Date/MemberReference), against a
/// set of <see cref="MembershipTypeField"/> definitions (typically the fields of a member's
/// currently selected <see cref="MembershipType"/>).
/// </summary>
public interface IMemberMetadataService
{
    /// <summary>
    /// Validates <paramref name="postedValues"/> (keyed by <see cref="MembershipTypeField.Key"/>)
    /// against <paramref name="fields"/>: checks required fields are present and parses/encodes
    /// each raw value per its <see cref="MembershipTypeField.FieldType"/>.
    /// </summary>
    /// <param name="fields">The metadata field definitions to validate against.</param>
    /// <param name="postedValues">Raw, unvalidated values keyed by field <c>Key</c>.</param>
    /// <param name="existingMemberIds">
    /// Optional set of currently existing member ids, used to validate
    /// <see cref="MemberMetadataFieldType.MemberReference"/> fields resolve to a real member.
    /// When <c>null</c>, existence of the referenced member is not checked (format-only validation).
    /// </param>
    /// <param name="referenceContext">
    /// Optional single-level hierarchy context used to reject self-references, grandchildren, and
    /// 2-cycles on <see cref="MemberMetadataFieldType.MemberReference"/> fields. When <c>null</c>,
    /// only existence (via <paramref name="existingMemberIds"/>) is enforced.
    /// </param>
    MemberMetadataValidationOutcome ValidateAndEncode(
        IReadOnlyList<MembershipTypeField> fields,
        IReadOnlyDictionary<string, string?> postedValues,
        IReadOnlyCollection<int>? existingMemberIds = null,
        MemberReferenceIntegrityContext? referenceContext = null);
}
