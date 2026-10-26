using System.Runtime.Loader;
using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Abstractions;

public interface IPluginRuntimeRegistry : IPluginRegistryReader
{
    /// <summary>
    /// Registers a loaded plugin. A registration it displaces is sealed, drained and unloaded first
    /// (CORE-17), which is why this is asynchronous.
    /// </summary>
    Task RegisterAsync(
        RegisteredPluginRuntime runtime,
        IPluginModule module,
        AssemblyLoadContext loadContext,
        CancellationToken cancellationToken = default);

    void AddOrReplaceRoute(string moduleId, PluginRouteContribution route);

    /// <summary>
    /// Seals the module, waits for in-flight plugin work within the configured budget and then
    /// unloads the module's load context — whether the drain succeeded or not.
    /// </summary>
    Task<PluginUnloadOutcome> UnregisterAsync(string moduleId, CancellationToken cancellationToken = default);
}
