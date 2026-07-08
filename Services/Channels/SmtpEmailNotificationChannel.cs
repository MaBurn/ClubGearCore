using System.Net;
using System.Net.Mail;
using ClubGear.Models;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Channels;

public class SmtpEmailNotificationChannel : INotificationChannel
{
    public const string Name = "email";
    private const string EmailSection = "Email";

    private readonly ISystemConfigService _systemConfigService;
    private readonly ILogger<SmtpEmailNotificationChannel> _logger;

    public SmtpEmailNotificationChannel(ISystemConfigService systemConfigService, ILogger<SmtpEmailNotificationChannel> logger)
    {
        _systemConfigService = systemConfigService;
        _logger = logger;
    }

    public string ChannelName => Name;

    public async Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var cfg = await LoadSettingsAsync(cancellationToken);

            if (cfg.UsePickupDirectory)
            {
                var dir = Path.IsPathRooted(cfg.PickupDirectory)
                    ? cfg.PickupDirectory
                    : Path.Combine(AppContext.BaseDirectory, cfg.PickupDirectory);
                Directory.CreateDirectory(dir);

                using var client = new SmtpClient(cfg.Host, cfg.Port)
                {
                    DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                    PickupDirectoryLocation = dir
                };

                using var mail = BuildMessage(cfg, message);
                await client.SendMailAsync(mail, cancellationToken);
                return new NotificationResult(true, ChannelName, message.Recipient);
            }

            using (var client = new SmtpClient(cfg.Host, cfg.Port))
            {
                client.EnableSsl = cfg.UseStartTls;
                if (!string.IsNullOrWhiteSpace(cfg.Username))
                {
                    client.Credentials = new NetworkCredential(cfg.Username, cfg.Password);
                }

                using var mail = BuildMessage(cfg, message);
                await client.SendMailAsync(mail, cancellationToken);
            }

            return new NotificationResult(true, ChannelName, message.Recipient);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Versand einer E-Mail an {Recipient}", message.Recipient);
            return new NotificationResult(false, ChannelName, message.Recipient, ex.Message);
        }
    }

    private async Task<EmailSettings> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        var defaults = new EmailSettings();

        var senderName = await _systemConfigService.GetValueAsync(EmailSection, nameof(EmailSettings.SenderName), cancellationToken) ?? defaults.SenderName;
        var senderAddress = await _systemConfigService.GetValueAsync(EmailSection, nameof(EmailSettings.SenderAddress), cancellationToken) ?? defaults.SenderAddress;
        var host = await _systemConfigService.GetValueAsync(EmailSection, nameof(EmailSettings.Host), cancellationToken) ?? defaults.Host;
        var portValue = await _systemConfigService.GetValueAsync(EmailSection, nameof(EmailSettings.Port), cancellationToken);
        var useStartTlsValue = await _systemConfigService.GetValueAsync(EmailSection, nameof(EmailSettings.UseStartTls), cancellationToken);
        var username = await _systemConfigService.GetValueAsync(EmailSection, nameof(EmailSettings.Username), cancellationToken);
        var password = await _systemConfigService.GetValueAsync(EmailSection, nameof(EmailSettings.Password), cancellationToken);
        var usePickupDirectoryValue = await _systemConfigService.GetValueAsync(EmailSection, nameof(EmailSettings.UsePickupDirectory), cancellationToken);
        var pickupDirectory = await _systemConfigService.GetValueAsync(EmailSection, nameof(EmailSettings.PickupDirectory), cancellationToken) ?? defaults.PickupDirectory;

        return new EmailSettings
        {
            SenderName = string.IsNullOrWhiteSpace(senderName) ? defaults.SenderName : senderName,
            SenderAddress = string.IsNullOrWhiteSpace(senderAddress) ? defaults.SenderAddress : senderAddress,
            Host = string.IsNullOrWhiteSpace(host) ? defaults.Host : host,
            Port = int.TryParse(portValue, out var port) ? port : defaults.Port,
            UseStartTls = bool.TryParse(useStartTlsValue, out var useStartTls) && useStartTls,
            Username = string.IsNullOrWhiteSpace(username) ? null : username,
            Password = string.IsNullOrWhiteSpace(password) ? null : password,
            UsePickupDirectory = bool.TryParse(usePickupDirectoryValue, out var usePickupDirectory) ? usePickupDirectory : defaults.UsePickupDirectory,
            PickupDirectory = string.IsNullOrWhiteSpace(pickupDirectory) ? defaults.PickupDirectory : pickupDirectory
        };
    }

    private static MailMessage BuildMessage(EmailSettings cfg, NotificationMessage message)
    {
        var mail = new MailMessage
        {
            From = new MailAddress(cfg.SenderAddress, cfg.SenderName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = true
        };

        mail.To.Add(message.Recipient);
        return mail;
    }
}
