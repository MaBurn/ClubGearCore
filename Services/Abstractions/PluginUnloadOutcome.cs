namespace ClubGear.Services.Abstractions;

/// <summary>
/// Result of removing a plugin from the runtime registry (CORE-17).
/// </summary>
/// <param name="Removed">Whether a registration for the module existed and was removed.</param>
/// <param name="Drained">
/// Whether all in-flight plugin work finished within the configured budget. The load context is
/// unloaded either way — a stuck plugin must never pin the host.
/// </param>
/// <param name="InFlightAtUnload">Number of plugin invocations still running when the unload happened.</param>
public readonly record struct PluginUnloadOutcome(bool Removed, bool Drained, int InFlightAtUnload);
