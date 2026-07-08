namespace ClubGear.Services.Abstractions;

public interface IPluginPackageStore
{
    Task<string> SaveAsync(
        string pluginKey,
        string packageHash,
        byte[] packageBytes,
        CancellationToken cancellationToken = default);

    Task<string> EnsureExtractedAsync(
        string pluginKey,
        string packageHash,
        string packagePath,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string pluginKey, CancellationToken ct = default);
}