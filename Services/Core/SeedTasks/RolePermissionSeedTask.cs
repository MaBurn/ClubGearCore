using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Core.SeedTasks;

public class RolePermissionSeedTask : ISeedTask
{
    public int Order => 30;

    public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in new[] { RoleNames.Admin, RoleNames.MemberManager, RoleNames.MemberSelfService, RoleNames.Kassenwart })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var desired = new Dictionary<string, string[]>
        {
            [RoleNames.Admin] = new[] { PermissionKeys.Wildcard, PermissionKeys.AdminAccess },
            [RoleNames.MemberManager] = new[] { PermissionKeys.MembersRead, PermissionKeys.MembersManage, PermissionKeys.SelfServiceAccess, PermissionKeys.SelfServiceProfileEdit },
            [RoleNames.MemberSelfService] = new[] { PermissionKeys.SelfServiceAccess, PermissionKeys.SelfServiceProfileEdit },
            [RoleNames.Kassenwart] = new[]
            {
                PermissionKeys.MembersRead,
                "clubgear.plugin.finance.kassenwart.access"
            }
        };

        var existing = await dbContext.RolePermissions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var toInsert = new List<AppRolePermission>();
        foreach (var pair in desired)
        {
            foreach (var permission in pair.Value)
            {
                var hasEntry = existing.Any(e => e.RoleName == pair.Key && e.PermissionKey == permission);
                if (!hasEntry)
                {
                    toInsert.Add(new AppRolePermission
                    {
                        RoleName = pair.Key,
                        PermissionKey = permission
                    });
                }
            }
        }

        if (toInsert.Count > 0)
        {
            dbContext.RolePermissions.AddRange(toInsert);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
