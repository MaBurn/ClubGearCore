using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Plugins.Runtime;

/// <summary>CORE-13. See <see cref="IPluginProviderRegistry"/>.</summary>
public sealed class PluginProviderRegistry : IPluginProviderRegistry
{
    private readonly IPluginRegistryReader _registryReader;
    private readonly IPluginContributionActivator _contributionActivator;
    private readonly ILogger<PluginProviderRegistry> _logger;

    public PluginProviderRegistry(
        IPluginRegistryReader registryReader,
        IPluginContributionActivator contributionActivator,
        ILogger<PluginProviderRegistry> logger)
    {
        _registryReader = registryReader;
        _contributionActivator = contributionActivator;
        _logger = logger;
    }

    public PluginContributionLease<TProvider> Resolve<TProvider>(string capabilityKey) where TProvider : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityKey);

        var candidates = ResolveAll<TProvider>(capabilityKey);

        if (candidates.Count == 0)
        {
            return PluginContributionLease<TProvider>.Empty(
                string.Empty,
                capabilityKey,
                $"Kein Plugin stellt die Capability '{capabilityKey}' bereit.");
        }

        // Tolerating the collision is the whole point. Two plugins claiming one capability is a
        // configuration problem the administrator has to settle (CORE-14), not a reason to fail
        // the request — and certainly not a reason to fail scope construction the way
        // NotificationService's ToDictionary does today.
        if (candidates.Count > 1)
        {
            _logger.LogWarning(
                "Mehrere Plugins beanspruchen die Capability '{CapabilityKey}': {Modules}. "
                + "Bis zur Auswahl durch einen Administrator (CORE-14) wird '{Selected}' verwendet.",
                capabilityKey,
                string.Join(", ", candidates.Select(c => c.ModuleId)),
                candidates[0].ModuleId);
        }

        // The losers are disposed here: only the selected lease travels on to the caller.
        for (var index = 1; index < candidates.Count; index++)
        {
            candidates[index].Dispose();
        }

        return candidates[0].Lease;
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
