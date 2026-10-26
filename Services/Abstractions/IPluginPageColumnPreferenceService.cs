namespace ClubGear.Services.Abstractions;

/// <summary>
/// Per-user column-visibility preference for a generic plugin list page
/// (<see cref="ClubGear.Controllers.PluginPageController"/>). Null from
/// <see cref="GetHiddenColumnsAsync"/> means the user never customised that page — the view
/// falls back to hiding columns with no data instead of an explicit choice.
/// </summary>
public interface IPluginPageColumnPreferenceService
{
    Task<IReadOnlySet<string>?> GetHiddenColumnsAsync(
        string userId, string moduleId, string pageKey, CancellationToken cancellationToken = default);

    Task SetHiddenColumnsAsync(
        string userId, string moduleId, string pageKey, IReadOnlyCollection<string> hiddenColumns,
        CancellationToken cancellationToken = default);
}
