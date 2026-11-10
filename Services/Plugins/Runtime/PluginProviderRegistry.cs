using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Plugins.Runtime;

/// <summary>CORE-13. See <see cref="IPluginProviderRegistry"/>.</summary>
public sealed class PluginProviderRegistry : IPluginProviderRegistry
{
    private readonly IPluginRegistryReader _registryReader;
    private readonly IPluginContributionActivator _contributionActivator;
    private readonly IPluginProviderSelectionService _selection;
    private readonly ILogger<PluginProviderRegistry> _logger;

    public PluginProviderRegistry(
        IPluginRegistryReader registryReader,
        IPluginContributionActivator contributionActivator,
        IPluginProviderSelectionService selection,
        ILogger<PluginProviderRegistry> logger)
    {
        _registryReader = registryReader;
        _contributionActivator = contributionActivator;
        _selection = selection;
        _logger = logger;
    }

    public PluginContributionLease<TProvider> Resolve<TProvider>(string capabilityKey) where TProvider : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityKey);

        // CORE-14: only the administrator's choice is resolved. Without one the core default
        // serves the capability, however many plugins claim it, so installing a plugin never
        // silently changes where data goes. A collision is therefore a resolvable state.
        var selected = _selection.GetSelectedModuleId(capabilityKey);
        if (selected is null)
        {
            return PluginContributionLease<TProvider>.Empty(
                string.Empty,
                capabilityKey,
                $"Fuer '{capabilityKey}' ist kein Plugin-Provider ausgewaehlt; der Core-Standard gilt.");
        }

        var registration = GetRegistrations(capabilityKey)
            .FirstOrDefault(r => string.Equals(r.ModuleId, selected, StringComparison.OrdinalIgnoreCase));
        if (registration is null)
        {
            _logger.LogWarning(
                "Der fuer '{CapabilityKey}' ausgewaehlte Provider '{ModuleId}' ist nicht aktiv; der Core-Standard gilt.",
                capabilityKey,
                selected);
            return PluginContributionLease<TProvider>.Empty(
                selected,
                capabilityKey,
                $"Das ausgewaehlte Plugin '{selected}' ist nicht aktiv; der Core-Standard gilt.");
        }

        var lease = _contributionActivator.Rent<TProvider>(registration.ModuleId, registration.ProviderType);
        if (lease.Instance is null)
        {
            _logger.LogWarning(
                "Provider '{ProviderType}' aus Plugin '{ModuleId}' fuer Capability '{CapabilityKey}' "
                + "konnte nicht als {Contract} erzeugt werden: {Reason}",
                registration.ProviderType,
                registration.ModuleId,
                capabilityKey,
                typeof(TProvider).Name,
                lease.Error);
        }

        return lease;
    }

    public IReadOnlyList<PluginProvider<TProvider>> ResolveAll<TProvider>(string capabilityKey)
        where TProvider : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityKey);

        var resolved = new List<PluginProvider<TProvider>>();

        foreach (var registration in GetRegistrations(capabilityKey))
        {
            var lease = _contributionActivator.Rent<TProvider>(
                registration.ModuleId, registration.ProviderType);

            if (lease.Instance is null)
            {
                // Either the type is missing from the plugin assembly or it does not implement the
                // contract the caller asked for. Both are authoring errors in the plugin, and both
                // are silent today; naming the plugin is what makes them findable.
                _logger.LogWarning(
                    "Provider '{ProviderType}' aus Plugin '{ModuleId}' fuer Capability "
                    + "'{CapabilityKey}' konnte nicht als {Contract} erzeugt werden: {Reason}",
                    registration.ProviderType,
                    registration.ModuleId,
                    capabilityKey,
                    typeof(TProvider).Name,
                    lease.Error);
                lease.Dispose();
                continue;
            }

            resolved.Add(new PluginProvider<TProvider>(lease));
        }

        return resolved;
    }

    public IReadOnlyList<PluginProviderRegistration> GetRegistrations(string capabilityKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityKey);

        return _registryReader.GetRegisteredPlugins()
            .SelectMany(runtime => runtime.Services
                .Where(service => string.Equals(service.Key, capabilityKey, StringComparison.Ordinal))
                .Select(service => new PluginProviderRegistration(
                    runtime.ModuleId, service.Key, service.ServiceType)))
            // GetRegisteredPlugins already orders by module id; ordering again keeps the sequence
            // stable if that ever changes, because callers of ResolveAll depend on it.
            .OrderBy(registration => registration.ModuleId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(registration => registration.ProviderType, StringComparer.Ordinal)
            .ToArray();
    }
}
