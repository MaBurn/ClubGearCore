using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClubGear.Plugin.Contracts;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Catalog;
using ClubGear.Services.Plugins.Manifest;
using ClubGear.Services.Plugins.Security;

namespace ClubGear.Services.Plugins.Installation;

public interface IPluginPackageDownloader
{
    Task<byte[]> DownloadAsync(string location, CancellationToken cancellationToken = default);
}

public sealed class HttpPluginPackageDownloader : IPluginPackageDownloader
{
    private readonly HttpClient _httpClient;

    public HttpPluginPackageDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<byte[]> DownloadAsync(string location, CancellationToken cancellationToken = default)
        => _httpClient.GetByteArrayAsync(location, cancellationToken);
}

public sealed class PluginInstallerService : IPluginInstallerService
{
    private readonly IReadOnlyList<IPluginCatalogProvider> _catalogProviders;
    private readonly IPluginPackageDownloader _packageDownloader;
    private readonly IPluginIntegrityVerifier _integrityVerifier;
    private readonly IContractCompatibilityService _contractCompatibilityService;
    private readonly PluginManifestParser _manifestParser;
    private readonly IPluginPackageStore _packageStore;
    private readonly IPluginStatusStore _statusStore;
    private readonly ILogger<PluginInstallerService> _logger;

    public PluginInstallerService(
        IEnumerable<IPluginCatalogProvider> catalogProviders,
        IPluginPackageDownloader packageDownloader,
        IPluginIntegrityVerifier integrityVerifier,
        IContractCompatibilityService contractCompatibilityService,
        PluginManifestParser manifestParser,
        IPluginPackageStore packageStore,
        IPluginStatusStore statusStore,
        ILogger<PluginInstallerService> logger)
    {
        _catalogProviders = catalogProviders.ToList();
        _packageDownloader = packageDownloader;
        _integrityVerifier = integrityVerifier;
        _contractCompatibilityService = contractCompatibilityService;
        _manifestParser = manifestParser;
        _packageStore = packageStore;
        _statusStore = statusStore;
        _logger = logger;
    }

    public async Task<PluginInstallOperationResult> InstallOrUpgradeFromMarketplaceAsync(
        string moduleId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return new PluginInstallOperationResult(false, "invalid", "moduleId ist erforderlich.");
        }

        var descriptors = new List<PluginCatalogDescriptor>();
        foreach (var provider in _catalogProviders)
        {
            var available = await provider.GetAvailableAsync(cancellationToken);
            descriptors.AddRange(available.Where(d => string.Equals(d.Source, "marketplace", StringComparison.OrdinalIgnoreCase)));
        }

        var descriptor = descriptors
            .Where(d => string.Equals(d.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.PluginVersion)
            .FirstOrDefault();

        if (descriptor is null)
        {
            return new PluginInstallOperationResult(false, "not-found", $"Plugin '{moduleId}' wurde im Marketplace nicht gefunden.");
        }

        if (string.IsNullOrWhiteSpace(descriptor.ExpectedSha256Hex)
            || string.IsNullOrWhiteSpace(descriptor.SignatureBase64)
            || string.IsNullOrWhiteSpace(descriptor.SignerPublicKeyPem))
        {
            return new PluginInstallOperationResult(false, "invalid", "Marketplace-Eintrag enthaelt keine vollstaendigen Signaturdaten.");
        }

        var packageBytes = await _packageDownloader.DownloadAsync(descriptor.Location, cancellationToken);
        if (!TryBuildVerificationRequest(
                packageBytes,
                descriptor.ExpectedSha256Hex,
                descriptor.SignatureBase64,
                descriptor.SignerPublicKeyPem,
                out var verificationRequest,
                out var verificationError))
        {
            return new PluginInstallOperationResult(false, "invalid", verificationError!);
        }

        var verification = _integrityVerifier.Verify(verificationRequest!);
        if (!verification.IsValid)
        {
            return new PluginInstallOperationResult(false, "integrity-failed", verification.Reason ?? "Integritaetspruefung fehlgeschlagen.");
        }

        var manifestResult = ExtractManifest(packageBytes);
        if (!manifestResult.IsValid || manifestResult.Manifest is null)
        {
            var reason = manifestResult.Errors.Count > 0
                ? string.Join("; ", manifestResult.Errors)
                : "Manifest im Paket ist ungueltig.";
            return new PluginInstallOperationResult(false, "manifest-invalid", reason);
        }

        return await SaveInstallAsync(manifestResult.Manifest, descriptor.Source, packageBytes, cancellationToken);
    }

    public async Task<PluginInstallOperationResult> InstallOrUpgradeFromZipAsync(
        string fileName,
        byte[] zipBytes,
        string expectedSha256Hex,
        string signatureBase64,
        string signerPublicKeyPem,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask;

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new PluginInstallOperationResult(false, "invalid", "Dateiname fuer ZIP-Upload ist erforderlich.");
        }

        if (zipBytes.Length == 0)
        {
            return new PluginInstallOperationResult(false, "invalid", "ZIP-Paket ist leer.");
        }

        if (!TryBuildVerificationRequest(zipBytes, expectedSha256Hex, signatureBase64, signerPublicKeyPem, out var request, out var error))
        {
            return new PluginInstallOperationResult(false, "invalid", error!);
        }

        var verification = _integrityVerifier.Verify(request!);
        if (!verification.IsValid)
        {
            return new PluginInstallOperationResult(false, "integrity-failed", verification.Reason ?? "Integritaetspruefung fehlgeschlagen.");
        }

        var manifestResult = ExtractManifest(zipBytes);
        if (!manifestResult.IsValid || manifestResult.Manifest is null)
        {
            var reason = manifestResult.Errors.Count > 0
                ? string.Join("; ", manifestResult.Errors)
                : $"Manifest in '{fileName}' ist ungueltig.";
            return new PluginInstallOperationResult(false, "manifest-invalid", reason);
        }

        return await SaveInstallAsync(manifestResult.Manifest, "zip", zipBytes, cancellationToken);
    }

    public IReadOnlyList<InstalledPluginRecord> GetInstalledPlugins()
        => _statusStore.List()
            .Select(MapInstalledRecord)
            .OrderBy(p => p.ModuleId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private async Task<PluginInstallOperationResult> SaveInstallAsync(
        PluginManifest manifest,
        string source,
        byte[] packageBytes,
        CancellationToken cancellationToken)
    {
        var existing = _statusStore.GetByKey(manifest.Key);
        var packageHash = Convert.ToHexString(SHA256.HashData(packageBytes));
        var packagePath = await _packageStore.SaveAsync(manifest.Key, packageHash, packageBytes, cancellationToken);
        var compatibility = _contractCompatibilityService.Validate(manifest.RequiredContractVersion);
        if (!compatibility.IsCompatible)
        {
            var failed = await PersistFailureAsync(
                existing,
                manifest,
                source,
                packageHash,
                packagePath,
                compatibility.Reason ?? "Plugin-Vertrag ist nicht kompatibel.",
                cancellationToken);
            return new PluginInstallOperationResult(false, "incompatible", failed.LastError ?? "Plugin-Vertrag ist nicht kompatibel.", MapInstalledRecord(failed));
        }

        var existingVersion = ParseVersion(existing?.Version);
        if (existingVersion is not null && manifest.PluginVersion < existingVersion)
        {
            var failed = await PersistFailureAsync(
                existing,
                manifest,
                source,
                packageHash,
                packagePath,
                $"Downgrade ist nicht erlaubt. Installiert ist {existingVersion}, angefordert wurde {manifest.PluginVersion}.",
                cancellationToken);
            return new PluginInstallOperationResult(false, "downgrade-blocked", failed.LastError!, MapInstalledRecord(failed));
        }

        var stored = await PersistSuccessAsync(existing, manifest, source, packageHash, packagePath, cancellationToken);

        if (existingVersion is not null && manifest.PluginVersion == existingVersion)
        {
            return new PluginInstallOperationResult(true, "already-installed", "Plugin ist bereits in derselben Version installiert.", MapInstalledRecord(stored));
        }

        if (existingVersion is null)
        {
            _logger.LogInformation("Plugin {ModuleId} in Version {Version} installiert ({Source}).", stored.Key, stored.Version, stored.InstallSource);
            return new PluginInstallOperationResult(true, "installed", "Plugin wurde erfolgreich installiert.", MapInstalledRecord(stored));
        }

        _logger.LogInformation(
            "Plugin {ModuleId} von {OldVersion} auf {NewVersion} aktualisiert ({Source}).",
            stored.Key,
            existingVersion,
            manifest.PluginVersion,
            source);
        return new PluginInstallOperationResult(true, "upgraded", "Plugin wurde erfolgreich aktualisiert.", MapInstalledRecord(stored));
    }

    private PluginManifestValidationResult ExtractManifest(byte[] packageBytes)
    {
        try
        {
            using var stream = new MemoryStream(packageBytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var manifestEntry = archive.GetEntry("plugin.json")
                ?? archive.GetEntry("plugin-manifest.json");
            if (manifestEntry is null)
            {
                return PluginManifestValidationResult.Failure("plugin.json fehlt im Paket.");
            }

            using var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8);
            var manifestJson = reader.ReadToEnd();
            return _manifestParser.Parse(manifestJson);
        }
        catch (InvalidDataException ex)
        {
            return PluginManifestValidationResult.Failure($"Ungueltiges ZIP-Paket: {ex.Message}");
        }
    }

    private static bool TryBuildVerificationRequest(
        byte[] packageBytes,
        string expectedSha256Hex,
        string signatureBase64,
        string signerPublicKeyPem,
        out PluginIntegrityVerificationRequest? request,
        out string? error)
    {
        request = null;
        error = null;

        if (string.IsNullOrWhiteSpace(expectedSha256Hex)
            || string.IsNullOrWhiteSpace(signatureBase64)
            || string.IsNullOrWhiteSpace(signerPublicKeyPem))
        {
            error = "Signaturdaten sind unvollstaendig (Hash/Signatur/PublicKey).";
            return false;
        }

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(expectedSha256Hex.Trim());
        }
        catch (FormatException)
        {
            error = "expectedSha256Hex ist kein gueltiger Hex-String.";
            return false;
        }

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(signatureBase64.Trim());
        }
        catch (FormatException)
        {
            error = "signatureBase64 ist kein gueltiger Base64-String.";
            return false;
        }

        request = new PluginIntegrityVerificationRequest(packageBytes, expectedHash, signature, signerPublicKeyPem);
        return true;
    }

    private async Task<PluginStatusRecord> PersistSuccessAsync(
        PluginStatusRecord? existing,
        PluginManifest manifest,
        string source,
        string packageHash,
        string packagePath,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var record = new PluginStatusRecord
        {
            Key = manifest.Key,
            DisplayName = manifest.Name,
            Version = manifest.Version.ToString(),
            Author = manifest.Author,
            License = manifest.License,
            EntryPoint = manifest.EntryPoint,
            RequiredCoreVersion = manifest.RequiredCoreVersion,
            InstallSource = source,
            PackageHash = packageHash,
            PackagePath = packagePath,
            IsActive = existing?.IsActive ?? false,
            LastError = null,
            Category = manifest.Category,
            PermissionsJson = JsonSerializer.Serialize(manifest.Permissions),
            ExtensionPointsJson = JsonSerializer.Serialize(manifest.ExtensionPoints),
            DependenciesJson = JsonSerializer.Serialize(manifest.Dependencies.Select(d => d.ToString()).ToArray()),
            InstalledAtUtc = existing?.InstalledAtUtc ?? now,
            UpdatedAtUtc = now
        };

        return await _statusStore.UpsertAsync(record, cancellationToken);
    }

    private async Task<PluginStatusRecord> PersistFailureAsync(
        PluginStatusRecord? existing,
        PluginManifest manifest,
        string source,
        string packageHash,
        string packagePath,
        string error,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var record = existing is null
            ? new PluginStatusRecord
            {
                Key = manifest.Key,
                DisplayName = manifest.Name,
                Version = manifest.Version.ToString(),
                Author = manifest.Author,
                License = manifest.License,
                EntryPoint = manifest.EntryPoint,
                RequiredCoreVersion = manifest.RequiredCoreVersion,
                InstallSource = source,
                PackageHash = packageHash,
                PackagePath = packagePath,
                IsActive = false,
                Category = manifest.Category,
                PermissionsJson = JsonSerializer.Serialize(manifest.Permissions),
                ExtensionPointsJson = JsonSerializer.Serialize(manifest.ExtensionPoints),
                DependenciesJson = JsonSerializer.Serialize(manifest.Dependencies.Select(d => d.ToString()).ToArray()),
                InstalledAtUtc = now
            }
            : new PluginStatusRecord
            {
                Key = existing.Key,
                DisplayName = existing.DisplayName,
                Version = existing.Version,
                Author = existing.Author,
                License = existing.License,
                EntryPoint = existing.EntryPoint,
                RequiredCoreVersion = existing.RequiredCoreVersion,
                InstallSource = existing.InstallSource,
                PackageHash = existing.PackageHash,
                PackagePath = existing.PackagePath,
                IsActive = existing.IsActive,
                Category = manifest.Category,
                PermissionsJson = existing.PermissionsJson,
                ExtensionPointsJson = existing.ExtensionPointsJson,
                DependenciesJson = existing.DependenciesJson,
                InstalledAtUtc = existing.InstalledAtUtc
            };

        record.LastError = error;
        record.UpdatedAtUtc = now;
        return await _statusStore.UpsertAsync(record, cancellationToken);
    }

    private static InstalledPluginRecord MapInstalledRecord(PluginStatusRecord record)
    {
        return new InstalledPluginRecord(
            record.Key,
            record.DisplayName,
            ParseVersion(record.Version) ?? new Version(0, 0),
            record.InstallSource,
            DateTime.SpecifyKind(record.InstalledAtUtc, DateTimeKind.Utc),
            record.Author,
            record.License,
            record.RequiredCoreVersion,
            DeserializeArray(record.PermissionsJson),
            DeserializeArray(record.ExtensionPointsJson),
            record.IsActive,
            record.LastError,
            record.PackageHash);
    }

    private static Version? ParseVersion(string? value)
    {
        return Version.TryParse(value, out var parsed)
            ? parsed
            : null;
    }

    private static IReadOnlyList<string> DeserializeArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        var values = JsonSerializer.Deserialize<string[]>(json);
        return values ?? Array.Empty<string>();
    }
}
