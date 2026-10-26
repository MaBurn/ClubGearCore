using System.Security.Claims;
using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ClubGear.Services.ExternalLogin;

internal sealed class IdentityExternalLoginSessionService : IExternalLoginSessionService
{
    private static readonly HashSet<string> HostAuthorizationClaimTypes = new(
        [
            "permission",
            "permissions",
            "clubgear.system-role",
            "role",
            "roles"
        ],
        StringComparer.OrdinalIgnoreCase);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ClaimsIdentityOptions _claimOptions;

    public IdentityExternalLoginSessionService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IOptions<IdentityOptions> identityOptions)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _claimOptions = identityOptions.Value.ClaimsIdentity;
    }

    public Task ClearExternalAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return _signInManager.Context.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<bool> SignInAsync(
        string? applicationUserId,
        IReadOnlyList<PluginClaimEntry> additionalClaims,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(applicationUserId))
        {
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByIdAsync(applicationUserId);
        if (user is null)
        {
            return false;
        }

        if (!await _signInManager.CanSignInAsync(user))
        {
            return false;
        }

        if (await _userManager.GetLockoutEnabledAsync(user)
            && await _userManager.IsLockedOutAsync(user))
        {
            return false;
        }

        // ClubGear does not yet have a continuation state for the local Identity
        // two-factor flow after an OIDC callback. Deny instead of silently bypassing it.
        if (await _userManager.GetTwoFactorEnabledAsync(user))
        {
            return false;
        }

        var claims = additionalClaims
            .Where(claim => !IsReservedClaimType(claim.Type))
            .Select(claim => new Claim(claim.Type, claim.Value ?? string.Empty))
            .ToArray();

        await _signInManager.SignInWithClaimsAsync(user, isPersistent: true, claims);
        return true;
    }

    private bool IsReservedClaimType(string? claimType)
    {
        if (string.IsNullOrWhiteSpace(claimType)
            || HostAuthorizationClaimTypes.Contains(claimType))
        {
            return true;
        }

        return string.Equals(claimType, _claimOptions.UserIdClaimType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(claimType, _claimOptions.UserNameClaimType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(claimType, _claimOptions.RoleClaimType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(claimType, _claimOptions.SecurityStampClaimType, StringComparison.OrdinalIgnoreCase);
    }
}
