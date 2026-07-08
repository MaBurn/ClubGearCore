namespace ClubGear.Services.Abstractions;

public interface IPluginInstallerService
{
    Task<PluginInstallOperationResult> InstallOrUpgradeFromMarketplaceAsync(
        string moduleId,
        CancellationToken cancellationToken = default);

    Task<PluginInstallOperationResult> InstallOrUpgradeFromZipAsync(
        string fileName,
        byte[] zipBytes,
        string expectedSha256Hex,
        string signatureBase64,
        string signerPublicKeyPem,
        CancellationToken cancellationToken = default);

    IReadOnlyList<InstalledPluginRecord> GetInstalledPlugins();
}

public sealed record InstalledPluginRecord(
    string ModuleId,
    string DisplayName,
    Version PluginVersion,
    string Source,
    DateTimeOffset InstalledAtUtc,
    string Author,
    string License,
    string RequiredCoreVersion,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> ExtensionPoints,
    bool IsActive,
    string? LastError = null,
    string? PackageHash = null);

public sealed record PluginInstallOperationResult(
    bool Success,
    string Status,
    string Message,
    InstalledPluginRecord? Plugin = null);
