using System.Security.Claims;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ClubGear.Services.Plugins.Runtime;

public sealed class PluginEndpointRegistrar
{
    private readonly IPluginRuntimeAdapter? _runtimeAdapter;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IPluginRuntimeRegistry? _runtimeRegistry;
    private readonly Dictionary<string, RegisteredEndpoint> _endpoints = new(StringComparer.OrdinalIgnoreCase);

    public PluginEndpointRegistrar(IPluginRuntimeAdapter runtimeAdapter)
        : this(runtimeAdapter, runtimeRegistry: null)
    {
    }

    public PluginEndpointRegistrar(IPluginRuntimeAdapter runtimeAdapter, IPluginRuntimeRegistry? runtimeRegistry)
    {
        _runtimeAdapter = runtimeAdapter;
        _runtimeRegistry = runtimeRegistry;
    }

    [ActivatorUtilitiesConstructor]
    public PluginEndpointRegistrar(IServiceScopeFactory scopeFactory, IPluginRuntimeRegistry runtimeRegistry)
    {
        _scopeFactory = scopeFactory;
        _runtimeRegistry = runtimeRegistry;
    }

    public IReadOnlyCollection<PluginRouteContribution> Registrations
        => _endpoints.Values
            .Select(endpoint => new PluginRouteContribution(endpoint.RoutePattern, endpoint.PermissionKey))
            .ToArray();

    public void RegisterGet(
        IPluginModule pluginModule,
        string routePattern,
        string permissionKey,
        Func<IPluginRuntimeBridge, CancellationToken, Task<PluginEndpointResult>> handler)
    {
        ArgumentNullException.ThrowIfNull(pluginModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(routePattern);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);
        ArgumentNullException.ThrowIfNull(handler);

        if (!routePattern.StartsWith("/", StringComparison.Ordinal))
        {
            throw new ValidationException("Plugin-Route muss mit '/' beginnen.");
        }

        EnsureIsolated(handler);

        var moduleId = pluginModule.Manifest.ModuleId;
        var key = BuildKey(moduleId, routePattern);

        if (_endpoints.ContainsKey(key))
        {
            throw new ValidationException("Plugin-Route ist bereits registriert.");
        }

        _endpoints[key] = new RegisteredEndpoint(moduleId, routePattern, permissionKey, handler);
        _runtimeRegistry?.AddOrReplaceRoute(moduleId, new PluginRouteContribution(routePattern, permissionKey));
    }

    public async Task<PluginEndpointResult> InvokeGetAsync(
        IPluginModule pluginModule,
        string routePattern,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(routePattern);
        ArgumentNullException.ThrowIfNull(user);

        var moduleId = pluginModule.Manifest.ModuleId;
        var key = BuildKey(moduleId, routePattern);

        if (!_endpoints.TryGetValue(key, out var endpoint))
        {
            throw new NotFoundException("Plugin-Route", key);
        }

        if (_runtimeAdapter is not null)
        {
            return await InvokeWithRuntimeAdapterAsync(_runtimeAdapter, pluginModule, endpoint, user, cancellationToken);
        }

        using var scope = _scopeFactory!.CreateScope();
        var runtimeAdapter = scope.ServiceProvider.GetRequiredService<IPluginRuntimeAdapter>();
        return await InvokeWithRuntimeAdapterAsync(runtimeAdapter, pluginModule, endpoint, user, cancellationToken);
    }

    public void Unregister(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var prefix = string.Concat(moduleId, ':');
        var keysToRemove = _endpoints.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var key in keysToRemove)
        {
            _endpoints.Remove(key);
        }
    }

    private static string BuildKey(string moduleId, string routePattern)
        => string.Concat(moduleId, ':', routePattern);

    private void EnsureIsolated(Delegate pluginDelegate)
    {
        if (_runtimeAdapter is not null)
        {
            _runtimeAdapter.EnsureIsolated(pluginDelegate);
            return;
        }

        using var scope = _scopeFactory!.CreateScope();
        scope.ServiceProvider.GetRequiredService<IPluginRuntimeAdapter>().EnsureIsolated(pluginDelegate);
    }

    private static async Task<PluginEndpointResult> InvokeWithRuntimeAdapterAsync(
        IPluginRuntimeAdapter runtimeAdapter,
        IPluginModule pluginModule,
        RegisteredEndpoint endpoint,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        try
        {
            return await runtimeAdapter.InvokeAsync(
                pluginModule,
                user,
                endpoint.Handler,
                endpoint.PermissionKey,
                isolatedDelegate: endpoint.Handler,
                cancellationToken: cancellationToken);
        }
        catch (PluginPermissionDeniedException)
        {
            return PluginEndpointResult.Forbidden();
        }
    }

    private sealed record RegisteredEndpoint(
        string ModuleId,
        string RoutePattern,
        string PermissionKey,
        Func<IPluginRuntimeBridge, CancellationToken, Task<PluginEndpointResult>> Handler);
}

public sealed record PluginEndpointResult(int StatusCode, string? Body = null, string ContentType = "text/plain")
{
    public static PluginEndpointResult Forbidden()
        => new(403, "Forbidden");
}