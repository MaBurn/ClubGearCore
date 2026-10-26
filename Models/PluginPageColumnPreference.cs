using System.ComponentModel.DataAnnotations;

namespace ClubGear.Models;

/// <summary>
/// One user's column-visibility choice for one generic plugin list page
/// (<see cref="ClubGear.Controllers.PluginPageController"/>). <see cref="HiddenColumnsJson"/>
/// stores the column keys the user explicitly hid, not the ones shown — a column a plugin adds
/// later therefore defaults to visible instead of silently disappearing. Absence of a row for
/// (UserId, ModuleId, PageKey) means the user has not customised that page: the host falls back
/// to hiding columns with no data in the rendered table instead.
/// </summary>
public class PluginPageColumnPreference
{
    public int Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string ModuleId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string PageKey { get; set; } = string.Empty;

    /// <summary>JSON array of column keys (<see cref="ClubGear.Plugin.Contracts.PluginPageColumn.Key"/>) the user hid.</summary>
    public string HiddenColumnsJson { get; set; } = "[]";

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
