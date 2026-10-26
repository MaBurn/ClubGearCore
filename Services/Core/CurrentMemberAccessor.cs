using System.Security.Claims;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Core;

/// <summary>
/// Resolves the signed-in principal to the member record behind it.
/// </summary>
/// <remarks>
/// Not every login has a member: an administrator account may exist without one, and a member row
/// may exist with no <see cref="Member.ApplicationUserId"/> yet. Both cases return <c>null</c>
/// rather than throwing, because "you have no inbox" is a page, not an error.
/// </remarks>
public sealed class CurrentMemberAccessor : ICurrentMemberAccessor
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public CurrentMemberAccessor(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public async Task<int?> GetMemberIdAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = _userManager.GetUserId(user);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return await _dbContext.Members
            .AsNoTracking()
            .Where(m => m.ApplicationUserId == userId)
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync(ct);
    }
}
