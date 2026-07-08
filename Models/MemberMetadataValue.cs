namespace ClubGear.Models;

public class MemberMetadataValue
{
    public int Id { get; set; }

    public int MemberId { get; set; }

    public int FieldId { get; set; }

    public MembershipTypeField? Field { get; set; }

    public string? Value { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
