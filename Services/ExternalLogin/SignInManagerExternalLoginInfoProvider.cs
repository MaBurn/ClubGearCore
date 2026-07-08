using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace ClubGear.Services.ExternalLogin;

/// <summary>
/// Production implementation of <see cref="IExternalLoginInfoProvider"/> that
/// delegates to <see cref="SignInManager{TUser}.GetExternalLoginInfoAsync"/>.
/// </summary>
internal sealed class SignInManagerExternalLoginInfoProvider : IExternalLoginInfoProvider
{
    private readonly SignInManager<ApplicationUser> _signInManager;

    public SignInManagerExternalLoginInfoProvider(SignInManager<ApplicationUser> signInManager)
    {
        _signInManager = signInManager;
    }

    public Task<ExternalLoginInfo?> GetExternalLoginInfoAsync(CancellationToken ct = default)
        => _signInManager.GetExternalLoginInfoAsync();
}
