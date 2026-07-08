namespace ClubGear.Plugin.Contracts;

public sealed record PluginNavEntry(
    string Label,
    string Icon,
    string Route,
    string? RequiredPermission,
    int SortOrder = 0);
