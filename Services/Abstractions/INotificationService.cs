namespace ClubGear.Services.Abstractions;

public interface INotificationService
{
    Task<NotificationResult> NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}

public interface INotificationChannel
{
    string ChannelName { get; }
    Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}

public sealed record NotificationMessage(
    string Recipient,
    string Subject,
    string Body,
    string Channel,
    string? CorrelationId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record NotificationResult(bool Success, string Channel, string Recipient, string? Error = null);
