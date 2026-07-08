using System.ComponentModel.DataAnnotations;

namespace ClubGear.Models;

public enum MemberMetadataFieldType
{
    Text,
    Number,
    Boolean,
    Date,
    MemberReference
}

public class MembershipTypeField
{
    public int Id { get; set; }

    public int MembershipTypeId { get; set; }

    public MembershipType? MembershipType { get; set; }

    [Required]
    [StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Label { get; set; } = string.Empty;

    public MemberMetadataFieldType FieldType { get; set; }

    public bool IsRequired { get; set; }

    [StringLength(500)]
    public string? HelpText { get; set; }

    public int SortOrder { get; set; }

    public bool IsSystemDefined { get; set; }
}
