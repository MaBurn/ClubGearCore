using System.Text.Json;
using ClubGear.Data;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Plugins.Uninstall;

public sealed class PluginUninstallService : IPluginUninstallService
{
    private readonly IPluginStatusStore _statusStore;
    private readonly IPluginLifecycleService _lifecycleService;
    private readonly IPluginPackageStore _packageStore;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<PluginUninstallService> _logger;

    public PluginUninstallService(
        IPluginStatusStore statusStore,
        IPluginLifecycleService lifecycleService,
        IPluginPackageStore packageStore,
        ApplicationDbContext dbContext,
        ILogger<PluginUninstallService> logger)
    {
        _statusStore = statusStore;
        _lifecycleService = lifecycleService;
        _packageStore = packageStore;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PluginUninstallResult> UninstallAsync(string moduleId, bool removeData, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var record = _statusStore.GetByKey(moduleId);
        if (record is null)
        {
            return new PluginUninstallResult(false, "not-found", $"Plugin '{moduleId}' ist nicht installiert.");
        }

        if (record.IsActive)
        {
            return new PluginUninstallResult(false, "still-active", $"Plugin '{moduleId}' ist noch aktiv. Bitte zuerst deaktivieren.");
        }

        // Path A and Path B both start with DeactivateAsync (ensures runtime cleanup even if record shows inactive)
        await _lifecycleService.DeactivateAsync(moduleId, ct);

        // Path B: remove plugin data (tables + migration rows) before the combined save
        if (removeData)
        {
            await RemovePluginDataAsync(moduleId, ct);
        }

        // Collect permission keys declared by this plugin, excluding core permissions
        var permissionKeysToRemove = DeserializePermissionKeys(record.PermissionsJson)
            .Where(k => !PermissionKeys.IsCorePermission(k))
            .ToList();

        // Delete status record, role-permission rows, and permission rows in a single transaction + SaveChangesAsync
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            // Remove the status record directly (no separate SaveChangesAsync)
            var statusRows = _dbContext.PluginStatusRecords
                .Where(r => r.Key == moduleId)
                .ToList();
            _dbContext.PluginStatusRecords.RemoveRange(statusRows);

            if (permissionKeysToRemove.Count > 0)
            {
                var rolePermRows = _dbContext.RolePermissions
                    .Where(rp => permissionKeysToRemove.Contains(rp.PermissionKey))
                    .ToList();
                _dbContext.RolePermissions.RemoveRange(rolePermRows);

                var permRows = _dbContext.Permissions
                    .Where(p => permissionKeysToRemove.Contains(p.Key))
                    .ToList();
                _dbContext.Permissions.RemoveRange(permRows);
            }

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning(ex, "Fehler beim Loeschen der Plugin-Daten fuer {PluginKey}.", moduleId);
            throw;
        }

        // Delete the package files
        await _packageStore.DeleteAsync(moduleId, ct);

        _logger.LogInformation("Plugin {PluginKey} wurde deinstalliert (removeData={RemoveData}).", moduleId, removeData);
        return new PluginUninstallResult(true, "uninstalled", $"Plugin '{moduleId}' wurde erfolgreich deinstalliert.");
    }

    private async Task RemovePluginDataAsync(string moduleId, CancellationToken ct)
    {
        var tablePrefixes = await _dbContext.PluginMigrationStates
            .Where(s => s.PluginKey == moduleId)
            .Select(s => s.TablePrefix)
            .Distinct()
            .ToListAsync(ct);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var prefix in tablePrefixes)
            {
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    continue;
                }

                // Find all tables with this prefix via sqlite_master
                var tableNames = await _dbContext.Database
                    .SqlQueryRaw<string>(
                        "SELECT name FROM sqlite_master WHERE type='table' AND name LIKE {0}",
                        prefix + "%")
                    .ToListAsync(ct);

                foreach (var tableName in tableNames)
                {
                    await _dbContext.Database.ExecuteSqlRawAsync($"DROP TABLE IF EXISTS \"{tableName}\"", ct);
                }
            }

            // Remove migration state rows
            var migrationRows = _dbContext.PluginMigrationStates
                .Where(s => s.PluginKey == moduleId);
            _dbContext.PluginMigrationStates.RemoveRange(migrationRows);
            await _dbContext.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogWarning(ex, "Fehler beim Entfernen der Plugin-Daten fuer {PluginKey}.", moduleId);
            throw;
        }
    }

    private static IReadOnlyList<string> DeserializePermissionKeys(string? permissionsJson)
    {
        if (string.IsNullOrWhiteSpace(permissionsJson))
        {
            return Array.Empty<string>();
        }

        return JsonSerializer.Deserialize<string[]>(permissionsJson) ?? Array.Empty<string>();
    }
}
