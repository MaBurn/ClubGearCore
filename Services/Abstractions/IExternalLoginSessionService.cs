using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Abstractions;

/// <summary>
/// Creates the local ASP.NET Core Identity session after an external identity has
/// been mapped to a linked ClubGear application user.
/// </summary>
public interface IExternalLoginSessionService
{
    /// <summary>
    /// Removes the short-lived ASP.NET Core Identity external-login cookie. The
    /// callback calls this before creating a local application session so a failed
    /// mapping or account lookup cannot be replayed with the same cookie.
    /// </summary>
    Task ClearExternalAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    Task<bool> SignInAsync(
        string? applicationUserId,
        IReadOnlyList<PluginClaimEntry> additionalClaims,
        CancellationToken cancellationToken = default);
}
