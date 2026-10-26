using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.ExternalLogin;

/// <summary>
/// Iterates <see cref="RegisteredPluginRuntime.IdentityProviders"/> across all loaded plugins,
/// resolves the <see cref="IIdentityProviderPlugin"/> that matches <c>providerKey</c>,
/// calls <see cref="IIdentityProviderPlugin.MapClaimsAsync"/>, and returns the merged
/// <see cref="PluginClaimEntry"/> list.
/// </summary>
internal sealed class IdpClaimsEnricher : IIdpClaimsEnricher
{
    private readonly IPluginRegistryReader _registryReader;
    private readonly IPluginContributionActivator _contributionActivator;
    private readonly ILogger<IdpClaimsEnricher> _logger;

    public IdpClaimsEnricher(
        IPluginRegistryReader registryReader,
        IPluginContributionActivator contributionActivator,
        ILogger<IdpClaimsEnricher> logger)
    {
        _registryReader = registryReader;
        _contributionActivator = contributionActivator;
        _logger         = logger;
    }

    public async Task<IReadOnlyList<PluginClaimEntry>> EnrichAsync(
        string providerKey,
        PluginExternalLoginContext context,
        CancellationToken ct = default)
    {
        foreach (var runtime in _registryReader.GetRegisteredPlugins())
        {
            foreach (var contribution in runtime.IdentityProviders)
            {
                if (!string.Equals(contribution.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                await using var lease = _contributionActivator.Rent<IIdentityProviderPlugin>(
                    runtime.ModuleId, contribution.ProviderType);

                // CORE-18. Still fails closed: a login must not silently lose its claims. What
                // changes is that the reason from the lease travels with the exception.
                if (lease.Instance is not { } plugin)
                {
                    _logger.LogError(
                        "IdpClaimsEnricher: could not instantiate provider type '{ProviderType}' " +
                        "for plugin '{ModuleId}' (key '{ProviderKey}'): {Reason}",
                        contribution.ProviderType, runtime.ModuleId, providerKey, lease.Error);
                    throw new InvalidOperationException(
                        $"Could not instantiate the mapper for identity provider '{providerKey}': {lease.Error}");
                }

                if (!string.Equals(
                    plugin.ProviderKey,
                    contribution.ProviderKey,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Identity provider '{contribution.ProviderType}' declares key " +
                        $"'{plugin.ProviderKey}' instead of '{contribution.ProviderKey}'.");
                }

                return await plugin.MapClaimsAsync(context, ct)
                    ?? throw new InvalidOperationException(
                        $"Identity provider '{providerKey}' returned a null claim mapping.");
            }
        }

        throw new InvalidOperationException(
            $"No claim mapper is registered for identity provider '{providerKey}'.");
    }
}
