using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
    private readonly IPluginContributionActivator _contributionActivator;
    private readonly IOptionsMonitorCache<OpenIdConnectOptions>? _optionsCache;
    private readonly ILogger<ExternalLoginConfigService> _logger;

    public ExternalLoginConfigService(
        ISystemConfigService  configService,
        IPluginRegistryReader registryReader,
        IPluginContributionActivator contributionActivator,
        IOptionsMonitorCache<OpenIdConnectOptions>? optionsCache = null,
        ILogger<ExternalLoginConfigService>? logger = null)
    {
        _configService  = configService;
        _registryReader = registryReader;
        _contributionActivator = contributionActivator;
        _optionsCache = optionsCache;
        _logger = logger ?? NullLogger<ExternalLoginConfigService>.Instance;
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

        return entries
            .GroupBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().Value,
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
        var existing = await GetConfigAsync(providerKey, ct);
        var secretKeys = GetSecretKeys(providerKey);

        foreach (var (name, value) in configValues)
        {
            if (secretKeys.Contains(name)
                && string.IsNullOrWhiteSpace(value)
                && existing.TryGetValue(name, out var storedValue)
                && !string.IsNullOrWhiteSpace(storedValue))
            {
                continue;
            }

            await _configService.UpsertAsync(section, name, value, description: string.Empty, ct);
        }

        _optionsCache?.TryRemove($"oidc.{providerKey}");
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

        await using var lease = RentProvider(runtime, contribution);
        var plugin = lease.Instance;

        if (plugin is null)
        {
            return new PluginExternalLoginTestResult(
                Success: false,
                Message: $"Could not instantiate provider type '{contribution.ProviderType}': {lease.Error}");
        }

        string pluginProviderKey;
        try
        {
            pluginProviderKey = plugin.ProviderKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Could not read the provider key from identity-provider type '{ProviderType}'.",
                contribution.ProviderType);
            return new PluginExternalLoginTestResult(
                Success: false,
                Message: $"Could not inspect provider type '{contribution.ProviderType}'.");
        }

        if (!string.Equals(pluginProviderKey, contribution.ProviderKey, StringComparison.OrdinalIgnoreCase))
        {
            return new PluginExternalLoginTestResult(
                Success: false,
                Message: $"Provider type '{contribution.ProviderType}' declares key " +
                         $"'{pluginProviderKey}' instead of '{contribution.ProviderKey}'.");
        }

        var config = await GetConfigAsync(providerKey, ct);
        try
        {
            return await plugin.TestConnectionAsync(config, ct)
                ?? new PluginExternalLoginTestResult(
                    Success: false,
                    Message: "The identity-provider connection test returned no result.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Identity-provider connection test failed for '{ProviderKey}'.", providerKey);
            return new PluginExternalLoginTestResult(
                Success: false,
                Message: "The identity-provider connection test failed.");
        }
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
                await using var lease = RentProvider(runtime, contribution);
                var plugin = lease.Instance;
                var displayName = contribution.ProviderKey;
                var isAvailable = false;
                if (plugin is not null)
                {
                    try
                    {
                        displayName = plugin.DisplayName;
                        isAvailable = ExternalLoginServiceProviderKey.IsValid(contribution.ProviderKey)
                            && string.Equals(
                            plugin.ProviderKey,
                            contribution.ProviderKey,
                            StringComparison.OrdinalIgnoreCase);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Could not inspect identity-provider type '{ProviderType}'.",
                            contribution.ProviderType);
                    }
                }

                // Read the enabled flag from config.
                var section   = SectionFor(contribution.ProviderKey);
                var rawEnabled = await _configService.GetValueAsync(section, EnabledKey, ct);
                var isEnabled  = string.Equals(rawEnabled, "true", StringComparison.OrdinalIgnoreCase);

                result.Add(new ExternalProviderInfo(
                    ProviderKey: contribution.ProviderKey,
                    DisplayName: displayName,
                    ModuleId:    runtime.ModuleId,
                    IsEnabled:   isEnabled,
                    IsAvailable: isAvailable));
            }
        }

        var duplicateKeys = result
            .GroupBy(provider => provider.ProviderKey, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return result
            .Select(provider => duplicateKeys.Contains(provider.ProviderKey)
                ? provider with { IsAvailable = false }
                : provider)
            .ToList();
    }

    // ------------------------------------------------------------------
    // private helpers
    // ------------------------------------------------------------------

    private static string SectionFor(string providerKey)
        => $"{SectionPrefix}{providerKey}";

    private HashSet<string> GetSecretKeys(string providerKey)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // This key is interpreted as a secret by the core even when a broken or
            // concurrently unloaded plugin cannot provide its schema.
            "clientsecret"
        };
        var (runtime, contribution) = FindContribution(providerKey);
        if (runtime is null || contribution is null)
        {
            return result;
        }

        // Synchronous on purpose — the lease is IDisposable as well as IAsyncDisposable.
        using var lease = RentProvider(runtime, contribution);
        var plugin = lease.Instance;

        if (plugin is null)
        {
            return result;
        }

        try
        {
            result.UnionWith(plugin.GetConfigSchema()
                .Where(field => field.InputType == PluginSchemaFieldType.Secret)
                .Select(field => field.Key));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Could not read the configuration schema for identity provider '{ProviderKey}'.",
                providerKey);
        }

        return result;
    }

    /// <summary>
    /// CORE-18. The caller owns the lease and therefore owns the window in which plugin code is
    /// live; the activator never throws for a plugin-side problem, it returns an empty lease
    /// carrying the reason.
    /// </summary>
    private PluginContributionLease<IIdentityProviderPlugin> RentProvider(
        RegisteredPluginRuntime runtime,
        PluginIdentityProviderContribution contribution)
    {
        var lease = _contributionActivator.Rent<IIdentityProviderPlugin>(
            runtime.ModuleId,
            contribution.ProviderType);

        if (lease.Instance is null)
        {
            _logger.LogError(
                "Could not instantiate identity-provider type '{ProviderType}' for plugin '{ModuleId}': {Reason}",
                contribution.ProviderType,
                runtime.ModuleId,
                lease.Error);
        }

        return lease;
    }

    private (RegisteredPluginRuntime? Runtime, PluginIdentityProviderContribution? Contribution)
        FindContribution(string providerKey)
    {
        (RegisteredPluginRuntime Runtime, PluginIdentityProviderContribution Contribution)? match = null;
        foreach (var runtime in _registryReader.GetRegisteredPlugins())
        {
            foreach (var contribution in runtime.IdentityProviders)
            {
                if (string.Equals(contribution.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (match is not null)
                    {
                        return (null, null);
                    }

                    match = (runtime, contribution);
                }
            }
        }

        return match is null
            ? (null, null)
            : (match.Value.Runtime, match.Value.Contribution);
    }
}
