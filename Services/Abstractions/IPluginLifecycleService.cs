namespace ClubGear.Services.Abstractions;

public interface IPluginLifecycleService
{
    Task<IReadOnlyList<PluginLifecycleOperationResult>> LoadActivatedAsync(CancellationToken cancellationToken = default);

    Task<PluginLifecycleOperationResult> ActivateAsync(string moduleId, CancellationToken cancellationToken = default);

    Task<PluginLifecycleOperationResult> DeactivateAsync(string moduleId, CancellationToken cancellationToken = default);
}

public sealed record PluginLifecycleOperationResult(
    bool Success,
    string Status,
    string Message,
    InstalledPluginRecord? Plugin = null);