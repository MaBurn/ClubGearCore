using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Core.SeedTasks;

public class PermissionSeedTask : ISeedTask
{
    public int Order => 20;

    public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var providers = serviceProvider.GetServices<IPermissionDefinitionProvider>().ToList();
        if (providers.Count == 0)
        {
            return;
        }

        var definitions = providers
            .SelectMany(p => p.GetPermissions())
            .GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var existing = await dbContext.Permissions
            .ToDictionaryAsync(p => p.Key, p => p, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var anyChanges = false;

        foreach (var definition in definitions)
        {
            if (existing.TryGetValue(definition.Key, out var row))
            {
                if (row.Description != definition.Description || row.Category != definition.Category)
                {
                    row.Description = definition.Description;
                    row.Category = definition.Category;
                    anyChanges = true;
                }
            }
            else
            {
                dbContext.Permissions.Add(new AppPermission
                {
                    Key = definition.Key,
                    Description = definition.Description,
                    Category = definition.Category
                });
                anyChanges = true;
            }
        }

        if (!anyChanges)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
