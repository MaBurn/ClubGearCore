namespace ClubGear.Plugin.Contracts;

public sealed record PluginNavEntry(
    string Label,
    string Icon,
    string Route,
    string? RequiredPermission,
    int SortOrder = 0)
{
    /// <summary>
    /// Contract 1.14.0. Entries with the same non-empty group key are rendered in one dropdown.
    /// Null keeps the established flat navigation entry.
    /// </summary>
    public string? GroupKey { get; init; }

    /// <summary>Contract 1.14.0. User-facing label of the grouped navigation dropdown.</summary>
    public string? GroupLabel { get; init; }

    /// <summary>Contract 1.14.0. Optional Bootstrap icon class for the dropdown trigger.</summary>
    public string? GroupIcon { get; init; }
}
