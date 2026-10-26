namespace ClubGear.Services.Abstractions;

/// <summary>
/// Counts in-flight plugin work per module so that deactivation can drain before it unloads
/// the module's <see cref="System.Runtime.Loader.AssemblyLoadContext"/> (CORE-17).
/// </summary>
/// <remarks>
/// Ordering guarantee: <see cref="Seal"/> stops new tokens from being handed out,
/// <see cref="WaitForDrainAsync"/> waits for the tokens already handed out, and the caller
/// unloads afterwards — whether the drain succeeded or not.
/// </remarks>
public interface IPluginActivityTracker
{
    /// <summary>
    /// Takes a token for one unit of plugin work. Returns <c>null</c> when the module is sealed,
    /// i.e. deactivation has started and no new work may enter.
    /// </summary>
    IDisposable? TryAcquire(string moduleId);

    /// <summary>Stops new tokens from being handed out for the module.</summary>
    void Seal(string moduleId);

    /// <summary>Allows tokens to be handed out again, e.g. after the module was re-registered.</summary>
    void Unseal(string moduleId);

    /// <summary>Number of tokens currently outstanding for the module.</summary>
    int GetInFlight(string moduleId);

    /// <summary>
    /// Waits until no token is outstanding for the module. Returns <c>false</c> when the budget
    /// expired first; the caller is expected to proceed regardless.
    /// </summary>
    Task<bool> WaitForDrainAsync(string moduleId, TimeSpan timeout, CancellationToken cancellationToken = default);
}
