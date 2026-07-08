using System.ComponentModel.DataAnnotations;

namespace ClubGear.Models;

public class SystemConfigEntry
{
    public int Id { get; set; }

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(128)]
    public string Section { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string Description { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
