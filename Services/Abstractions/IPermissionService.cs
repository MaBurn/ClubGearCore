using System.Security.Claims;

namespace ClubGear.Services.Abstractions;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionKey, CancellationToken cancellationToken = default);
}

public interface IPermissionDefinitionProvider
{
    IEnumerable<PermissionDefinition> GetPermissions();
}

public sealed record PermissionDefinition(string Key, string Description, string Category = "General");
