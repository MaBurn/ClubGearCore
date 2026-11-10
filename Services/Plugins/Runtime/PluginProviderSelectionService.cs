using System.Collections.Concurrent;
using ClubGear.Data;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Plugins.Runtime;

/// <summary>CORE-14. See <see cref="IPluginProviderSelectionService"/>.</summary>
/// <remarks>
/// Persisted in <c>SystemConfigEntries</c> (section <see cref="ConfigSection"/>, name = capability
/// key, value = module id, empty = core default), so no schema change. Singleton, because
/// <see cref="PluginProviderRegistry"/> is one and reads the selection on every resolve.
/// </remarks>
public sealed class PluginProviderSelectionService : IPluginProviderSelectionService
{
    public const string ConfigSection = "plugin.provider-selection";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPluginRegistryReader _registryReader;

    // ponytail: per-process cache, invalidated only by SelectAsync in this process. Fine for the
    // single-instance SQLite deployment; a multi-instance host needs a shared invalidation signal.
    private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.Ordinal);

    public PluginProviderSelectionService(IServiceScopeFactory scopeFactory, IPluginRegistryReader registryReader)
    {
        _scopeFactory = scopeFactory;
        _registryReader = registryReader;
    }

    public string? GetSelectedModuleId(string capabilityKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityKey);

        return _cache.GetOrAdd(capabilityKey, key =>
        {
            // Synchronous on purpose: IPluginProviderRegistry.Resolve is synchronous, and a sync
            // EF query is better than blocking on an async one.
            using var scope = _scopeFactory.CreateScope();
            var value = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .SystemConfigEntries
                .AsNoTracking()
                .Where(entry => entry.Section == ConfigSection && entry.Name == key)
                .Select(entry => entry.Value)
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(value) ? null : value;
        });
    }

    public IReadOnlyList<PluginProviderSelectionStatus> GetStatuses()
        => PluginCapabilityKeys.All
            .Where(key => PluginCapabilityKeys.GetArity(key) == PluginCapabilityArity.SingleActive)
            .Order(StringComparer.Ordinal)
            .Select(key => new PluginProviderSelectionStatus(key, GetSelectedModuleId(key), GetClaimants(key)))
            .ToArray();

    public async Task<PluginProviderSelectionResult> SelectAsync(
        string capabilityKey,
        string? moduleId,
        string? actor,
        CancellationToken cancellationToken = default)
    {
        if (!PluginCapabilityKeys.IsKnown(capabilityKey)
            || PluginCapabilityKeys.GetArity(capabilityKey) != PluginCapabilityArity.SingleActive)
        {
            return new(false, "Für diese Capability gibt es keine Provider-Auswahl.");
        }

        string? selected = null;
        if (!string.IsNullOrWhiteSpace(moduleId))
        {
            selected = GetClaimants(capabilityKey)
                .FirstOrDefault(claimant => string.Equals(claimant, moduleId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                return new(false, $"Das Plugin '{moduleId.Trim()}' ist nicht aktiv oder stellt '{capabilityKey}' nicht bereit.");
            }
        }

        var before = GetSelectedModuleId(capabilityKey);

        using (var scope = _scopeFactory.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISystemConfigService>().UpsertAsync(
                ConfigSection,
                capabilityKey,
                selected ?? string.Empty,
                "CORE-14: ausgewählter Provider (Modul-ID); leer = Core-Standard.",
                cancellationToken);

            _cache[capabilityKey] = selected;

            await scope.ServiceProvider.GetRequiredService<IAuditLogService>().LogChangeAsync(
                action: "plugin.provider.selected",
                before: new { ModuleId = before },
                after: new { ModuleId = selected },
                actor: actor,
                source: "PluginAdmin",
                targetType: "PluginCapability",
                targetId: capabilityKey,
                cancellationToken: cancellationToken);
        }

        return new(true, selected is null
            ? $"'{capabilityKey}' nutzt jetzt den Core-Standard."
            : $"'{capabilityKey}' wird jetzt von '{selected}' bereitgestellt.");
    }

    private string[] GetClaimants(string capabilityKey)
        => _registryReader.GetRegisteredPlugins()
            .Where(runtime => runtime.Services.Any(service => string.Equals(service.Key, capabilityKey, StringComparison.Ordinal)))
            .Select(runtime => runtime.ModuleId)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
