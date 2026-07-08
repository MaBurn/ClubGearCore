using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Core;

public class ExtensionNotificationFacade : IExtensionNotificationFacade
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<ExtensionNotificationFacade> _logger;

    public ExtensionNotificationFacade(INotificationService notificationService, ILogger<ExtensionNotificationFacade> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<NotificationResult> NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _notificationService.NotifyAsync(message, cancellationToken);
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erweiterungs-Notification-Versand fuer Channel {Channel}", message.Channel);
            throw new UserFriendlyException("Die Benachrichtigung der Erweiterung konnte nicht versendet werden.", ex);
        }
    }
}