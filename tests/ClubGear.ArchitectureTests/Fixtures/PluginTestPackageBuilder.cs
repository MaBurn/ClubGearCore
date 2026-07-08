using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests.Fixtures;

internal static class PluginTestPackageBuilder
{
    public static async Task<PluginStatusRecord> CreateStoredPluginRecordAsync(
        IPluginPackageStore packageStore,
        string moduleId,
        string displayName,
        string entryPoint,
        bool isActive = true,
        CancellationToken cancellationToken = default)
    {
        var packageBytes = CreatePluginPackage(moduleId, displayName, entryPoint);
        var packageHash = Convert.ToHexString(SHA256.HashData(packageBytes));
        var packagePath = await packageStore.SaveAsync(moduleId, packageHash, packageBytes, cancellationToken);
        var now = DateTime.UtcNow;

        return new PluginStatusRecord
        {
            Key = moduleId,
            DisplayName = displayName,
            Version = "1.0.0",
            Author = "Plugin Tests",
            License = "Proprietary",
            EntryPoint = entryPoint,
            RequiredCoreVersion = ">=1.0.0",
            InstallSource = "test",
            PackageHash = packageHash,
            PackagePath = packagePath,
            IsActive = isActive,
            LastError = null,
            PermissionsJson = "[\"members.read\"]",
            ExtensionPointsJson = "[\"member.detail\"]",
            InstalledAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    private static byte[] CreatePluginPackage(string moduleId, string displayName, string entryPoint)
    {
        var manifest = $$"""
        {
          "key": "{{moduleId}}",
          "name": "{{displayName}}",
          "version": "1.0.0",
          "author": "Plugin Tests",
          "license": "Proprietary",
          "entryPoint": "{{entryPoint}}",
          "requiredCoreVersion": ">=1.0.0",
          "permissions": ["members.read"],
          "extensionPoints": ["member.detail"]
        }
        """;

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteTextEntry(archive, "plugin.json", manifest);
            AddFileEntry(archive, typeof(Plugins.RuntimeLoadedPluginModuleA).Assembly.Location);
            AddFileEntry(archive, typeof(FactAttribute).Assembly.Location);
        }

        return stream.ToArray();
    }

    private static void WriteTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, Encoding.UTF8);
        writer.Write(content);
    }

    private static void AddFileEntry(ZipArchive archive, string sourcePath)
    {
        var entry = archive.CreateEntry(Path.GetFileName(sourcePath));
        using var entryStream = entry.Open();
        using var sourceStream = File.OpenRead(sourcePath);
        sourceStream.CopyTo(entryStream);
    }
}