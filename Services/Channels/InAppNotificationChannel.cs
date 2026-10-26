using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Channels;

/// <summary>
/// Delivers a notification into the member's in-app inbox by writing it to the core message
/// substrate (CORE-04) as a read-only notification thread.
/// </summary>
/// <remarks>
/// Until CORE-04 this channel only wrote a log line and reported success, so every "inapp"
/// notification was silently discarded while the sender was told it had been delivered.
/// It now either lands in an inbox or reports failure.
/// </remarks>
public class InAppNotificationChannel : INotificationChannel
{
    public const string Name = "inapp";

    /// <summary>
    /// Lets a caller address a member directly instead of by e-mail, which matters for a member
    /// who has no address or whose address is flagged invalid.
    /// </summary>
    public const string MemberIdMetadataKey = "memberId";

    private readonly ApplicationDbContext _dbContext;
    private readonly IMessageService _messageService;
    private readonly ILogger<InAppNotificationChannel> _logger;

    public InAppNotificationChannel(
        ApplicationDbContext dbContext,
        IMessageService messageService,
        ILogger<InAppNotificationChannel> logger)
    {
        _dbContext = dbContext;
        _messageService = messageService;
        _logger = logger;
    }

    public string ChannelName => Name;

    public async Task<NotificationResult> SendAsync(
        NotificationMessage message,
        CancellationToken cancellationToken = default)
    {
        var memberId = await ResolveMemberIdAsync(message, cancellationToken);

        if (memberId is null)
        {
            // Failing is the point. A silent success here is how a notification disappears.
            const string error = "Kein Mitglied zu diesem Empfänger gefunden; die Nachricht hat keinen Posteingang.";
            _logger.LogWarning("InApp-Zustellung an {Recipient} nicht möglich: {Error}", message.Recipient, error);
            return new NotificationResult(false, ChannelName, message.Recipient, error);
        }

        var thread = await _messageService.StartThreadAsync(
            MessageThreadKinds.Notification,
            message.Subject,
            [new MessageParticipantSpec(memberId.Value, CanPost: false)],
            correlationId: message.CorrelationId,
            ct: cancellationToken);

        await _messageService.PostAsync(
            thread.Id,
            message.Body,
            MessageAuthorKinds.System,
            ct: cancellationToken);

        return new NotificationResult(true, ChannelName, message.Recipient);
    }

    private async Task<int?> ResolveMemberIdAsync(NotificationMessage message, CancellationToken ct)
    {
        if (message.Metadata is not null
            && message.Metadata.TryGetValue(MemberIdMetadataKey, out var raw)
            && int.TryParse(raw, out var explicitId))
        {
            var exists = await _dbContext.Members.AsNoTracking().AnyAsync(m => m.Id == explicitId, ct);
            return exists ? explicitId : null;
        }

        if (string.IsNullOrWhiteSpace(message.Recipient))
        {
            return null;
        }

        var byEmail = await _dbContext.Members
            .AsNoTracking()
            .Where(m => m.Email != null && m.Email == message.Recipient)
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync(ct);

        return byEmail;
    }
}
