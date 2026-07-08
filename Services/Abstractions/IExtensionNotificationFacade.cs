namespace ClubGear.Services.Abstractions;

public interface IExtensionNotificationFacade
{
    Task<NotificationResult> NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default);
}