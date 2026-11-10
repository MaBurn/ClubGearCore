using System.Security.Claims;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Core;

public sealed class PluginNavEntryService : IPluginNavEntryService
{
    private readonly IPluginRegistryReader _registryReader;
    private readonly IPermissionService _permissionService;

    public PluginNavEntryService(IPluginRegistryReader registryReader, IPermissionService permissionService)
    {
        _registryReader = registryReader;
        _permissionService = permissionService;
    }

    public async Task<IReadOnlyList<PluginNavEntry>> GetVisibleNavEntriesAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var candidates = _registryReader.GetRegisteredPlugins()
            .SelectMany(runtime => runtime.NavEntries)
            .Where(entry => IsLocalRoute(entry.Route))
            .OrderBy(entry => entry.SortOrder)
            .ThenBy(entry => entry.Label)
            .ToList();

        var entries = new List<PluginNavEntry>(candidates.Count);
        foreach (var entry in candidates)
        {
            if (entry.RequiredPermission is null
                || await _permissionService.HasPermissionAsync(user, entry.RequiredPermission, ct))
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>
    /// CORE-31. A plugin nav entry may only point into this host: an app-relative path. That rules
    /// out <c>javascript:</c>/<c>data:</c> links and protocol-relative (<c>//</c>) or backslash
    /// redirects to another origin.
    /// </summary>
    internal static bool IsLocalRoute(string? route)
        => !string.IsNullOrEmpty(route)
           && route[0] == '/'
           && (route.Length == 1 || (route[1] != '/' && route[1] != '\\'))
           // Browsers strip tab/newline from URLs, so "/\t/evil.example" would become "//evil.example".
           && !route.Any(c => char.IsControl(c) || char.IsWhiteSpace(c));
}
