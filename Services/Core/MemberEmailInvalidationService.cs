using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubGear.Services.Core;

/// <summary>
/// Marks members whose email address bounced as email-invalid, writes an audit entry and a
/// "Bounced" notification record so the bounce is visible in the admin notification log.
/// </summary>
public sealed class MemberEmailInvalidationService : IMemberEmailInvalidationService
{
    /// <summary>Audit action written whenever a member address is newly flagged.</summary>
    public const string AuditAction = "member.email.invalidated";

    /// <summary>Notification-record status used for a recorded bounce.</summary>
    public const string BouncedStatus = "Bounced";

    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<MemberEmailInvalidationService> _logger;

    public MemberEmailInvalidationService(
        ApplicationDbContext dbContext,
        IAuditLogService auditLogService,
        ILogger<MemberEmailInvalidationService> logger)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// Clears the email-invalid state on <paramref name="member"/> when <paramref name="newEmail"/>
    /// differs from the currently stored address. Call this immediately <em>before</em> assigning the
    /// new address, at every write path that can change <see cref="Member.Email"/> (admin edit,
    /// self-service profile, CSV import). A change that only differs in casing or surrounding
    /// whitespace is not a correction and keeps the flag.
    /// </summary>
    /// <returns>True when the flag was cleared by this call.</returns>
    public static bool ResetInvalidOnChange(Member member, string? newEmail)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (!member.EmailInvalid)
        {
            return false;
        }

        var current = member.Email?.Trim() ?? string.Empty;
        var incoming = newEmail?.Trim() ?? string.Empty;

        if (string.Equals(current, incoming, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        member.EmailInvalid = false;
        member.EmailInvalidatedAtUtc = null;
        member.EmailInvalidReason = null;
        return true;
    }

    public async Task<int> MarkInvalidByEmailAsync(string address, string? reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return 0;
        }

        var normalized = address.Trim();
        var lowered = normalized.ToLowerInvariant();

        var matches = await _dbContext.Members
            .Where(m => m.Email != null && m.Email.ToLower() == lowered)
            .ToListAsync(cancellationToken);

        var invalidatedAtUtc = DateTime.UtcNow;
        var newlyFlagged = new List<Member>();

        foreach (var member in matches)
        {
            if (member.EmailInvalid)
            {
                // Already flagged: repeated bounces for the same address must not churn the
                // timestamp/reason, and must not produce a second audit entry.
                continue;
            }

            member.EmailInvalid = true;
            member.EmailInvalidatedAtUtc = invalidatedAtUtc;
            member.EmailInvalidReason = Truncate(reason, 1000);
            newlyFlagged.Add(member);
        }

        _dbContext.NotificationRecords.Add(new NotificationRecord
        {
            CreatedAtUtc = invalidatedAtUtc,
            Channel = SmtpEmailNotificationChannel.Name,
            Recipient = normalized,
            Subject = "Unzustellbare E-Mail-Adresse",
            Body = reason ?? string.Empty,
            Status = BouncedStatus,
            Error = reason
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var member in newlyFlagged)
        {
            await _auditLogService.LogAsync(
                new AuditLogRecord(
                    Action: AuditAction,
                    Source: "system",
                    TargetType: nameof(Member),
                    TargetId: member.Id.ToString(),
                    After: new { member.Email, member.EmailInvalid, member.EmailInvalidatedAtUtc, member.EmailInvalidReason },
                    Metadata: new { Address = normalized, Reason = reason },
                    OccurredAtUtc: invalidatedAtUtc),
                cancellationToken);
        }

        if (newlyFlagged.Count > 0)
        {
            _logger.LogWarning(
                "E-Mail-Adresse {Address} als ungueltig markiert ({Count} Mitglied(er)): {Reason}",
                normalized,
                newlyFlagged.Count,
                reason ?? "kein Diagnosetext");
        }
        else
        {
            _logger.LogInformation(
                "Bounce fuer {Address} verzeichnet, kein Mitglied neu markiert",
                normalized);
        }

        return newlyFlagged.Count;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
