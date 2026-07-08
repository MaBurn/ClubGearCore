using System.Security.Claims;
using ClubGear.Data;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Authorization;

public class DatabasePermissionService : IPermissionService
{
    private const string MasterAdminClaimType = "clubgear.system-role";
    private const string MasterAdminClaimValue = "master-admin";

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<Models.ApplicationUser> _userManager;

    public DatabasePermissionService(ApplicationDbContext dbContext, UserManager<Models.ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionKey, CancellationToken cancellationToken = default)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (user.Claims.Any(c =>
            string.Equals(c.Type, MasterAdminClaimType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(c.Value, MasterAdminClaimValue, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (user.Claims.Any(c => c.Type == "permission" && (c.Value == permissionKey || c.Value == PermissionKeys.Wildcard)))
        {
            return true;
        }

        var roleNames = user.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToList();

        // Merge DB roles with cookie claims on every request.
        // This prevents stale sign-in cookies from causing false AccessDenied responses
        // after role changes (e.g., bootstrap admin assignment).
        var appUser = await _userManager.GetUserAsync(user);
        if (appUser is not null)
        {
            var dbRoles = await _userManager.GetRolesAsync(appUser);
            roleNames.AddRange(dbRoles.Where(r => !string.IsNullOrWhiteSpace(r)));
            roleNames = roleNames.Distinct().ToList();
        }

        if (roleNames.Count == 0)
        {
            return false;
        }

        return await _dbContext.RolePermissions
            .AsNoTracking()
            .AnyAsync(
                rp => roleNames.Contains(rp.RoleName) &&
                      (rp.PermissionKey == permissionKey || rp.PermissionKey == PermissionKeys.Wildcard),
                cancellationToken);
    }
}
