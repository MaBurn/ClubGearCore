using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Core.SeedTasks;

/// <summary>
/// Grants the new <see cref="PermissionKeys.MembersTypesManage"/> permission to the
/// <see cref="RoleNames.Admin"/> role, following the same idempotent
/// check-then-insert convention as <see cref="SystemConfigSeedTask"/>. Runs after
/// <see cref="PermissionSeedTask"/> (which registers the permission itself) and
/// <see cref="RolePermissionSeedTask"/> (which creates the core roles).
/// </summary>
public sealed class MembershipTypePermissionSeedTask : ISeedTask
{
    public int Order => 35;

    public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var alreadyGranted = await dbContext.RolePermissions
            .AsNoTracking()
            .AnyAsync(rp => rp.RoleName == RoleNames.Admin && rp.PermissionKey == PermissionKeys.MembersTypesManage, cancellationToken);

        if (alreadyGranted)
        {
            return;
        }

        dbContext.RolePermissions.Add(new AppRolePermission
        {
            RoleName = RoleNames.Admin,
            PermissionKey = PermissionKeys.MembersTypesManage
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
