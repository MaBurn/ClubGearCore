using System.Text.Json;

namespace ClubGear.Services.Plugins.Catalog;

public sealed class MarketplacePluginCatalogProvider : IPluginCatalogProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _catalogEndpoint;

    public MarketplacePluginCatalogProvider(HttpClient httpClient, string catalogEndpoint)
    {
        _httpClient = httpClient;
        _catalogEndpoint = catalogEndpoint;
    }

    public async Task<IReadOnlyList<PluginCatalogDescriptor>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        var payload = await _httpClient.GetStringAsync(_catalogEndpoint, cancellationToken);
        using var document = JsonDocument.Parse(payload);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Marketplace catalog response must be a JSON array.");
        }

        var descriptors = new List<PluginCatalogDescriptor>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var moduleId = item.GetProperty("moduleId").GetString()
                ?? throw new InvalidOperationException("Marketplace plugin entry is missing moduleId.");
            var displayName = item.GetProperty("displayName").GetString()
                ?? throw new InvalidOperationException("Marketplace plugin entry is missing displayName.");
            var pluginVersionRaw = item.GetProperty("pluginVersion").GetString()
                ?? throw new InvalidOperationException("Marketplace plugin entry is missing pluginVersion.");
            var location = item.GetProperty("location").GetString()
                ?? throw new InvalidOperationException("Marketplace plugin entry is missing location.");
            var expectedSha256Hex = item.TryGetProperty("expectedSha256Hex", out var expectedHashProperty)
                ? expectedHashProperty.GetString()
                : null;
            var signatureBase64 = item.TryGetProperty("signatureBase64", out var signatureProperty)
                ? signatureProperty.GetString()
                : null;
            var signerPublicKeyPem = item.TryGetProperty("signerPublicKeyPem", out var publicKeyProperty)
                ? publicKeyProperty.GetString()
                : null;

            if (!Version.TryParse(pluginVersionRaw, out var pluginVersion))
            {
                throw new InvalidOperationException($"Marketplace plugin entry has invalid pluginVersion '{pluginVersionRaw}'.");
            }

            descriptors.Add(new PluginCatalogDescriptor(
                moduleId,
                displayName,
                pluginVersion,
                "marketplace",
                location,
                expectedSha256Hex,
                signatureBase64,
                signerPublicKeyPem));
        }

        return descriptors;
    }
}