using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using Microsoft.AspNetCore.Identity;

namespace ClubGear.Middleware;

public sealed class MaintenanceModeMiddleware
{
    private readonly RequestDelegate _next;

    public MaintenanceModeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISystemConfigService systemConfigService, UserManager<ApplicationUser> userManager)
    {
        var maintenanceModeValue = await systemConfigService.GetValueAsync("System", "MaintenanceMode", context.RequestAborted);
        if (!bool.TryParse(maintenanceModeValue, out var isMaintenanceModeEnabled) || !isMaintenanceModeEnabled)
        {
            await _next(context);
            return;
        }

        if (IsExcludedPath(context.Request.Path) || await CanBypassMaintenanceAsync(context, userManager))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    message = "Die Anwendung befindet sich derzeit im Wartungsmodus.",
                    statusCode = StatusCodes.Status503ServiceUnavailable
                }
            });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync("<!DOCTYPE html><html lang='de'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Wartungsmodus</title><link href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css' rel='stylesheet'></head><body><div class='container py-5'><div class='alert alert-warning'><h1 class='h4 mb-3'>Wartungsmodus aktiv</h1><p>Die Anwendung ist momentan voruebergehend nicht verfuegbar.</p><p class='mb-0'>Bitte versuche es spaeter erneut.</p></div></div></body></html>");
    }

    private static bool IsExcludedPath(PathString path)
    {
        return path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/css", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/js", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/lib", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Home/Privacy", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<bool> CanBypassMaintenanceAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (context.User.Claims.Any(claim =>
                string.Equals(claim.Type, "permission", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(claim.Value, PermissionKeys.AdminAccess, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(claim.Value, PermissionKeys.Wildcard, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        var currentUser = await userManager.GetUserAsync(context.User);
        if (currentUser is null && !string.IsNullOrWhiteSpace(context.User.Identity?.Name))
        {
            currentUser = await userManager.FindByNameAsync(context.User.Identity.Name);
        }

        if (currentUser is null)
        {
            var emailClaim = context.User.Claims.FirstOrDefault(c =>
                string.Equals(c.Type, "email", StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Type, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(emailClaim?.Value))
            {
                currentUser = await userManager.FindByEmailAsync(emailClaim.Value);
            }
        }

        return currentUser is not null && await userManager.IsInRoleAsync(currentUser, RoleNames.Admin);
    }
}