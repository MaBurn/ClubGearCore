using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Plugins.Persistence;

public sealed class PluginMigrationRunner
{
    private readonly ApplicationDbContext _dbContext;
    private readonly PluginSchemaNamePolicy _schemaNamePolicy;
    private readonly ILogger<PluginMigrationRunner> _logger;

    public PluginMigrationRunner(
        ApplicationDbContext dbContext,
        PluginSchemaNamePolicy schemaNamePolicy,
        ILogger<PluginMigrationRunner> logger)
    {
        _dbContext = dbContext;
        _schemaNamePolicy = schemaNamePolicy;
        _logger = logger;
    }

    public async Task<PluginMigrationRunResult> ApplyAsync(IPluginModule pluginModule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginModule);

        var migrations = pluginModule.GetMigrations();
        if (migrations.Count == 0)
        {
            return new PluginMigrationRunResult(true, null, _schemaNamePolicy.GetTablePrefix(pluginModule.Manifest.ModuleId), 0);
        }

        var appliedMigrationIds = await _dbContext.PluginMigrationStates
            .AsNoTracking()
            .Where(state => state.PluginKey == pluginModule.Manifest.ModuleId)
            .Select(state => state.MigrationId)
            .ToListAsync(cancellationToken);

        var appliedSet = new HashSet<string>(appliedMigrationIds, StringComparer.OrdinalIgnoreCase);
        var executedCount = 0;
        var tablePrefix = _schemaNamePolicy.GetTablePrefix(pluginModule.Manifest.ModuleId);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var context = new PluginDataStore(pluginModule.Manifest.ModuleId, _dbContext, _schemaNamePolicy);
            foreach (var migration in migrations)
            {
                if (string.IsNullOrWhiteSpace(migration.MigrationId))
                {
                    throw new InvalidOperationException($"Plugin '{pluginModule.Manifest.ModuleId}' liefert eine Migration ohne MigrationId.");
                }

                if (!appliedSet.Add(migration.MigrationId))
                {
                    continue;
                }

                await migration.ApplyAsync(context, cancellationToken);

                _dbContext.PluginMigrationStates.Add(new PluginMigrationState
                {
                    PluginKey = pluginModule.Manifest.ModuleId,
                    MigrationId = migration.MigrationId,
                    TablePrefix = tablePrefix,
                    AppliedAtUtc = DateTime.UtcNow
                });

                await _dbContext.SaveChangesAsync(cancellationToken);
                executedCount++;
            }

            await transaction.CommitAsync(cancellationToken);
            return new PluginMigrationRunResult(true, null, tablePrefix, executedCount);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "Plugin-Migrationen fuer {PluginKey} fehlgeschlagen.", pluginModule.Manifest.ModuleId);
            return new PluginMigrationRunResult(false, ex.Message, tablePrefix, executedCount);
        }
    }
}

public sealed record PluginMigrationRunResult(bool Success, string? Error, string TablePrefix, int AppliedMigrationCount);