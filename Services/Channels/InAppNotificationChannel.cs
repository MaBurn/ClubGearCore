using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Channels;

public class InAppNotificationChannel : INotificationChannel
{
    public const string Name = "inapp";
    private readonly ILogger<InAppNotificationChannel> _logger;

    public InAppNotificationChannel(ILogger<InAppNotificationChannel> logger)
    {
        _logger = logger;
    }

    public string ChannelName => Name;

    public Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("InApp Nachricht an {Recipient}: {Subject}", message.Recipient, message.Subject);
        return Task.FromResult(new NotificationResult(true, ChannelName, message.Recipient));
    }
}
