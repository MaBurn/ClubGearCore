using System.Security.Claims;

namespace ClubGear.Services.Abstractions;

/// <summary>
/// Maps the signed-in principal to its member id, or <c>null</c> when the login has no member
/// behind it.
/// </summary>
public interface ICurrentMemberAccessor
{
    Task<int?> GetMemberIdAsync(ClaimsPrincipal user, CancellationToken ct = default);
}
