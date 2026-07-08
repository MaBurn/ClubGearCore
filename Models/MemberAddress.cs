using System.ComponentModel.DataAnnotations;

namespace ClubGear.Models;

public class MemberAddress
{
    public int Id { get; set; }

    public int MemberId { get; set; }
    public Member? Member { get; set; }

    [StringLength(255)]
    public string? Street { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    public bool IsDefault { get; set; }
}
