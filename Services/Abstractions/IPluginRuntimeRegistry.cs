using System.Runtime.Loader;
using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Abstractions;

public interface IPluginRuntimeRegistry : IPluginRegistryReader
{
    void Register(RegisteredPluginRuntime runtime, IPluginModule module, AssemblyLoadContext loadContext);

    void AddOrReplaceRoute(string moduleId, PluginRouteContribution route);

    bool Unregister(string moduleId);
}