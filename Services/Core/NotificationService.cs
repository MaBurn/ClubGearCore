using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Channels;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Core;

public class NotificationService : INotificationService
{
    /// <summary>Notification-record status used when a send was suppressed before transport.</summary>
    public const string SuppressedStatus = "Suppressed";

    /// <summary>Notification-record status used for a successful send.</summary>
    public const string SentStatus = "Sent";

    /// <summary>Notification-record status used for a send that reached the channel and failed.</summary>
    public const string FailedStatus = "Failed";

    /// <summary>Error text reported to the caller when a send is suppressed.</summary>
    public const string SuppressedMessage = "Empfängeradresse ungültig – Versand unterdrückt";

    private readonly IReadOnlyDictionary<string, INotificationChannel> _channels;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IEnumerable<INotificationChannel> channels,
        ApplicationDbContext dbContext,
        ILogger<NotificationService> logger)
    {
        _channels = channels.ToDictionary(c => c.ChannelName, StringComparer.OrdinalIgnoreCase);
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<NotificationResult> NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        // Suppression guard: runs before the channel is resolved or touched, so no transport ever
        // sees a recipient that is known to be undeliverable. Every core and plugin send funnels
        // through here (plugins via ExtensionNotificationFacade), so the guard covers them all.
        if (string.Equals(message.Channel, SmtpEmailNotificationChannel.Name, StringComparison.OrdinalIgnoreCase)
            && await IsRecipientInvalidAsync(message.Recipient, cancellationToken))
        {
            var suppressed = new NotificationResult(false, message.Channel, message.Recipient, SuppressedMessage);
            _logger.LogWarning(
                "E-Mail an {Recipient} unterdrueckt: Adresse ist als ungueltig markiert (Betreff: {Subject})",
                message.Recipient,
                message.Subject);
            await PersistAsync(message, suppressed, SuppressedStatus, cancellationToken);
            return suppressed;
        }

        if (!_channels.TryGetValue(message.Channel, out var channel))
        {
            var unavailable = new NotificationResult(false, message.Channel, message.Recipient, "Channel nicht registriert");
            await PersistAsync(message, unavailable, FailedStatus, cancellationToken);
            return unavailable;
        }

        var result = await channel.SendAsync(message, cancellationToken);
        await PersistAsync(message, result, result.Success ? SentStatus : FailedStatus, cancellationToken);
        return result;
    }

    private async Task<bool> IsRecipientInvalidAsync(string recipient, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return false;
        }

        var lowered = recipient.Trim().ToLowerInvariant();

        return await _dbContext.Members
            .AsNoTracking()
            .AnyAsync(m => m.EmailInvalid && m.Email != null && m.Email.ToLower() == lowered, cancellationToken);
    }

    private async Task PersistAsync(
        NotificationMessage message,
        NotificationResult result,
        string status,
        CancellationToken cancellationToken)
    {
        var row = new NotificationRecord
        {
            Channel = result.Channel,
            Recipient = result.Recipient,
            Subject = message.Subject,
            Body = message.Body,
            Status = status,
            Error = result.Error,
            CorrelationId = message.CorrelationId
        };

        _dbContext.NotificationRecords.Add(row);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
