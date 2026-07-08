using System.Security.Claims;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Core;

public sealed class SelfServiceFeatureService : ISelfServiceFeatureService
{
    private const long MaxProfileImageBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    };

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp"
    };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;
    private readonly IMessageComposer _messageComposer;
    private readonly IProfileImageStorageService _profileImageStorageService;

    public SelfServiceFeatureService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        IMessageComposer messageComposer,
        IProfileImageStorageService profileImageStorageService)
    {
        _db = db;
        _userManager = userManager;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _messageComposer = messageComposer;
        _profileImageStorageService = profileImageStorageService;
    }

    public async Task<SelfServiceDashboardOutcome> GetDashboardAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
        {
            return new SelfServiceDashboardOutcome(true, null, false);
        }

        var member = await ResolveMemberAsync(user, includeAddresses: true, asNoTracking: true, cancellationToken);
        return new SelfServiceDashboardOutcome(false, member, member is not null);
    }

    public async Task<SelfServiceProfileOutcome> GetProfileAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
        {
            return new SelfServiceProfileOutcome(true, null);
        }

        var member = await ResolveMemberAsync(user, includeAddresses: true, asNoTracking: true, cancellationToken);

        var profile = new SelfServiceProfileViewModel
        {
            FullName = user.FullName ?? string.Empty,
            Email = member?.Email ?? user.Email ?? string.Empty,
            PhoneNumber = member?.PhoneNumber ?? user.PhoneNumber,
            MemberLinked = member is not null,
            MemberNumber = member?.MemberNumber,
            MemberActive = member?.IsActive ?? false,
            MemberJoinedAt = member?.JoinedAt,
            FirstName = member?.FirstName,
            LastName = member?.LastName,
            DateOfBirth = member?.DateOfBirth,
            LastUpdated = member?.LastUpdated,
            ProfileImagePath = member?.ProfileImagePath,
            PendingEmail = member?.PendingEmail,
            Addresses = member?.Addresses
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.Id)
                .Select(a => new SelfServiceAddressInputViewModel
                {
                    Id = a.Id,
                    Street = a.Street,
                    PostalCode = a.PostalCode,
                    City = a.City,
                    Country = a.Country,
                    IsDefault = a.IsDefault
                })
                .ToList()
                ?? new List<SelfServiceAddressInputViewModel>()
        };

        return new SelfServiceProfileOutcome(false, profile, member);
    }

    public async Task<SelfServiceProfileUpdateOutcome> UpdateProfileAsync(
        ClaimsPrincipal principal,
        SelfServiceProfileViewModel model,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
        {
            return new SelfServiceProfileUpdateOutcome(true, false, Array.Empty<string>());
        }

        var before = new
        {
            user.FullName,
            user.Email,
            user.PhoneNumber
        };

        var member = await ResolveMemberAsync(user, includeAddresses: true, asNoTracking: false, cancellationToken);

        user.FullName = model.FullName;
        user.Email = model.Email;
        user.UserName = model.Email;
        user.PhoneNumber = model.PhoneNumber;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return new SelfServiceProfileUpdateOutcome(
                false,
                false,
                result.Errors.Select(e => e.Description).ToArray());
        }

        await _auditLogService.LogChangeAsync(
            action: "SelfService.Profile.Update",
            before: before,
            after: new { user.FullName, user.Email, user.PhoneNumber },
            actor: user.Id,
            source: "mvc",
            targetType: nameof(ApplicationUser),
            targetId: user.Id,
            cancellationToken: cancellationToken);

        if (member is not null)
        {
            var memberBefore = new
            {
                member.Email,
                member.PhoneNumber,
                AddressCount = member.Addresses.Count
            };

            member.Email = model.Email;
            member.PhoneNumber = model.PhoneNumber;
            MergeMemberAddresses(member, model.Addresses);
            member.LastUpdated = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            await _auditLogService.LogChangeAsync(
                action: "SelfService.MemberSync.Update",
                before: memberBefore,
                after: new { member.Email, member.PhoneNumber, member.LastUpdated, AddressCount = member.Addresses.Count },
                actor: user.Id,
                source: "mvc",
                targetType: nameof(Member),
                targetId: member.Id.ToString(),
                cancellationToken: cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var rendered = _messageComposer.Compose(
                subjectTemplate: "Dein ClubGear-Profil wurde aktualisiert",
                bodyTemplate: "<p>Hallo {{FullName}},</p><p>deine Profildaten wurden erfolgreich gespeichert.</p>",
                values: new Dictionary<string, string>
                {
                    ["FullName"] = user.FullName ?? user.Email,
                    ["Email"] = user.Email
                });

            await _notificationService.NotifyAsync(new NotificationMessage(
                Recipient: user.Email,
                Subject: rendered.Subject,
                Body: rendered.Body,
                Channel: "email",
                CorrelationId: $"profile-update:{user.Id}"), cancellationToken);
        }

        return new SelfServiceProfileUpdateOutcome(false, true, Array.Empty<string>());
    }

    public async Task<SelfServiceProfileImageOutcome> UploadProfileImageAsync(
        ClaimsPrincipal principal,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
        {
            return new SelfServiceProfileImageOutcome(true, false, null, "Nicht angemeldet.");
        }

        var member = await ResolveMemberAsync(user, includeAddresses: false, asNoTracking: false, cancellationToken);
        if (member is null)
        {
            return new SelfServiceProfileImageOutcome(false, false, null, "Kein verknuepftes Mitglied gefunden.");
        }

        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (!AllowedImageExtensions.Contains(extension)
            || !AllowedImageContentTypes.Contains(contentType ?? string.Empty))
        {
            return new SelfServiceProfileImageOutcome(false, false, null, "Ungueltiger Dateityp. Erlaubt sind JPG, PNG, GIF und WebP.");
        }

        await using var bufferedContent = new MemoryStream();
        await content.CopyToAsync(bufferedContent, cancellationToken);
        if (bufferedContent.Length == 0)
        {
            return new SelfServiceProfileImageOutcome(false, false, null, "Die Datei ist leer.");
        }

        if (bufferedContent.Length > MaxProfileImageBytes)
        {
            return new SelfServiceProfileImageOutcome(false, false, null, "Die Datei ist zu gross. Maximal 5 MB sind erlaubt.");
        }

        bufferedContent.Position = 0;
        var oldImagePath = member.ProfileImagePath;
        var imagePath = await _profileImageStorageService.SaveProfileImageAsync(
            member.Id,
            extension,
            bufferedContent,
            cancellationToken);

        member.ProfileImagePath = imagePath;
        member.LastUpdated = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldImagePath))
        {
            await _profileImageStorageService.DeleteProfileImageAsync(oldImagePath, cancellationToken);
        }

        return new SelfServiceProfileImageOutcome(false, true, imagePath, null);
    }

    public async Task<SelfServiceProfileImageOutcome> DeleteProfileImageAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.GetUserAsync(principal);
        if (user is null)
        {
            return new SelfServiceProfileImageOutcome(true, false, null, "Nicht angemeldet.");
        }

        var member = await ResolveMemberAsync(user, includeAddresses: false, asNoTracking: false, cancellationToken);
        if (member is null)
        {
            return new SelfServiceProfileImageOutcome(false, false, null, "Kein verknuepftes Mitglied gefunden.");
        }

        var oldImagePath = member.ProfileImagePath;
        member.ProfileImagePath = null;
        member.LastUpdated = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(oldImagePath))
        {
            await _profileImageStorageService.DeleteProfileImageAsync(oldImagePath, cancellationToken);
        }

        return new SelfServiceProfileImageOutcome(false, true, null, null);
    }

    private IQueryable<Member> BuildMemberQuery(bool includeAddresses, bool asNoTracking)
    {
        IQueryable<Member> query = _db.Members;
        if (includeAddresses)
        {
            query = query.Include(m => m.Addresses);
        }

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return query;
    }

    private async Task<Member?> ResolveMemberAsync(
        ApplicationUser user,
        bool includeAddresses,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        var member = await BuildMemberQuery(includeAddresses, asNoTracking)
            .FirstOrDefaultAsync(m => m.ApplicationUserId == user.Id, cancellationToken);
        if (member is not null)
        {
            return member;
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return null;
        }

        var normalizedEmail = user.Email.Trim().ToLower();

        var fallbackCandidates = await BuildMemberQuery(includeAddresses, asNoTracking: false)
            .Where(m => (m.ApplicationUserId == null || m.ApplicationUserId.Trim() == string.Empty)
                        && m.Email != null
                        && m.Email.ToLower() == normalizedEmail)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (fallbackCandidates.Count != 1)
        {
            return await TryResolveSingleLegacyMemberAsync(includeAddresses, asNoTracking, cancellationToken);
        }

        var linkedMember = fallbackCandidates[0];
        linkedMember.ApplicationUserId = user.Id;
        linkedMember.LastUpdated = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (asNoTracking)
        {
            return await BuildMemberQuery(includeAddresses, asNoTracking: true)
                .FirstOrDefaultAsync(m => m.Id == linkedMember.Id, cancellationToken);
        }

        return linkedMember;
    }

    private async Task<Member?> TryResolveSingleLegacyMemberAsync(
        bool includeAddresses,
        bool asNoTracking,
        CancellationToken cancellationToken)
    {
        // Legacy-Dev-Sonderfall: genau 1 User und 1 nicht verknuepfter Member.
        // Nur in diesem eindeutigen Fall wird automatisch verknuepft.
        var userCount = await _db.Users.CountAsync(cancellationToken);
        if (userCount != 1)
        {
            return null;
        }

        var memberCount = await _db.Members.CountAsync(cancellationToken);
        if (memberCount != 1)
        {
            return null;
        }

        var unlinkedMembers = await _db.Members
            .Where(m => m.ApplicationUserId == null || m.ApplicationUserId.Trim() == string.Empty)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (unlinkedMembers.Count != 1)
        {
            return null;
        }

        var onlyUser = await _db.Users.AsNoTracking().SingleAsync(cancellationToken);
        var linkedMember = unlinkedMembers[0];
        linkedMember.ApplicationUserId = onlyUser.Id;
        linkedMember.LastUpdated = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (asNoTracking)
        {
            return await BuildMemberQuery(includeAddresses, asNoTracking: true)
                .FirstOrDefaultAsync(m => m.Id == linkedMember.Id, cancellationToken);
        }

        return linkedMember;
    }

    private static void MergeMemberAddresses(Member member, IReadOnlyCollection<SelfServiceAddressInputViewModel>? incomingAddresses)
    {
        if (incomingAddresses is null)
        {
            incomingAddresses = Array.Empty<SelfServiceAddressInputViewModel>();
        }

        var normalized = incomingAddresses
            .Select(a => new SelfServiceAddressInputViewModel
            {
                Id = a.Id,
                Street = a.Street?.Trim(),
                PostalCode = a.PostalCode?.Trim(),
                City = a.City?.Trim(),
                Country = a.Country?.Trim(),
                IsDefault = a.IsDefault
            })
            .Where(a => !string.IsNullOrWhiteSpace(a.Street)
                        || !string.IsNullOrWhiteSpace(a.PostalCode)
                        || !string.IsNullOrWhiteSpace(a.City)
                        || !string.IsNullOrWhiteSpace(a.Country))
            .ToList();

        var existingById = member.Addresses.ToDictionary(a => a.Id);
        var incomingIds = normalized.Where(a => a.Id > 0).Select(a => a.Id).ToHashSet();

        var toRemove = member.Addresses.Where(a => a.Id > 0 && !incomingIds.Contains(a.Id)).ToList();
        foreach (var address in toRemove)
        {
            member.Addresses.Remove(address);
        }

        foreach (var incoming in normalized)
        {
            if (incoming.Id > 0 && existingById.TryGetValue(incoming.Id, out var existing))
            {
                existing.Street = incoming.Street;
                existing.PostalCode = incoming.PostalCode;
                existing.City = incoming.City;
                existing.Country = incoming.Country;
                existing.IsDefault = incoming.IsDefault;
                continue;
            }

            member.Addresses.Add(new MemberAddress
            {
                Street = incoming.Street,
                PostalCode = incoming.PostalCode,
                City = incoming.City,
                Country = incoming.Country,
                IsDefault = incoming.IsDefault
            });
        }

        if (member.Addresses.Count > 0 && !member.Addresses.Any(a => a.IsDefault))
        {
            member.Addresses[0].IsDefault = true;
        }

        if (member.Addresses.Count(a => a.IsDefault) > 1)
        {
            var consumed = false;
            foreach (var address in member.Addresses)
            {
                if (!consumed && address.IsDefault)
                {
                    consumed = true;
                    continue;
                }

                address.IsDefault = false;
            }
        }
    }
}
