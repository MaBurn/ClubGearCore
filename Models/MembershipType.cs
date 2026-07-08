using System.ComponentModel.DataAnnotations;

namespace ClubGear.Models;

public class MembershipType
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public int? DefaultDiscountPercent { get; set; }

    public bool IsSystemDefined { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool AllowsSubMembers { get; set; }

    [StringLength(100)]
    public string? SubMemberLabel { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<MembershipTypeField> Fields { get; set; } = new();
}
