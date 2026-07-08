using System.Security.Claims;

namespace ClubGear.Services.Abstractions;

public interface IExtensionPermissionFacade
{
    Task<bool> HasPermissionAsync(
        ClaimsPrincipal user,
        string permissionKey,
        IReadOnlyCollection<string> declaredPermissions,
        CancellationToken cancellationToken = default);
}