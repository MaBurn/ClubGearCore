using System.ComponentModel.DataAnnotations;

namespace ClubGear.Models;

public sealed class PluginMigrationState
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string PluginKey { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string MigrationId { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string TablePrefix { get; set; } = string.Empty;

    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow;
}