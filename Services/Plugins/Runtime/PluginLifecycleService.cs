using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Persistence;

namespace ClubGear.Services.Plugins.Runtime;

public sealed class PluginLifecycleService : IPluginLifecycleService
{
    private readonly IPluginStatusStore _statusStore;
    private readonly IPluginRuntimeRegistry _runtimeRegistry;
    private readonly PluginEndpointRegistrar _endpointRegistrar;
    private readonly PluginLoader _pluginLoader;
    private readonly PluginMigrationRunner _pluginMigrationRunner;
    private readonly IPluginBackgroundJobRunner _jobRunner;
    private readonly ILogger<PluginLifecycleService> _logger;

    public PluginLifecycleService(
        IPluginStatusStore statusStore,
        IPluginRuntimeRegistry runtimeRegistry,
        PluginEndpointRegistrar endpointRegistrar,
        PluginLoader pluginLoader,
        PluginMigrationRunner pluginMigrationRunner,
        IPluginBackgroundJobRunner jobRunner,
        ILogger<PluginLifecycleService> logger)
    {
        _statusStore = statusStore;
        _runtimeRegistry = runtimeRegistry;
        _endpointRegistrar = endpointRegistrar;
        _pluginLoader = pluginLoader;
        _pluginMigrationRunner = pluginMigrationRunner;
        _jobRunner = jobRunner;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PluginLifecycleOperationResult>> LoadActivatedAsync(CancellationToken cancellationToken = default)
    {
        var activatedPlugins = _statusStore.List()
            .Where(record => record.IsActive)
            .ToArray();

        var results = new List<PluginLifecycleOperationResult>(activatedPlugins.Length);
        foreach (var record in activatedPlugins)
        {
            results.Add(await LoadIntoRuntimeAsync(Clone(record), cancellationToken));
        }

        return results;
    }

    public Task<PluginLifecycleOperationResult> ActivateAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var existing = _statusStore.GetByKey(moduleId);
        if (existing is null)
        {
            return Task.FromResult(new PluginLifecycleOperationResult(false, "not-found", $"Plugin '{moduleId}' ist nicht installiert."));
        }

        var dependencyFailure = CheckDependencies(existing);
        if (dependencyFailure is not null)
        {
            return Task.FromResult(dependencyFailure);
        }

        var target = Clone(existing);
        target.IsActive = true;
        target.LastError = null;
        target.UpdatedAtUtc = DateTime.UtcNow;

        return LoadIntoRuntimeAsync(target, cancellationToken);
    }

    private PluginLifecycleOperationResult? CheckDependencies(PluginStatusRecord record)
    {
        var dependencyEntries = DeserializeArray(record.DependenciesJson);
        foreach (var entry in dependencyEntries)
        {
            if (!PluginDependency.TryParse(entry, out var dependency) || dependency is null)
            {
                // Tolerate stored format drift; skip entries that no longer parse.
                continue;
            }

            var runtime = _runtimeRegistry.GetByModuleId(dependency.ModuleId);
            if (runtime is null)
            {
                return new PluginLifecycleOperationResult(
                    false,
                    "dependency-not-met",
                    $"Voraussetzung '{dependency.ModuleId}' (>= {dependency.MinVersion}) muss aktiv sein, bevor dieses Plugin aktiviert werden kann.");
            }

            if (runtime.PluginVersion < dependency.MinVersion)
            {
                return new PluginLifecycleOperationResult(
                    false,
                    "dependency-not-met",
                    $"Voraussetzung '{dependency.ModuleId}' erfordert Version >= {dependency.MinVersion}, aktiv ist {runtime.PluginVersion}.");
            }
        }

        return null;
    }

    public async Task<PluginLifecycleOperationResult> DeactivateAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var existing = _statusStore.GetByKey(moduleId);
        if (existing is null)
        {
            return new PluginLifecycleOperationResult(false, "not-found", $"Plugin '{moduleId}' ist nicht installiert.");
        }

        _endpointRegistrar.Unregister(moduleId);
        await _jobRunner.StopJobsForModuleAsync(moduleId);
        _runtimeRegistry.Unregister(moduleId);

        var target = Clone(existing);
        target.IsActive = false;
        target.LastError = null;
        target.UpdatedAtUtc = DateTime.UtcNow;

        var stored = await _statusStore.UpsertAsync(target, cancellationToken);
        _logger.LogInformation("Plugin {PluginKey} deaktiviert.", moduleId);
        return new PluginLifecycleOperationResult(true, "deactivated", "Plugin wurde deaktiviert.", MapInstalledRecord(stored));
    }

    private async Task<PluginLifecycleOperationResult> LoadIntoRuntimeAsync(PluginStatusRecord target, CancellationToken cancellationToken)
    {
        _endpointRegistrar.Unregister(target.Key);
        _runtimeRegistry.Unregister(target.Key);

        var loadResult = await _pluginLoader.LoadAsync(target, cancellationToken);
        target.UpdatedAtUtc = DateTime.UtcNow;

        if (!loadResult.Success || loadResult.LoadedPlugin is null)
        {
            target.LastError = loadResult.Error ?? $"Plugin '{target.Key}' konnte nicht geladen werden.";
            var failed = await _statusStore.UpsertAsync(target, cancellationToken);
            _logger.LogWarning("Plugin {PluginKey} konnte nicht aktiviert werden: {Error}", target.Key, failed.LastError);
            return new PluginLifecycleOperationResult(false, "load-failed", failed.LastError ?? "Plugin konnte nicht geladen werden.", MapInstalledRecord(failed));
        }

        var migrationResult = await _pluginMigrationRunner.ApplyAsync(loadResult.LoadedPlugin.Module, cancellationToken);
        if (!migrationResult.Success)
        {
            loadResult.LoadedPlugin.LoadContext.Unload();
            target.IsActive = false;
            target.LastError = migrationResult.Error ?? $"Plugin-Migration fuer '{target.Key}' ist fehlgeschlagen.";

            var failed = await _statusStore.UpsertAsync(target, cancellationToken);
            _logger.LogWarning("Plugin {PluginKey} konnte wegen fehlgeschlagener Migration nicht aktiviert werden: {Error}", target.Key, failed.LastError);
            return new PluginLifecycleOperationResult(false, "migration-failed", failed.LastError ?? "Plugin-Migration ist fehlgeschlagen.", MapInstalledRecord(failed));
        }

        target.LastError = null;
        var stored = await _statusStore.UpsertAsync(target, cancellationToken);
        _runtimeRegistry.Register(loadResult.LoadedPlugin.Runtime, loadResult.LoadedPlugin.Module, loadResult.LoadedPlugin.LoadContext);
        await _jobRunner.StartJobsForModuleAsync(target.Key, cancellationToken);
        _logger.LogInformation("Plugin {PluginKey} aktiviert.", target.Key);
        return new PluginLifecycleOperationResult(true, "activated", "Plugin wurde aktiviert.", MapInstalledRecord(stored));
    }

    private static InstalledPluginRecord MapInstalledRecord(PluginStatusRecord record)
    {
        return new InstalledPluginRecord(
            record.Key,
            record.DisplayName,
            Version.TryParse(record.Version, out var parsedVersion) ? parsedVersion : new Version(0, 0),
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

    private static IReadOnlyList<string> DeserializeArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<string>();
        }

        return System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
    }

    private static PluginStatusRecord Clone(PluginStatusRecord source)
    {
        return new PluginStatusRecord
        {
            Id = source.Id,
            Key = source.Key,
            DisplayName = source.DisplayName,
            Version = source.Version,
            Author = source.Author,
            License = source.License,
            EntryPoint = source.EntryPoint,
            RequiredCoreVersion = source.RequiredCoreVersion,
            InstallSource = source.InstallSource,
            PackageHash = source.PackageHash,
            PackagePath = source.PackagePath,
            IsActive = source.IsActive,
            LastError = source.LastError,
            PermissionsJson = source.PermissionsJson,
            ExtensionPointsJson = source.ExtensionPointsJson,
            DependenciesJson = source.DependenciesJson,
            InstalledAtUtc = source.InstalledAtUtc,
            UpdatedAtUtc = source.UpdatedAtUtc
        };
    }
}