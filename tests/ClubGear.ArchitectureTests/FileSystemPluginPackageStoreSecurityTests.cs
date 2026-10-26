using ClubGear.Services.Plugins.Installation;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class FileSystemPluginPackageStoreSecurityTests
{
    private const string ValidHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData("../outside")]
    [InlineData("plugin/subdirectory")]
    [InlineData(".hidden")]
    public async Task SaveAsync_RejectsUnsafePluginKeys(string pluginKey)
    {
        var root = CreateTemporaryRoot();
        try
        {
            var store = new FileSystemPluginPackageStore(root);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                store.SaveAsync(pluginKey, ValidHash, []));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_RejectsNonSha256PackageHash()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var store = new FileSystemPluginPackageStore(root);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                store.SaveAsync("clubgear.safe-plugin", "../outside", []));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureExtractedAsync_RejectsPackagePathOutsideStore()
    {
        var root = CreateTemporaryRoot();
        var externalPackage = Path.Combine(Path.GetTempPath(), $"clubgear-external-{Guid.NewGuid():N}.zip");
        try
        {
            await File.WriteAllBytesAsync(externalPackage, []);
            var store = new FileSystemPluginPackageStore(root);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                store.EnsureExtractedAsync("clubgear.safe-plugin", ValidHash, externalPackage));
        }
        finally
        {
            File.Delete(externalPackage);
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "clubgear-package-security-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
