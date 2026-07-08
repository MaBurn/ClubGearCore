namespace ClubGear.Services.Plugins.Catalog;

public interface IPluginCatalogProvider
{
    Task<IReadOnlyList<PluginCatalogDescriptor>> GetAvailableAsync(CancellationToken cancellationToken = default);
}

public sealed record PluginCatalogDescriptor(
    string ModuleId,
    string DisplayName,
    Version PluginVersion,
    string Source,
    string Location,
    string? ExpectedSha256Hex = null,
    string? SignatureBase64 = null,
    string? SignerPublicKeyPem = null);