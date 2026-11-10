namespace ClubGear.Services.Abstractions;

/// <summary>
/// CORE-14. Which plugin serves a single-active capability (storage.blob, document.store). An
/// administrator chooses; until then the core default serves it, so installing a plugin never
/// silently changes where data goes (R3).
/// </summary>
public interface IPluginProviderSelectionService
{
    /// <summary>The selected module id, or <c>null</c> when the core default is selected.</summary>
    string? GetSelectedModuleId(string capabilityKey);

    /// <summary>One entry per single-active capability key, for plugin administration.</summary>
    IReadOnlyList<PluginProviderSelectionStatus> GetStatuses();

    /// <summary>
    /// Selects <paramref name="moduleId"/> for the capability, or the core default when it is
    /// <c>null</c>. The module must be an active plugin that claims the capability. Audited.
    /// </summary>
    Task<PluginProviderSelectionResult> SelectAsync(
        string capabilityKey,
        string? moduleId,
        string? actor,
        CancellationToken cancellationToken = default);
}

public sealed record PluginProviderSelectionStatus(
    string CapabilityKey,
    string? SelectedModuleId,
    IReadOnlyList<string> ClaimantModuleIds)
{
    /// <summary>The selected plugin is not active (or no longer claims the key): the core default serves it.</summary>
    public bool IsSelectedProviderMissing => SelectedModuleId is not null
        && !ClaimantModuleIds.Contains(SelectedModuleId, StringComparer.OrdinalIgnoreCase);

    /// <summary>Several plugins claim the key and nobody chose between them.</summary>
    public bool HasUnresolvedCollision => SelectedModuleId is null && ClaimantModuleIds.Count > 1;
}

public sealed record PluginProviderSelectionResult(bool Success, string Message);
