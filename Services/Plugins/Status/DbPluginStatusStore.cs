using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Plugins.Status;

public sealed class DbPluginStatusStore : IPluginStatusStore
{
    private readonly ApplicationDbContext _dbContext;

    public DbPluginStatusStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public PluginStatusRecord? GetByKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return _dbContext.PluginStatusRecords
            .AsNoTracking()
            .SingleOrDefault(record => record.Key == key);
    }

    public IReadOnlyList<PluginStatusRecord> List()
    {
        return _dbContext.PluginStatusRecords
            .AsNoTracking()
            .OrderBy(record => record.Key)
            .ToArray();
    }

    public async Task<PluginStatusRecord> UpsertAsync(PluginStatusRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var existing = await _dbContext.PluginStatusRecords
            .SingleOrDefaultAsync(status => status.Key == record.Key, cancellationToken);

        if (existing is null)
        {
            if (record.InstalledAtUtc == default)
            {
                record.InstalledAtUtc = DateTime.UtcNow;
            }

            if (record.UpdatedAtUtc == default)
            {
                record.UpdatedAtUtc = record.InstalledAtUtc;
            }

            _dbContext.PluginStatusRecords.Add(record);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return record;
        }

        existing.DisplayName = record.DisplayName;
        existing.Version = record.Version;
        existing.Author = record.Author;
        existing.License = record.License;
        existing.EntryPoint = record.EntryPoint;
        existing.RequiredCoreVersion = record.RequiredCoreVersion;
        existing.InstallSource = record.InstallSource;
        existing.PackageHash = record.PackageHash;
        existing.PackagePath = record.PackagePath;
        existing.IsActive = record.IsActive;
        existing.LastError = record.LastError;
        existing.PermissionsJson = record.PermissionsJson;
        existing.ExtensionPointsJson = record.ExtensionPointsJson;
        existing.InstalledAtUtc = record.InstalledAtUtc == default
            ? existing.InstalledAtUtc
            : record.InstalledAtUtc;
        existing.UpdatedAtUtc = record.UpdatedAtUtc == default
            ? DateTime.UtcNow
            : record.UpdatedAtUtc;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var records = _dbContext.PluginStatusRecords
            .Where(record => record.Key == key)
            .ToList();

        _dbContext.PluginStatusRecords.RemoveRange(records);
        await _dbContext.SaveChangesAsync(ct);
    }
}