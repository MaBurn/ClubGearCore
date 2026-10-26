using System.IO.Compression;
using System.Text.RegularExpressions;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Plugins.Installation;

public sealed class FileSystemPluginPackageStore : IPluginPackageStore
{
    private static readonly Regex SafePluginKeyPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex Sha256Pattern = new(
        "^[A-Fa-f0-9]{64}$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

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

        ValidateStorageIdentifiers(pluginKey, packageHash);
        var packageDirectory = GetContainedPath("packages", pluginKey, packageHash);
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

        ValidateStorageIdentifiers(pluginKey, packageHash);
        var expectedPackagePath = GetContainedPath("packages", pluginKey, packageHash, "package.zip");
        if (!string.Equals(Path.GetFullPath(packagePath), expectedPackagePath, PathComparison))
        {
            throw new InvalidDataException("Plugin-Paketpfad liegt ausserhalb des erwarteten Speicherorts.");
        }

        if (!File.Exists(expectedPackagePath))
        {
            throw new FileNotFoundException("Plugin-Paket wurde nicht gefunden.", expectedPackagePath);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var extractionDirectory = GetContainedPath("extracted", pluginKey, packageHash);
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

        using var packageStream = File.OpenRead(expectedPackagePath);
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read, leaveOpen: false);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationPath = Path.GetFullPath(Path.Combine(extractionDirectory, entry.FullName));
            var extractionRoot = Path.GetFullPath(extractionDirectory) + Path.DirectorySeparatorChar;
            if (!destinationPath.StartsWith(extractionRoot, PathComparison))
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

        ValidatePluginKey(pluginKey);
        var packagesPath = GetContainedPath("packages", pluginKey);
        if (Directory.Exists(packagesPath))
        {
            Directory.Delete(packagesPath, recursive: true);
        }

        var extractedPath = GetContainedPath("extracted", pluginKey);
        if (Directory.Exists(extractedPath))
        {
            Directory.Delete(extractedPath, recursive: true);
        }

        return Task.CompletedTask;
    }

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private string GetContainedPath(params string[] segments)
    {
        var root = Path.GetFullPath(_rootPath);
        var path = Path.GetFullPath(Path.Combine(new[] { root }.Concat(segments).ToArray()));
        var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!path.StartsWith(rootPrefix, PathComparison))
        {
            throw new InvalidDataException("Plugin-Speicherpfad liegt ausserhalb des konfigurierten Stammverzeichnisses.");
        }

        return path;
    }

    private static void ValidateStorageIdentifiers(string pluginKey, string packageHash)
    {
        ValidatePluginKey(pluginKey);
        if (!Sha256Pattern.IsMatch(packageHash))
        {
            throw new ArgumentException("Paket-Hash muss ein SHA-256-Wert in Hexadezimaldarstellung sein.", nameof(packageHash));
        }
    }

    private static void ValidatePluginKey(string pluginKey)
    {
        if (!SafePluginKeyPattern.IsMatch(pluginKey))
        {
            throw new ArgumentException("Plugin-Schluessel enthaelt unzulaessige Zeichen.", nameof(pluginKey));
        }
    }
}
