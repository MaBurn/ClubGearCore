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
    private readonly ILogger<IdpClaimsEnricher> _logger;

    public IdpClaimsEnricher(
        IPluginRegistryReader registryReader,
        ILogger<IdpClaimsEnricher> logger)
    {
        _registryReader = registryReader;
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

                var plugin = _registryReader.CreateMemberProvider<IIdentityProviderPlugin>(
                    runtime.ModuleId, contribution.ProviderType);

                if (plugin is null)
                {
                    _logger.LogWarning(
                        "IdpClaimsEnricher: could not instantiate provider type '{ProviderType}' " +
                        "for plugin '{ModuleId}' (key '{ProviderKey}').",
                        contribution.ProviderType, runtime.ModuleId, providerKey);
                    continue;
                }

                var claims = await plugin.MapClaimsAsync(context, ct);
                return claims;
            }
        }

        return Array.Empty<PluginClaimEntry>();
    }
}
