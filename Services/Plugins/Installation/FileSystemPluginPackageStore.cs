using System.IO.Compression;
using System.Text;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Plugins.Installation;

public sealed class FileSystemPluginPackageStore : IPluginPackageStore
{
    private readonly string _rootPath;

    public FileSystemPluginPackageStore()
        : this(Path.Combine(AppContext.BaseDirectory, "plugin-store"))
    {
    }

    public FileSystemPluginPackageStore(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = rootPath;
    }

    public async Task<string> SaveAsync(
        string pluginKey,
        string packageHash,
        byte[] packageBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageHash);
        ArgumentNullException.ThrowIfNull(packageBytes);

        cancellationToken.ThrowIfCancellationRequested();

        var packageDirectory = Path.Combine(_rootPath, "packages", SanitizePathSegment(pluginKey), packageHash);
        Directory.CreateDirectory(packageDirectory);

        var packagePath = Path.Combine(packageDirectory, "package.zip");
        await File.WriteAllBytesAsync(packagePath, packageBytes, cancellationToken);
        return packagePath;
    }

    public async Task<string> EnsureExtractedAsync(
        string pluginKey,
        string packageHash,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException("Plugin-Paket wurde nicht gefunden.", packagePath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var extractionDirectory = Path.Combine(_rootPath, "extracted", SanitizePathSegment(pluginKey), packageHash);
        if (Directory.Exists(extractionDirectory)
            && Directory.EnumerateFileSystemEntries(extractionDirectory).Any())
        {
            return extractionDirectory;
        }

        if (Directory.Exists(extractionDirectory))
        {
            Directory.Delete(extractionDirectory, recursive: true);
        }

        Directory.CreateDirectory(extractionDirectory);

        using var packageStream = File.OpenRead(packagePath);
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: false);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationPath = Path.GetFullPath(Path.Combine(extractionDirectory, entry.FullName));
            var extractionRoot = Path.GetFullPath(extractionDirectory) + Path.DirectorySeparatorChar;
            if (!destinationPath.StartsWith(extractionRoot, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Plugin-Paket enthaelt einen ungueltigen Dateipfad.");
            }

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            await using var entryStream = entry.Open();
            await using var fileStream = File.Create(destinationPath);
            await entryStream.CopyToAsync(fileStream, cancellationToken);
        }

        return extractionDirectory;
    }

    public Task DeleteAsync(string pluginKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginKey);

        ct.ThrowIfCancellationRequested();

        var packagesPath = Path.Combine(_rootPath, "packages", SanitizePathSegment(pluginKey));
        if (Directory.Exists(packagesPath))
        {
            Directory.Delete(packagesPath, recursive: true);
        }

        var extractedPath = Path.Combine(_rootPath, "extracted", SanitizePathSegment(pluginKey));
        if (Directory.Exists(extractedPath))
        {
            Directory.Delete(extractedPath, recursive: true);
        }

        return Task.CompletedTask;
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            builder.Append(invalidCharacters.Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }
}