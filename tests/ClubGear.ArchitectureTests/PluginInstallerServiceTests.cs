using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins;
using ClubGear.Services.Plugins.Catalog;
using ClubGear.Services.Plugins.Installation;
using ClubGear.Services.Plugins.Manifest;
using ClubGear.Services.Plugins.Security;
using ClubGear.Services.Plugins.Status;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginInstallerServiceTests
{
    [Fact]
    public async Task MarketplaceInstall_InstallsAndUpgrades_Module()
    {
        using var rsa = RSA.Create(2048);
        var mutableProvider = new MutableCatalogProvider();
        var downloader = new StaticPackageDownloader();
        await using var fixture = await CreateFixtureAsync();

        var v1 = CreateSignedPackage(
            moduleId: "clubgear.market.members",
            displayName: "Members Marketplace",
            pluginVersion: new Version(1, 0, 0),
            rsa);
        var v2 = CreateSignedPackage(
            moduleId: "clubgear.market.members",
            displayName: "Members Marketplace",
            pluginVersion: new Version(1, 1, 0),
            rsa);

        downloader.Packages["https://plugins.example/members-1.0.0.zip"] = v1.PackageBytes;
        downloader.Packages["https://plugins.example/members-1.1.0.zip"] = v2.PackageBytes;

        mutableProvider.Descriptors =
        [
            new PluginCatalogDescriptor(
                "clubgear.market.members",
                "Members Marketplace",
                new Version(1, 0, 0),
                "marketplace",
                "https://plugins.example/members-1.0.0.zip",
                v1.ExpectedSha256Hex,
                v1.SignatureBase64,
                v1.PublicKeyPem)
        ];

            var sut = CreateSut(mutableProvider, downloader, fixture.Store);

        var initialInstall = await sut.InstallOrUpgradeFromMarketplaceAsync("clubgear.market.members");

        Assert.True(initialInstall.Success);
        Assert.Equal("installed", initialInstall.Status);
        Assert.NotNull(initialInstall.Plugin);
        Assert.Equal(new Version(1, 0, 0), initialInstall.Plugin!.PluginVersion);
        Assert.Equal("Plugin Author", initialInstall.Plugin.Author);
        Assert.False(initialInstall.Plugin.IsActive);

        var storedAfterInstall = fixture.Store.GetByKey("clubgear.market.members");
        Assert.NotNull(storedAfterInstall);
        Assert.Equal("Proprietary", storedAfterInstall!.License);
        Assert.False(storedAfterInstall.IsActive);
        Assert.False(string.IsNullOrWhiteSpace(storedAfterInstall.PackagePath));
        Assert.True(File.Exists(storedAfterInstall.PackagePath));

        mutableProvider.Descriptors =
        [
            new PluginCatalogDescriptor(
                "clubgear.market.members",
                "Members Marketplace",
                new Version(1, 1, 0),
                "marketplace",
                "https://plugins.example/members-1.1.0.zip",
                v2.ExpectedSha256Hex,
                v2.SignatureBase64,
                v2.PublicKeyPem)
        ];

        var upgraded = await sut.InstallOrUpgradeFromMarketplaceAsync("clubgear.market.members");

        Assert.True(upgraded.Success);
        Assert.Equal("upgraded", upgraded.Status);
        Assert.NotNull(upgraded.Plugin);
        Assert.Equal(new Version(1, 1, 0), upgraded.Plugin!.PluginVersion);

        var storedAfterUpgrade = fixture.Store.GetByKey("clubgear.market.members");
        Assert.NotNull(storedAfterUpgrade);
        Assert.Equal("1.1.0", storedAfterUpgrade!.Version);
    }

    [Fact]
    public async Task ZipInstall_UsesCanonicalPluginJson_AndFallsBackToLegacyManifest()
    {
        using var rsa = RSA.Create(2048);
        await using var fixture = await CreateFixtureAsync();
        var sut = CreateSut(new MutableCatalogProvider(), new StaticPackageDownloader(), fixture.Store);

        var zipV1 = CreateSignedPackage(
            moduleId: "clubgear.zip.members",
            displayName: "Members ZIP",
            pluginVersion: new Version(2, 0, 0),
            rsa,
            useLegacyManifest: false);

        var installV1 = await sut.InstallOrUpgradeFromZipAsync(
            "members-2.0.0.zip",
            zipV1.PackageBytes,
            zipV1.ExpectedSha256Hex,
            zipV1.SignatureBase64,
            zipV1.PublicKeyPem);

        Assert.True(installV1.Success);
        Assert.Equal("installed", installV1.Status);
        Assert.Equal("Plugin Author", installV1.Plugin!.Author);

        var zipV2 = CreateSignedPackage(
            moduleId: "clubgear.zip.members",
            displayName: "Members ZIP",
            pluginVersion: new Version(2, 1, 0),
            rsa,
            useLegacyManifest: true);

        var installV2 = await sut.InstallOrUpgradeFromZipAsync(
            "members-2.1.0.zip",
            zipV2.PackageBytes,
            zipV2.ExpectedSha256Hex,
            zipV2.SignatureBase64,
            zipV2.PublicKeyPem);

        Assert.True(installV2.Success);
        Assert.Equal("upgraded", installV2.Status);
        Assert.NotNull(installV2.Plugin);
        Assert.Equal(new Version(2, 1, 0), installV2.Plugin!.PluginVersion);
    }

    [Fact]
    public async Task ZipInstall_PersistsLastError_ForIncompatibleManifest()
    {
        using var rsa = RSA.Create(2048);
        await using var fixture = await CreateFixtureAsync();
        var sut = CreateSut(new MutableCatalogProvider(), new StaticPackageDownloader(), fixture.Store);

        var incompatible = CreateSignedPackage(
            moduleId: "clubgear.zip.incompatible",
            displayName: "Incompatible ZIP",
            pluginVersion: new Version(3, 0, 0),
            rsa,
            requiredCoreVersion: ">=2.0.0");

        var result = await sut.InstallOrUpgradeFromZipAsync(
            "members-3.0.0.zip",
            incompatible.PackageBytes,
            incompatible.ExpectedSha256Hex,
            incompatible.SignatureBase64,
            incompatible.PublicKeyPem);

        Assert.False(result.Success);
        Assert.Equal("incompatible", result.Status);

        var stored = fixture.Store.GetByKey("clubgear.zip.incompatible");
        Assert.NotNull(stored);
        Assert.NotNull(stored!.LastError);
        Assert.Contains("Incompatible major version", stored.LastError, StringComparison.Ordinal);
        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task ZipInstall_PersistsCategory_FromManifest_OnSuccess()
    {
        using var rsa = RSA.Create(2048);
        await using var fixture = await CreateFixtureAsync();
        var sut = CreateSut(new MutableCatalogProvider(), new StaticPackageDownloader(), fixture.Store);

        var package = CreateSignedPackage(
            moduleId: "clubgear.zip.categorised",
            displayName: "Categorised Plugin",
            pluginVersion: new Version(1, 0, 0),
            rsa,
            category: "member-profile");

        var result = await sut.InstallOrUpgradeFromZipAsync(
            "categorised-1.0.0.zip",
            package.PackageBytes,
            package.ExpectedSha256Hex,
            package.SignatureBase64,
            package.PublicKeyPem);

        Assert.True(result.Success);
        Assert.Equal("installed", result.Status);

        var stored = fixture.Store.GetByKey("clubgear.zip.categorised");
        Assert.NotNull(stored);
        Assert.Equal("member-profile", stored!.Category);
    }

    [Fact]
    public async Task ZipInstall_PersistsCategory_FromManifest_OnFailure()
    {
        using var rsa = RSA.Create(2048);
        await using var fixture = await CreateFixtureAsync();
        var sut = CreateSut(new MutableCatalogProvider(), new StaticPackageDownloader(), fixture.Store);

        var package = CreateSignedPackage(
            moduleId: "clubgear.zip.incompatible-cat",
            displayName: "Incompatible Categorised Plugin",
            pluginVersion: new Version(9, 0, 0),
            rsa,
            requiredCoreVersion: ">=2.0.0",
            category: "vehicle-data");

        var result = await sut.InstallOrUpgradeFromZipAsync(
            "incompatible-cat-9.0.0.zip",
            package.PackageBytes,
            package.ExpectedSha256Hex,
            package.SignatureBase64,
            package.PublicKeyPem);

        Assert.False(result.Success);
        Assert.Equal("incompatible", result.Status);

        var stored = fixture.Store.GetByKey("clubgear.zip.incompatible-cat");
        Assert.NotNull(stored);
        Assert.Equal("vehicle-data", stored!.Category);
    }

    private static PluginInstallerService CreateSut(IPluginCatalogProvider provider, IPluginPackageDownloader downloader, IPluginStatusStore statusStore)
        => new(
            [provider],
            downloader,
            new PluginIntegrityVerifier(),
            new ContractCompatibilityService(),
            new PluginManifestParser(),
            new FileSystemPluginPackageStore(Path.Combine(Path.GetTempPath(), "clubgear-plugin-store-tests", Guid.NewGuid().ToString("N"))),
            statusStore,
            NullLogger<PluginInstallerService>.Instance);

    private static SignedPackage CreateSignedPackage(
        string moduleId,
        string displayName,
        Version pluginVersion,
        RSA rsa,
        bool useLegacyManifest = false,
        string requiredCoreVersion = ">=1.0.0",
        string? category = null)
    {
        var categoryLine = category is not null ? $$$"""
  "category": "{{{category}}}",
""" : "";
        var manifest = useLegacyManifest
            ?
            $$"""
            {
              "moduleId": "{{moduleId}}",
              "displayName": "{{displayName}}",
              "pluginVersion": "{{pluginVersion}}",
              "requiredContractVersion": "1.0.0",
              "entryPointType": "{{moduleId}}.PluginModule"
            }
            """
            :
            $$"""
            {
              "key": "{{moduleId}}",
              "name": "{{displayName}}",
              "version": "{{pluginVersion}}",
              "author": "Plugin Author",
              "license": "Proprietary",
              "entryPoint": "{{moduleId}}.PluginModule",
              "requiredCoreVersion": "{{requiredCoreVersion}}",
            {{categoryLine}}  "permissions": ["Plugin_Test_View"],
              "extensionPoints": ["member.detail"]
            }
            """;

        var packageBytes = CreateZipWithManifest(manifest, useLegacyManifest ? "plugin-manifest.json" : "plugin.json");
        var hash = SHA256.HashData(packageBytes);
        var signature = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var publicKeyPem = rsa.ExportRSAPublicKeyPem();

        return new SignedPackage(
            packageBytes,
            Convert.ToHexString(hash),
            Convert.ToBase64String(signature),
            publicKeyPem);
    }

    private static byte[] CreateZipWithManifest(string manifestJson, string entryName)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(entryName);
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, Encoding.UTF8);
            writer.Write(manifestJson);
        }

        return stream.ToArray();
    }

    private sealed class MutableCatalogProvider : IPluginCatalogProvider
    {
        public IReadOnlyList<PluginCatalogDescriptor> Descriptors { get; set; } = Array.Empty<PluginCatalogDescriptor>();

        public Task<IReadOnlyList<PluginCatalogDescriptor>> GetAvailableAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Descriptors);
    }

    private sealed class StaticPackageDownloader : IPluginPackageDownloader
    {
        public Dictionary<string, byte[]> Packages { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<byte[]> DownloadAsync(string location, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Packages.TryGetValue(location, out var bytes)
                ? Task.FromResult(bytes)
                : throw new InvalidOperationException($"Package '{location}' not configured in test.");
        }
    }

    private sealed record SignedPackage(
        byte[] PackageBytes,
        string ExpectedSha256Hex,
        string SignatureBase64,
        string PublicKeyPem);

    private static async Task<TestFixture> CreateFixtureAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new ApplicationDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new TestFixture(connection, dbContext, new DbPluginStatusStore(dbContext));
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        public TestFixture(SqliteConnection connection, ApplicationDbContext dbContext, DbPluginStatusStore store)
        {
            Connection = connection;
            DbContext = dbContext;
            Store = store;
        }

        public SqliteConnection Connection { get; }

        public ApplicationDbContext DbContext { get; }

        public DbPluginStatusStore Store { get; }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
