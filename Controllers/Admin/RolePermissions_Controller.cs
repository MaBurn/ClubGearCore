using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Models.Admin;
using ClubGear.Models.Feedback;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Controllers.Admin;

[Route("Admin/RolePermissions")]
[Authorize]
[PermissionAuthorize(PermissionKeys.AdminAccess)]
public sealed class RolePermissionsController : Controller
{
    private readonly ApplicationDbContext _dbContext;

    public RolePermissionsController(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var roles = await _dbContext.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        var permissions = await _dbContext.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Key)
            .ToListAsync(cancellationToken);

        var rolePermissions = await _dbContext.RolePermissions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var roleRows = roles.Select(role =>
        {
            var grantedKeys = rolePermissions
                .Where(rp => string.Equals(rp.RoleName, role.Name, StringComparison.OrdinalIgnoreCase))
                .Select(rp => rp.PermissionKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return new RolePermissionRowViewModel
            {
                RoleName = role.Name ?? string.Empty,
                GrantedKeys = grantedKeys
            };
        }).ToList();

        var permissionGroups = permissions
            .GroupBy(p => p.Category)
            .Select(g => new PermissionGroupViewModel
            {
                Category = g.Key,
                Permissions = g.ToList()
            })
            .ToList();

        var model = new RolePermissionsViewModel
        {
            Roles = roleRows,
            Permissions = permissionGroups,
            Feedback = ActionFeedbackViewModel.FromTempData(TempData)
        };

        return View("~/Views/Admin/RolePermissions/Index.cshtml", model);
    }

    [HttpPost("Grant")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Grant(string? roleName, string? permissionKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName) || string.IsNullOrWhiteSpace(permissionKey))
        {
            SetFeedback(ActionFeedbackViewModel.Error("Rollenname und Berechtigungsschluessel duerfen nicht leer sein."));
            return RedirectToAction(nameof(Index));
        }

        var permissionExists = await _dbContext.Permissions
            .AnyAsync(p => p.Key == permissionKey, cancellationToken);

        if (!permissionExists)
        {
            SetFeedback(ActionFeedbackViewModel.Error($"Berechtigung '{permissionKey}' ist nicht vorhanden."));
            return RedirectToAction(nameof(Index));
        }

        var duplicate = await _dbContext.RolePermissions
            .AnyAsync(rp => rp.RoleName == roleName && rp.PermissionKey == permissionKey, cancellationToken);

        if (duplicate)
        {
            SetFeedback(ActionFeedbackViewModel.Error($"Berechtigung '{permissionKey}' ist Rolle '{roleName}' bereits zugewiesen."));
            return RedirectToAction(nameof(Index));
        }

        _dbContext.RolePermissions.Add(new AppRolePermission { RoleName = roleName, PermissionKey = permissionKey });
        await _dbContext.SaveChangesAsync(cancellationToken);

        SetFeedback(ActionFeedbackViewModel.Success($"Berechtigung '{permissionKey}' wurde Rolle '{roleName}' zugewiesen."));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(string? roleName, string? permissionKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleName) || string.IsNullOrWhiteSpace(permissionKey))
        {
            SetFeedback(ActionFeedbackViewModel.Error("Rollenname und Berechtigungsschluessel duerfen nicht leer sein."));
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(permissionKey, PermissionKeys.AdminAccess, StringComparison.OrdinalIgnoreCase)
            && string.Equals(roleName, "ClubGear.Admin", StringComparison.OrdinalIgnoreCase))
        {
            SetFeedback(ActionFeedbackViewModel.Error($"Die Wildcard-Berechtigung der Rolle 'ClubGear.Admin' kann nicht entzogen werden."));
            return RedirectToAction(nameof(Index));
        }

        var row = await _dbContext.RolePermissions
            .FirstOrDefaultAsync(rp => rp.RoleName == roleName && rp.PermissionKey == permissionKey, cancellationToken);

        if (row is null)
        {
            SetFeedback(ActionFeedbackViewModel.Warning($"Berechtigung '{permissionKey}' ist Rolle '{roleName}' nicht zugewiesen."));
            return RedirectToAction(nameof(Index));
        }

        _dbContext.RolePermissions.Remove(row);
        await _dbContext.SaveChangesAsync(cancellationToken);

        SetFeedback(ActionFeedbackViewModel.Warning($"Berechtigung '{permissionKey}' wurde Rolle '{roleName}' entzogen."));
        return RedirectToAction(nameof(Index));
    }

    private void SetFeedback(ActionFeedbackViewModel feedback)
    {
        TempData[ActionFeedbackViewModel.TempDataKindKey] = feedback.Kind;
        TempData[ActionFeedbackViewModel.TempDataMessageKey] = feedback.Message;
    }
}
