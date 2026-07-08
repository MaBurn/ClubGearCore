using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Core;

public sealed class SystemConfigService : ISystemConfigService
{
    private readonly ApplicationDbContext _dbContext;

    public SystemConfigService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SystemConfigEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SystemConfigEntries
            .AsNoTracking()
            .OrderBy(e => e.Section)
            .ThenBy(e => e.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SystemConfigEntry>> GetBySectionAsync(string section, CancellationToken cancellationToken = default)
    {
        var normalizedSection = Normalize(section);
        return await _dbContext.SystemConfigEntries
            .AsNoTracking()
            .Where(e => e.Section == normalizedSection)
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> GetValueAsync(string section, string name, CancellationToken cancellationToken = default)
    {
        var normalizedSection = Normalize(section);
        var normalizedName = Normalize(name);

        return await _dbContext.SystemConfigEntries
            .AsNoTracking()
            .Where(e => e.Section == normalizedSection && e.Name == normalizedName)
            .Select(e => e.Value)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(string section, string name, string value, string description, CancellationToken cancellationToken = default)
    {
        var normalizedSection = Normalize(section);
        var normalizedName = Normalize(name);

        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedName);

        var entry = await _dbContext.SystemConfigEntries
            .FirstOrDefaultAsync(e => e.Section == normalizedSection && e.Name == normalizedName, cancellationToken);

        if (entry is null)
        {
            _dbContext.SystemConfigEntries.Add(new SystemConfigEntry
            {
                Section = normalizedSection,
                Name = normalizedName,
                Value = value.Trim(),
                Description = description.Trim(),
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            entry.Value = value.Trim();
            entry.Description = description.Trim();
            entry.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpsertManyAsync(IEnumerable<SystemConfigEntry> entries, CancellationToken cancellationToken = default)
    {
        var materialized = entries.ToList();
        foreach (var entry in materialized)
        {
            await UpsertAsync(entry.Section, entry.Name, entry.Value, entry.Description, cancellationToken);
        }
    }

    public async Task DeleteByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.SystemConfigEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (existing is null)
        {
            return;
        }

        _dbContext.SystemConfigEntries.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string Normalize(string value)
    {
        return value.Trim();
    }
}
