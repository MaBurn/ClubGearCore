using System.Text.Json;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Models.PluginAdmin;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Plugins.Admin;

public sealed class PluginAdminQueryService : IPluginAdminQueryService
{
    private readonly IPluginStatusStore _pluginStatusStore;
    private readonly IPluginRegistryReader _pluginRegistryReader;
    private readonly IContractCompatibilityService _contractCompatibilityService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IPluginBackgroundJobRunner _jobRunner;

    public PluginAdminQueryService(
        IPluginStatusStore pluginStatusStore,
        IPluginRegistryReader pluginRegistryReader,
        IContractCompatibilityService contractCompatibilityService,
        ApplicationDbContext dbContext,
        IPluginBackgroundJobRunner jobRunner)
    {
        _pluginStatusStore = pluginStatusStore;
        _pluginRegistryReader = pluginRegistryReader;
        _contractCompatibilityService = contractCompatibilityService;
        _dbContext = dbContext;
        _jobRunner = jobRunner;
    }

    public IReadOnlyList<PluginAdminStatusViewModel> GetPluginStatuses()
    {
        var installedByKey = _pluginStatusStore.List()
            .ToDictionary(record => record.Key, StringComparer.OrdinalIgnoreCase);
        var runtimeByKey = _pluginRegistryReader.GetRegisteredPlugins()
            .ToDictionary(runtime => runtime.ModuleId, StringComparer.OrdinalIgnoreCase);

        var allKeys = installedByKey.Keys
            .Union(runtimeByKey.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return allKeys
            .Select(key =>
            {
                var migrationCount = _dbContext.PluginMigrationStates.Count(s => s.PluginKey == key);
                return CreateStatus(key, installedByKey.GetValueOrDefault(key), runtimeByKey.GetValueOrDefault(key), migrationCount);
            })
            .ToArray();
    }

    public PluginAdminStatusViewModel? GetPluginStatus(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var installed = _pluginStatusStore.GetByKey(moduleId);
        var runtime = _pluginRegistryReader.GetByModuleId(moduleId);
        if (installed is null && runtime is null)
        {
            return null;
        }

        var migrationCount = _dbContext.PluginMigrationStates.Count(s => s.PluginKey == moduleId);
        return CreateStatus(moduleId, installed, runtime, migrationCount);
    }

    private PluginAdminStatusViewModel CreateStatus(
        string moduleId,
        PluginStatusRecord? installed,
        RegisteredPluginRuntime? runtime,
        int appliedMigrationCount = 0)
    {
        var runtimeModule = _pluginRegistryReader.GetModule(moduleId);
        var displayName = installed?.DisplayName
            ?? runtime?.DisplayName
            ?? runtimeModule?.Manifest.DisplayName
            ?? moduleId;
        var pluginVersion = ParseVersion(installed?.Version)
            ?? runtime?.PluginVersion
            ?? runtimeModule?.Manifest.PluginVersion
            ?? new Version(0, 0);
        var source = installed?.InstallSource ?? "runtime";
        DateTimeOffset? installedAtUtc = installed is null
            ? null
            : DateTime.SpecifyKind(installed.InstalledAtUtc, DateTimeKind.Utc);
        var author = installed?.Author
            ?? runtimeModule?.Manifest.Author
            ?? "Unknown";
        var license = installed?.License
            ?? runtimeModule?.Manifest.License
            ?? "Unspecified";
        var requiredCoreVersion = installed?.RequiredCoreVersion
            ?? runtimeModule?.Manifest.RequiredCoreVersion
            ?? ContractVersion.Current.ToString();
        var permissions = installed is not null
            ? DeserializeArray(installed.PermissionsJson)
            : runtimeModule?.Manifest.Permissions.ToArray() ?? Array.Empty<string>();
        var extensionPoints = installed is not null
            ? DeserializeArray(installed.ExtensionPointsJson)
            : runtimeModule?.Manifest.ExtensionPoints.ToArray() ?? Array.Empty<string>();
        var compatibility = _contractCompatibilityService.Validate(
            runtimeModule?.Manifest.RequiredContractVersion ?? ParseMinimumRequiredVersion(requiredCoreVersion));
        var category = installed?.Category ?? runtimeModule?.Manifest.Category ?? "General";
        var packageHash = installed?.PackageHash ?? string.Empty;

        return new PluginAdminStatusViewModel(
            moduleId,
            displayName,
            pluginVersion,
            source,
            installedAtUtc,
            author,
            license,
            requiredCoreVersion,
            installed is not null,
            installed?.IsActive ?? runtime is not null,
            runtime is not null,
            compatibility.IsCompatible,
            compatibility.Reason,
            installed?.LastError,
            permissions,
            extensionPoints,
            runtime?.Routes.Count ?? 0,
            runtime?.Services.Count ?? 0,
            runtime?.MemberProviders.Count ?? 0,
            runtime?.BackgroundJobs.Count ?? 0,
            runtime?.LoadContextName,
            category,
            packageHash,
            appliedMigrationCount,
            runtime?.NavEntries.Count ?? 0,
            runtime?.AuditSinks.Count ?? 0,
            _jobRunner.GetJobStatuses(moduleId).Count(s => s.State == PluginJobRunState.Running));
    }

    private static Version? ParseVersion(string? value)
        => Version.TryParse(value, out var parsed) ? parsed : null;

    private static Version ParseMinimumRequiredVersion(string value)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith(">=", StringComparison.Ordinal))
        {
            normalized = normalized[2..].Trim();
        }

        return Version.TryParse(normalized, out var parsed)
            ? parsed
            : ContractVersion.Current;
    }

    private static IReadOnlyList<string> DeserializeArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
    }
}