using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Core;

public class NotificationService : INotificationService
{
    private readonly IReadOnlyDictionary<string, INotificationChannel> _channels;
    private readonly ApplicationDbContext _dbContext;

    public NotificationService(IEnumerable<INotificationChannel> channels, ApplicationDbContext dbContext)
    {
        _channels = channels.ToDictionary(c => c.ChannelName, StringComparer.OrdinalIgnoreCase);
        _dbContext = dbContext;
    }

    public async Task<NotificationResult> NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        if (!_channels.TryGetValue(message.Channel, out var channel))
        {
            var unavailable = new NotificationResult(false, message.Channel, message.Recipient, "Channel nicht registriert");
            await PersistAsync(message, unavailable, cancellationToken);
            return unavailable;
        }

        var result = await channel.SendAsync(message, cancellationToken);
        await PersistAsync(message, result, cancellationToken);
        return result;
    }

    private async Task PersistAsync(NotificationMessage message, NotificationResult result, CancellationToken cancellationToken)
    {
        var row = new NotificationRecord
        {
            Channel = result.Channel,
            Recipient = result.Recipient,
            Subject = message.Subject,
            Body = message.Body,
            Status = result.Success ? "Sent" : "Failed",
            Error = result.Error,
            CorrelationId = message.CorrelationId
        };

        _dbContext.NotificationRecords.Add(row);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
