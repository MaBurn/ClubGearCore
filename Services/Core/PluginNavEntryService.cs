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
}
