using System.IO.Compression;
using ClubGear.Services.Plugins.Manifest;

namespace ClubGear.Services.Plugins.Catalog;

public sealed class ZipPluginCatalogProvider : IPluginCatalogProvider
{
    private readonly IReadOnlyList<string> _zipPaths;
    private readonly PluginManifestParser _manifestParser;

    public ZipPluginCatalogProvider(IEnumerable<string> zipPaths, PluginManifestParser manifestParser)
    {
        _zipPaths = zipPaths.ToList();
        _manifestParser = manifestParser;
    }

    public Task<IReadOnlyList<PluginCatalogDescriptor>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        var descriptors = new List<PluginCatalogDescriptor>();
        foreach (var zipPath in _zipPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var archive = ZipFile.OpenRead(zipPath);
            var manifestEntry = archive.GetEntry("plugin-manifest.json");
            if (manifestEntry is null)
            {
                continue;
            }

            using var reader = new StreamReader(manifestEntry.Open());
            var manifestJson = reader.ReadToEnd();
            var parsed = _manifestParser.Parse(manifestJson);
            if (!parsed.IsValid || parsed.Manifest is null)
            {
                continue;
            }

            descriptors.Add(new PluginCatalogDescriptor(
                parsed.Manifest.ModuleId,
                parsed.Manifest.DisplayName,
                parsed.Manifest.PluginVersion,
                "zip",
                zipPath));
        }

        return Task.FromResult<IReadOnlyList<PluginCatalogDescriptor>>(descriptors);
    }
}