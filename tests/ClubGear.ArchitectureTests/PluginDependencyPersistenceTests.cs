using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClubGear.Data;
using ClubGear.Data.Migrations;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins;
using ClubGear.Services.Plugins.Installation;
using ClubGear.Services.Plugins.Manifest;
using ClubGear.Services.Plugins.Security;
using ClubGear.Services.Plugins.Status;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginDependencyPersistenceTests
{
    // ── 3.2 parser DoD ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_WithValidDependency_ProducesManifestWithOneDependencyEntry()
    {
        var sut = new PluginManifestParser();
        var json =
            """
            {
                "key": "clubgear.test.plugin",
                "name": "Test Plugin",
                "version": "1.0.0",
                "author": "ClubGear",
                "license": "Proprietary",
                "entryPoint": "Test.PluginModule",
                "requiredCoreVersion": ">=1.0.0",
                "permissions": [],
                "extensionPoints": [],
                "dependencies": ["clubgear.plugin.carinfo@>=1.0.5"]
            }
            """;

        var result = sut.Parse(json);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Manifest);
        Assert.Single(result.Manifest!.Dependencies);
        Assert.Equal("clubgear.plugin.carinfo", result.Manifest.Dependencies[0].ModuleId);
        Assert.Equal(new Version(1, 0, 5), result.Manifest.Dependencies[0].MinVersion);
    }

    [Fact]
    public void Parse_WithMalformedDependency_AddsErrorToErrorsList()
    {
        var sut = new PluginManifestParser();
        var json =
            """
            {
                "key": "clubgear.test.plugin",
                "name": "Test Plugin",
                "version": "1.0.0",
                "author": "ClubGear",
                "license": "Proprietary",
                "entryPoint": "Test.PluginModule",
                "requiredCoreVersion": ">=1.0.0",
                "permissions": [],
                "extensionPoints": [],
                "dependencies": ["bad-format"]
            }
            """;

        var result = sut.Parse(json);

        Assert.False(result.IsValid);
        Assert.Null(result.Manifest);
        Assert.Contains(result.Errors, e => e.Contains("bad-format"));
    }

    [Fact]
    public void Parse_WithNoDependenciesKey_ProducesEmptyDependenciesListWithNoError()
    {
        var sut = new PluginManifestParser();
        var json =
            """
            {
                "key": "clubgear.test.plugin",
                "name": "Test Plugin",
                "version": "1.0.0",
                "author": "ClubGear",
                "license": "Proprietary",
                "entryPoint": "Test.PluginModule",
                "requiredCoreVersion": ">=1.0.0",
                "permissions": [],
                "extensionPoints": []
            }
            """;

        var result = sut.Parse(json);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Manifest);
        Assert.Empty(result.Manifest!.Dependencies);
        Assert.Empty(result.Errors);
    }

    // ── 3.4 migration DoD ───────────────────────────────────────────────────

    [Fact]
    public async Task AddPluginDependencies_ApplyAsync_IsIdempotent_WhenColumnAbsentThenPresent()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        // Create the table WITHOUT DependenciesJson directly on the raw connection
        // before handing the connection to EF Core, so both the migration's
        // PRAGMA check and ExecuteSqlRawAsync see the same in-memory database.
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                @"CREATE TABLE PluginStatusRecords (
                    Id INTEGER NOT NULL CONSTRAINT PK_PluginStatusRecords PRIMARY KEY AUTOINCREMENT,
                    Key TEXT NOT NULL,
                    DisplayName TEXT NOT NULL,
                    Version TEXT NOT NULL,
                    Author TEXT NOT NULL,
                    License TEXT NOT NULL,
                    Category TEXT NOT NULL DEFAULT 'General',
                    EntryPoint TEXT NOT NULL,
                    RequiredCoreVersion TEXT NOT NULL,
                    InstallSource TEXT NOT NULL,
                    PackageHash TEXT NOT NULL,
                    PackagePath TEXT NOT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 0,
                    LastError TEXT NULL,
                    PermissionsJson TEXT NOT NULL DEFAULT '[]',
                    ExtensionPointsJson TEXT NOT NULL DEFAULT '[]',
                    InstalledAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );";
            await cmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplicationDbContext(options);

        // First call: column is absent, migration adds it.
        await AddPluginDependencies_202606300101.ApplyAsync(dbContext);

        // Second call: column already present, migration skips without error (idempotent).
        await AddPluginDependencies_202606300101.ApplyAsync(dbContext);
    }

    // ── 3.6 installer DoD ───────────────────────────────────────────────────

    [Fact]
    public async Task ZipInstall_PersistsDependenciesJson_WhenManifestContainsDependencies()
    {
        using var rsa = RSA.Create(2048);
        await using var fixture = await CreateFixtureAsync();
        var sut = CreateSut(fixture.Store);

        var package = CreateSignedPackageWithDependencies(
            moduleId: "clubgear.dep.test",
            displayName: "Dep Test Plugin",
            pluginVersion: new Version(1, 0, 0),
            dependencies: ["clubgear.plugin.carinfo@>=1.0.5"],
            rsa,
            requiredCoreVersion: ">=1.8.0");

        var result = await sut.InstallOrUpgradeFromZipAsync(
            "dep-test-1.0.0.zip",
            package.PackageBytes,
            package.ExpectedSha256Hex,
            package.SignatureBase64,
            package.PublicKeyPem);

        Assert.True(result.Success, $"Install failed with status '{result.Status}': {result.Message}");

        var stored = fixture.Store.GetByKey("clubgear.dep.test");
        Assert.NotNull(stored);

        var deserialized = JsonSerializer.Deserialize<string[]>(stored!.DependenciesJson);
        Assert.NotNull(deserialized);
        Assert.Single(deserialized!);
        Assert.Equal("clubgear.plugin.carinfo@>=1.0.5", deserialized[0]);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static PluginInstallerService CreateSut(IPluginStatusStore statusStore)
        => new(
            [],
            new NullPackageDownloader(),
            new PluginIntegrityVerifier(),
            new ContractCompatibilityService(),
            new PluginManifestParser(),
            new FileSystemPluginPackageStore(Path.Combine(Path.GetTempPath(), "clubgear-dep-persistence-tests", Guid.NewGuid().ToString("N"))),
            statusStore,
            NullLogger<PluginInstallerService>.Instance);

    private static SignedPackage CreateSignedPackageWithDependencies(
        string moduleId,
        string displayName,
        Version pluginVersion,
        string[] dependencies,
        RSA rsa,
        string requiredCoreVersion = ">=1.8.0")
    {
        var depsJson = JsonSerializer.Serialize(dependencies);
        var manifest =
            $$"""
            {
              "key": "{{moduleId}}",
              "name": "{{displayName}}",
              "version": "{{pluginVersion}}",
              "author": "Plugin Author",
              "license": "Proprietary",
              "entryPoint": "{{moduleId}}.PluginModule",
              "requiredCoreVersion": "{{requiredCoreVersion}}",
              "permissions": [],
              "extensionPoints": [],
              "dependencies": {{depsJson}}
            }
            """;

        var packageBytes = CreateZipWithManifest(manifest);
        var hash = SHA256.HashData(packageBytes);
        var signature = rsa.SignHash(hash, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var publicKeyPem = rsa.ExportRSAPublicKeyPem();

        return new SignedPackage(
            packageBytes,
            Convert.ToHexString(hash),
            Convert.ToBase64String(signature),
            publicKeyPem);
    }

    private static byte[] CreateZipWithManifest(string manifestJson)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("plugin.json");
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, Encoding.UTF8);
            writer.Write(manifestJson);
        }

        return stream.ToArray();
    }

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

    private sealed class NullPackageDownloader : IPluginPackageDownloader
    {
        public Task<byte[]> DownloadAsync(string location, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Download not used in this test.");
    }

    private sealed record SignedPackage(
        byte[] PackageBytes,
        string ExpectedSha256Hex,
        string SignatureBase64,
        string PublicKeyPem);

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
