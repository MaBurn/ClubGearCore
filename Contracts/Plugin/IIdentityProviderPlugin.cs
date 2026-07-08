namespace ClubGear.Plugin.Contracts;

public interface IIdentityProviderPlugin
{
    string ProviderKey { get; }

    string DisplayName { get; }

    IReadOnlyList<PluginFieldSchema> GetConfigSchema();

    Task<PluginExternalLoginTestResult> TestConnectionAsync(
        IReadOnlyDictionary<string, string> config,
        CancellationToken ct = default);

    Task<IReadOnlyList<PluginClaimEntry>> MapClaimsAsync(
        PluginExternalLoginContext context,
        CancellationToken ct = default);
}
