using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Abstractions;

public interface IIdpClaimsEnricher
{
    /// <summary>
    /// Dispatches <paramref name="context"/> to the <see cref="IIdentityProviderPlugin"/>
    /// registered under <paramref name="providerKey"/> and returns the merged list of
    /// <see cref="PluginClaimEntry"/> values produced by the plugin.
    /// Returns an empty list when no matching plugin is found.
    /// </summary>
    Task<IReadOnlyList<PluginClaimEntry>> EnrichAsync(
        string providerKey,
        PluginExternalLoginContext context,
        CancellationToken ct = default);
}
