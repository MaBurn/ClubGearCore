using System.Security.Claims;
using ClubGear.Models;

namespace ClubGear.Services.Abstractions;

public sealed record SelfServiceDashboardOutcome(bool RequiresChallenge, Member? Member, bool MemberLinked);

public sealed record SelfServiceProfileOutcome(bool RequiresChallenge, SelfServiceProfileViewModel? Profile, Member? Member = null);

public sealed record SelfServiceProfileUpdateOutcome(bool RequiresChallenge, bool Succeeded, IReadOnlyList<string> Errors);

public sealed record SelfServiceProfileImageOutcome(
    bool RequiresChallenge,
    bool Succeeded,
    string? ImagePath,
    string? ErrorMessage);

public interface ISelfServiceFeatureService
{
    Task<SelfServiceDashboardOutcome> GetDashboardAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    Task<SelfServiceProfileOutcome> GetProfileAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    Task<SelfServiceProfileUpdateOutcome> UpdateProfileAsync(
        ClaimsPrincipal principal,
        SelfServiceProfileViewModel model,
        CancellationToken cancellationToken = default);

    Task<SelfServiceProfileImageOutcome> UploadProfileImageAsync(
        ClaimsPrincipal principal,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);

    Task<SelfServiceProfileImageOutcome> DeleteProfileImageAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
