using System.Text.Json;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Core;

public sealed class PluginPageColumnPreferenceService : IPluginPageColumnPreferenceService
{
    private readonly ApplicationDbContext _dbContext;

    public PluginPageColumnPreferenceService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlySet<string>?> GetHiddenColumnsAsync(
        string userId, string moduleId, string pageKey, CancellationToken cancellationToken = default)
    {
        var json = await _dbContext.PluginPageColumnPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.ModuleId == moduleId && p.PageKey == pageKey)
            .Select(p => p.HiddenColumnsJson)
            .FirstOrDefaultAsync(cancellationToken);

        if (json is null)
        {
            return null;
        }

        var hidden = JsonSerializer.Deserialize<string[]>(json) ?? [];
        return hidden.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task SetHiddenColumnsAsync(
        string userId, string moduleId, string pageKey, IReadOnlyCollection<string> hiddenColumns,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(hiddenColumns.Distinct(StringComparer.OrdinalIgnoreCase));

        var existing = await _dbContext.PluginPageColumnPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ModuleId == moduleId && p.PageKey == pageKey, cancellationToken);

        if (existing is null)
        {
            _dbContext.PluginPageColumnPreferences.Add(new PluginPageColumnPreference
            {
                UserId = userId,
                ModuleId = moduleId,
                PageKey = pageKey,
                HiddenColumnsJson = json,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.HiddenColumnsJson = json;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
