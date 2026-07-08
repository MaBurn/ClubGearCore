namespace ClubGear.Services.Abstractions;

public interface IPluginUninstallService
{
    Task<PluginUninstallResult> UninstallAsync(string moduleId, bool removeData, CancellationToken ct = default);
}

public sealed record PluginUninstallResult(bool Success, string Status, string Message);
