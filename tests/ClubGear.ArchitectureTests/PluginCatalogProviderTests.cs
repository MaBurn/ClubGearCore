using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using ClubGear.Services.Plugins.Catalog;
using ClubGear.Services.Plugins.Manifest;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginCatalogProviderTests
{
    [Fact]
    public async Task MarketplaceProvider_ReturnsDescriptors_FromMarketplaceJson()
    {
        var payload =
            """
            [
              {
                "moduleId": "clubgear.marketplace.members",
                "displayName": "Members Marketplace Plugin",
                "pluginVersion": "2.0.0",
                "location": "https://plugins.example/members-2.0.0.zip"
              }
            ]
            """;

        using var handler = new StaticResponseHandler(payload);
        using var httpClient = new HttpClient(handler, disposeHandler: true);
        var sut = new MarketplacePluginCatalogProvider(httpClient, "https://plugins.example/catalog");

        var result = await sut.GetAvailableAsync();

        var descriptor = Assert.Single(result);
        Assert.Equal("clubgear.marketplace.members", descriptor.ModuleId);
        Assert.Equal("marketplace", descriptor.Source);
        Assert.Equal(new Version(2, 0, 0), descriptor.PluginVersion);
    }

    [Fact]
    public async Task ZipProvider_ReturnsDescriptors_FromValidZipManifest()
    {
        var zipPath = Path.Combine(Path.GetTempPath(), $"clubgear-plugin-{Guid.NewGuid():N}.zip");
        try
        {
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("plugin-manifest.json");
                await using var stream = entry.Open();
                await using var writer = new StreamWriter(stream, Encoding.UTF8);
                await writer.WriteAsync(
                    """
                    {
                      "moduleId": "clubgear.zip.members",
                      "displayName": "Zip Members Plugin",
                      "pluginVersion": "1.1.0",
                      "requiredContractVersion": "1.0.0",
                      "entryPointType": "Zip.Members.PluginModule"
                    }
                    """);
            }

            var sut = new ZipPluginCatalogProvider(new[] { zipPath }, new PluginManifestParser());

            var result = await sut.GetAvailableAsync();

            var descriptor = Assert.Single(result);
            Assert.Equal("clubgear.zip.members", descriptor.ModuleId);
            Assert.Equal("zip", descriptor.Source);
            Assert.Equal(zipPath, descriptor.Location);
        }
        finally
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly string _payload;

        public StaticResponseHandler(string payload)
        {
            _payload = payload;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_payload, Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }
}