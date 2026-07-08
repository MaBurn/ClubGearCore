using System.ComponentModel.DataAnnotations;

namespace ClubGear.Models;

public class PluginStatusRecord
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Version { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Author { get; set; } = "Unknown";

    [Required]
    [StringLength(100)]
    public string License { get; set; } = "Unspecified";

    [Required]
    [StringLength(50)]
    public string Category { get; set; } = "General";

    [Required]
    [StringLength(500)]
    public string EntryPoint { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string RequiredCoreVersion { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string InstallSource { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string PackageHash { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string PackagePath { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    [StringLength(4000)]
    public string? LastError { get; set; }

    [Required]
    public string PermissionsJson { get; set; } = "[]";

    [Required]
    public string ExtensionPointsJson { get; set; } = "[]";

    [Required]
    public string DependenciesJson { get; set; } = "[]";

    public DateTime InstalledAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}