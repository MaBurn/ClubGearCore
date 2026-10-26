using System.Runtime.Loader;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClubGear.Services.Plugins.Runtime;

public sealed class PluginRegistry : IPluginRuntimeRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<string, RegisteredPluginState> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly IPluginActivityTracker _activityTracker;
    private readonly PluginRuntimeOptions _options;
    private readonly ILogger<PluginRegistry> _logger;

    // The dependencies are optional so that the many call sites which only need a plain in-memory
    // registry can keep using `new PluginRegistry()`. In the host they are supplied by DI.
    public PluginRegistry(
        IPluginActivityTracker? activityTracker = null,
        IOptions<PluginRuntimeOptions>? options = null,
        ILogger<PluginRegistry>? logger = null)
    {
        _activityTracker = activityTracker ?? new PluginActivityTracker();
        _options = options?.Value ?? new PluginRuntimeOptions();
        _logger = logger ?? NullLogger<PluginRegistry>.Instance;
    }

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

    public Type? ResolveContributionType(string moduleId, string contributionType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionType);

        // CORE-18. Lookup only. Activator.CreateInstance used to run here, inside _sync, which
        // meant plugin code executed under the registry lock and outside the activity tracker.
        lock (_sync)
        {
            return _plugins.TryGetValue(moduleId, out var state)
                ? state.Module.GetType().Assembly.GetType(contributionType, throwOnError: false, ignoreCase: false)
                : null;
        }
    }

    public async Task RegisterAsync(
        RegisteredPluginRuntime runtime,
        IPluginModule module,
        AssemblyLoadContext loadContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(loadContext);

        // A registration that displaces a live one goes through the same seal -> drain -> unload
        // path as deactivation, so no unload bypasses the drain.
        await SealDrainAndUnloadAsync(runtime.ModuleId, cancellationToken);

        lock (_sync)
        {
            _plugins[runtime.ModuleId] = new RegisteredPluginState(runtime, module, loadContext);
        }

        _activityTracker.Unseal(runtime.ModuleId);
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

    public Task<PluginUnloadOutcome> UnregisterAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        return SealDrainAndUnloadAsync(moduleId, cancellationToken);
    }

    /// <summary>
    /// Seals the module inside the lock and removes it, then waits for in-flight plugin work
    /// outside the lock and unloads unconditionally. Sealing before removal means a caller that has
    /// already read the registry entry can no longer acquire a token afterwards.
    /// </summary>
    private async Task<PluginUnloadOutcome> SealDrainAndUnloadAsync(string moduleId, CancellationToken cancellationToken)
    {
        RegisteredPluginState state;
        lock (_sync)
        {
            if (!_plugins.TryGetValue(moduleId, out var existing))
            {
                return new PluginUnloadOutcome(Removed: false, Drained: true, InFlightAtUnload: 0);
            }

            _activityTracker.Seal(moduleId);
            _plugins.Remove(moduleId);
            state = existing;
        }

        var drained = await _activityTracker.WaitForDrainAsync(moduleId, _options.DrainTimeout, cancellationToken);
        var inFlight = _activityTracker.GetInFlight(moduleId);

        if (!drained)
        {
            _logger.LogWarning(
                "Plugin {PluginKey} wird entladen, obwohl {InFlightCount} Aufruf(e) nach {DrainSeconds}s noch laufen.",
                moduleId,
                inFlight,
                _options.DrainTimeout.TotalSeconds);
        }

        Unload(state.LoadContext);
        return new PluginUnloadOutcome(Removed: true, Drained: drained, InFlightAtUnload: drained ? 0 : inFlight);
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