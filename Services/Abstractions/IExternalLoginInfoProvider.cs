using Microsoft.AspNetCore.Identity;

namespace ClubGear.Services.Abstractions;

/// <summary>
/// Thin abstraction over <see cref="SignInManager{TUser}.GetExternalLoginInfoAsync"/>
/// so that <c>ExternalLoginService</c> can be unit-tested without a real
/// <see cref="SignInManager{TUser}"/>.
/// </summary>
public interface IExternalLoginInfoProvider
{
    /// <summary>
    /// Returns the <see cref="ExternalLoginInfo"/> obtained from the OIDC callback,
    /// or <c>null</c> when no external login info is present in the current request context.
    /// </summary>
    Task<ExternalLoginInfo?> GetExternalLoginInfoAsync(CancellationToken ct = default);
}
