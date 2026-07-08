using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Abstractions;

public interface IExternalLoginConfigService
{
    /// <summary>
    /// Returns all config values stored for the given provider key as a dictionary
    /// keyed by config field name.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetConfigAsync(
        string providerKey,
        CancellationToken ct = default);

    /// <summary>
    /// Saves (upserts) every entry in <paramref name="configValues"/> under the
    /// section <c>externallogin.{providerKey}</c>.
    /// </summary>
    Task SaveConfigAsync(
        string providerKey,
        IReadOnlyDictionary<string, string> configValues,
        CancellationToken ct = default);

    /// <summary>
    /// Invokes the provider plugin's connectivity check and returns the result.
    /// </summary>
    Task<PluginExternalLoginTestResult> TestConnectionAsync(
        string providerKey,
        CancellationToken ct = default);

    /// <summary>
    /// Returns info records for every identity provider declared by any loaded plugin.
    /// </summary>
    Task<IReadOnlyList<ExternalProviderInfo>> GetAllDeclaredProvidersAsync(
        CancellationToken ct = default);
}
