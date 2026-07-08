using System.Runtime.Loader;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Plugins.Runtime;

public sealed class PluginRegistry : IPluginRuntimeRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<string, RegisteredPluginState> _plugins = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<RegisteredPluginRuntime> GetRegisteredPlugins()
    {
        lock (_sync)
        {
            return _plugins.Values
                .Select(state => state.Runtime)
                .OrderBy(runtime => runtime.ModuleId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    public RegisteredPluginRuntime? GetByModuleId(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        lock (_sync)
        {
            return _plugins.TryGetValue(moduleId, out var state)
                ? state.Runtime
                : null;
        }
    }

    public IPluginModule? GetModule(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        lock (_sync)
        {
            return _plugins.TryGetValue(moduleId, out var state)
                ? state.Module
                : null;
        }
    }

    public TProvider? CreateMemberProvider<TProvider>(string moduleId, string providerType)
        where TProvider : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerType);

        lock (_sync)
        {
            if (!_plugins.TryGetValue(moduleId, out var state))
            {
                return null;
            }

            var providerClrType = state.Module.GetType().Assembly.GetType(providerType, throwOnError: false, ignoreCase: false);
            if (providerClrType is null || !typeof(TProvider).IsAssignableFrom(providerClrType))
            {
                return null;
            }

            return Activator.CreateInstance(providerClrType) as TProvider;
        }
    }

    public void Register(RegisteredPluginRuntime runtime, IPluginModule module, AssemblyLoadContext loadContext)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(loadContext);

        lock (_sync)
        {
            if (_plugins.TryGetValue(runtime.ModuleId, out var existing))
            {
                Unload(existing.LoadContext);
            }

            _plugins[runtime.ModuleId] = new RegisteredPluginState(runtime, module, loadContext);
        }
    }

    public void AddOrReplaceRoute(string moduleId, PluginRouteContribution route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(route);

        lock (_sync)
        {
            if (!_plugins.TryGetValue(moduleId, out var state))
            {
                return;
            }

            var routes = state.Runtime.Routes
                .Where(existing => !string.Equals(existing.RoutePattern, route.RoutePattern, StringComparison.OrdinalIgnoreCase))
                .Append(route)
                .OrderBy(existing => existing.RoutePattern, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            state.Runtime = state.Runtime with { Routes = routes };
        }
    }

    public bool Unregister(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        lock (_sync)
        {
            if (!_plugins.Remove(moduleId, out var state))
            {
                return false;
            }

            Unload(state.LoadContext);
            return true;
        }
    }

    private static void Unload(AssemblyLoadContext loadContext)
    {
        if (!loadContext.IsCollectible)
        {
            return;
        }

        loadContext.Unload();
    }

    private sealed class RegisteredPluginState
    {
        public RegisteredPluginState(RegisteredPluginRuntime runtime, IPluginModule module, AssemblyLoadContext loadContext)
        {
            Runtime = runtime;
            Module = module;
            LoadContext = loadContext;
        }

        public RegisteredPluginRuntime Runtime { get; set; }

        public IPluginModule Module { get; }

        public AssemblyLoadContext LoadContext { get; }
    }
}