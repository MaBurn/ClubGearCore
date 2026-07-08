using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.ExternalLogin;

/// <summary>
/// Backed by <see cref="ISystemConfigService"/> for per-provider config get/save.
/// Provider discovery reads <see cref="RegisteredPluginRuntime.IdentityProviders"/> from
/// all currently-loaded plugins via <see cref="IPluginRegistryReader"/>.
/// </summary>
internal sealed class ExternalLoginConfigService : IExternalLoginConfigService
{
    private const string SectionPrefix = "externallogin.";
    private const string EnabledKey    = "enabled";

    private readonly ISystemConfigService  _configService;
    private readonly IPluginRegistryReader _registryReader;

    public ExternalLoginConfigService(
        ISystemConfigService  configService,
        IPluginRegistryReader registryReader)
    {
        _configService  = configService;
        _registryReader = registryReader;
    }

    // ------------------------------------------------------------------
    // GetConfigAsync
    // ------------------------------------------------------------------

    public async Task<IReadOnlyDictionary<string, string>> GetConfigAsync(
        string providerKey,
        CancellationToken ct = default)
    {
        var section = SectionFor(providerKey);
        var entries = await _configService.GetBySectionAsync(section, ct);

        return entries.ToDictionary(
            e => e.Name,
            e => e.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------
    // SaveConfigAsync
    // ------------------------------------------------------------------

    public async Task SaveConfigAsync(
        string providerKey,
        IReadOnlyDictionary<string, string> configValues,
        CancellationToken ct = default)
    {
        var section = SectionFor(providerKey);

        foreach (var (name, value) in configValues)
        {
            await _configService.UpsertAsync(section, name, value, description: string.Empty, ct);
        }
    }

    // ------------------------------------------------------------------
    // TestConnectionAsync
    // ------------------------------------------------------------------

    public async Task<PluginExternalLoginTestResult> TestConnectionAsync(
        string providerKey,
        CancellationToken ct = default)
    {
        // Resolve the plugin and its contribution.
        var (runtime, contribution) = FindContribution(providerKey);

        if (runtime is null || contribution is null)
        {
            return new PluginExternalLoginTestResult(
                Success: false,
                Message: $"No identity-provider plugin found for key '{providerKey}'.");
        }

        var plugin = _registryReader.CreateMemberProvider<IIdentityProviderPlugin>(
            runtime.ModuleId, contribution.ProviderType);

        if (plugin is null)
        {
            return new PluginExternalLoginTestResult(
                Success: false,
                Message: $"Could not instantiate provider type '{contribution.ProviderType}'.");
        }

        var config = await GetConfigAsync(providerKey, ct);
        return await plugin.TestConnectionAsync(config, ct);
    }

    // ------------------------------------------------------------------
    // GetAllDeclaredProvidersAsync
    // ------------------------------------------------------------------

    public async Task<IReadOnlyList<ExternalProviderInfo>> GetAllDeclaredProvidersAsync(
        CancellationToken ct = default)
    {
        var result = new List<ExternalProviderInfo>();

        foreach (var runtime in _registryReader.GetRegisteredPlugins())
        {
            foreach (var contribution in runtime.IdentityProviders)
            {
                // Try to resolve a live plugin instance to get its display name.
                var plugin = _registryReader.CreateMemberProvider<IIdentityProviderPlugin>(
                    runtime.ModuleId, contribution.ProviderType);

                var displayName = plugin?.DisplayName ?? contribution.ProviderKey;

                // Read the enabled flag from config.
                var section   = SectionFor(contribution.ProviderKey);
                var rawEnabled = await _configService.GetValueAsync(section, EnabledKey, ct);
                var isEnabled  = string.Equals(rawEnabled, "true", StringComparison.OrdinalIgnoreCase);

                result.Add(new ExternalProviderInfo(
                    ProviderKey: contribution.ProviderKey,
                    DisplayName: displayName,
                    ModuleId:    runtime.ModuleId,
                    IsEnabled:   isEnabled));
            }
        }

        return result;
    }

    // ------------------------------------------------------------------
    // private helpers
    // ------------------------------------------------------------------

    private static string SectionFor(string providerKey)
        => $"{SectionPrefix}{providerKey}";

    private (RegisteredPluginRuntime? Runtime, PluginIdentityProviderContribution? Contribution)
        FindContribution(string providerKey)
    {
        foreach (var runtime in _registryReader.GetRegisteredPlugins())
        {
            foreach (var contribution in runtime.IdentityProviders)
            {
                if (string.Equals(contribution.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase))
                    return (runtime, contribution);
            }
        }

        return (null, null);
    }
}
