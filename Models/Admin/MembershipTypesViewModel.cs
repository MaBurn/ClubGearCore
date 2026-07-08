using ClubGear.Models;
using ClubGear.Models.Feedback;

namespace ClubGear.Models.Admin;

public sealed class MembershipTypesViewModel
{
    public List<MembershipType> Types { get; init; } = new();
    public ActionFeedbackViewModel? Feedback { get; init; }
    public IReadOnlyList<MemberMetadataFieldType> FieldTypes { get; init; } =
        Enum.GetValues<MemberMetadataFieldType>();
}

public sealed class CreateMembershipTypeInputModel
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DefaultDiscountPercent { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool AllowsSubMembers { get; set; }
    public string? SubMemberLabel { get; set; }
}

public sealed class UpdateMembershipTypeInputModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DefaultDiscountPercent { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public bool AllowsSubMembers { get; set; }
    public string? SubMemberLabel { get; set; }
}

public sealed class CreateMembershipTypeFieldInputModel
{
    public int MembershipTypeId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public MemberMetadataFieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public string? HelpText { get; set; }
    public int SortOrder { get; set; }
}

public sealed class UpdateMembershipTypeFieldInputModel
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public MemberMetadataFieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public string? HelpText { get; set; }
    public int SortOrder { get; set; }
}
