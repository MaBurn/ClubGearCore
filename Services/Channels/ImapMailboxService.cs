using ClubGear.Models;
using ClubGear.Services.Abstractions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace ClubGear.Services.Channels;

/// <summary>
/// MailKit-backed IMAP access for the mailbox that also serves as the SMTP sender account.
/// Configuration lives in the "Email" section of <see cref="SystemConfigEntry"/> and is read per
/// call, so an admin change takes effect without a restart.
/// </summary>
public sealed class ImapMailboxService : IImapMailboxService
{
    private const string EmailSection = "Email";

    private readonly ISystemConfigService _systemConfigService;
    private readonly ILogger<ImapMailboxService> _logger;

    public ImapMailboxService(ISystemConfigService systemConfigService, ILogger<ImapMailboxService> logger)
    {
        _systemConfigService = systemConfigService;
        _logger = logger;
    }

    public async Task AppendToSentAsync(MimeMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var cfg = await LoadSettingsAsync(cancellationToken);
        if (!cfg.AppendSentEnabled || string.IsNullOrWhiteSpace(cfg.ImapHost))
        {
            return;
        }

        using var client = new ImapClient();
        await ConnectAsync(client, cfg, cancellationToken);

        var sent = await ResolveFolderAsync(client, cfg.SentFolder, cancellationToken);
        if (sent is null)
        {
            // No BCC fallback by design: a misconfigured folder must not silently change delivery.
            _logger.LogWarning("IMAP Gesendet-Ordner '{Folder}' wurde nicht gefunden", cfg.SentFolder);
            await client.DisconnectAsync(true, cancellationToken);
            return;
        }

        await sent.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
        await sent.AppendAsync(message, MessageFlags.Seen, DateTimeOffset.UtcNow, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    public async Task<IReadOnlyList<BounceNotice>> FetchBouncesAsync(CancellationToken cancellationToken = default)
    {
        var cfg = await LoadSettingsAsync(cancellationToken);
        if (!cfg.BounceCheckEnabled || string.IsNullOrWhiteSpace(cfg.ImapHost))
        {
            return Array.Empty<BounceNotice>();
        }

        var notices = new List<BounceNotice>();

        using var client = new ImapClient();
        await ConnectAsync(client, cfg, cancellationToken);

        var folder = await ResolveFolderAsync(client, cfg.BounceFolder, cancellationToken);
        if (folder is null)
        {
            _logger.LogWarning("IMAP Bounce-Ordner '{Folder}' wurde nicht gefunden", cfg.BounceFolder);
            await client.DisconnectAsync(true, cancellationToken);
            return notices;
        }

        await folder.OpenAsync(FolderAccess.ReadWrite, cancellationToken);
        var uids = await folder.SearchAsync(SearchQuery.NotSeen, cancellationToken);

        foreach (var uid in uids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            MimeMessage message;
            try
            {
                message = await folder.GetMessageAsync(uid, cancellationToken);
            }
            catch (MessageNotFoundException)
            {
                continue;
            }

            if (!IsDeliveryStatusNotification(message))
            {
                // Leave ordinary mail untouched — the bounce folder may well be the shared INBOX.
                continue;
            }

            notices.AddRange(ExtractBounces(message));

            // Idempotency: a message we already looked at is never reported a second time.
            await folder.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
        }

        await client.DisconnectAsync(true, cancellationToken);
        return notices;
    }

    /// <summary>
    /// Recognises the three shapes a delivery-status notification arrives in: a
    /// <c>multipart/report</c> with <c>report-type=delivery-status</c>, any message carrying a
    /// <c>message/delivery-status</c> part, or mail from a mailer-daemon/postmaster address.
    /// </summary>
    public static bool IsDeliveryStatusNotification(MimeMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Body?.ContentType is { } contentType &&
            contentType.IsMimeType("multipart", "report") &&
            string.Equals(contentType.Parameters["report-type"], "delivery-status", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (FindDeliveryStatus(message) is not null)
        {
            return true;
        }

        if (message.Sender is not null && IsDaemonAddress(message.Sender))
        {
            return true;
        }

        return message.From.Mailboxes.Any(IsDaemonAddress);
    }

    /// <summary>
    /// Extracts one <see cref="BounceNotice"/> per permanently failed recipient. Returns an empty
    /// list for anything that is not a parseable delivery-status notification.
    /// </summary>
    public static IReadOnlyList<BounceNotice> ExtractBounces(MimeMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!IsDeliveryStatusNotification(message))
        {
            return Array.Empty<BounceNotice>();
        }

        var deliveryStatus = FindDeliveryStatus(message);
        if (deliveryStatus is null)
        {
            return Array.Empty<BounceNotice>();
        }

        var notices = new List<BounceNotice>();
        foreach (var group in deliveryStatus.StatusGroups)
        {
            var action = group["Action"];
            if (string.IsNullOrWhiteSpace(action) ||
                !action.Trim().StartsWith("failed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var recipient = StripAddressType(group["Final-Recipient"]) ?? StripAddressType(group["Original-Recipient"]);
            if (string.IsNullOrWhiteSpace(recipient))
            {
                continue;
            }

            var diagnostic = Normalize(group["Diagnostic-Code"]) ?? Normalize(group["Status"]);
            notices.Add(new BounceNotice(recipient, diagnostic));
        }

        return notices;
    }

    private static MessageDeliveryStatus? FindDeliveryStatus(MimeMessage message)
        => message.BodyParts.OfType<MessageDeliveryStatus>().FirstOrDefault();

    private static bool IsDaemonAddress(MailboxAddress mailbox)
    {
        var address = mailbox.Address;
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var localPart = address.Split('@', 2)[0].Replace("-", string.Empty, StringComparison.Ordinal);
        return localPart.Equals("mailerdaemon", StringComparison.OrdinalIgnoreCase)
            || localPart.Equals("postmaster", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Turns a DSN recipient field such as <c>rfc822; user@example.org</c> into the bare address.
    /// </summary>
    private static string? StripAddressType(string? value)
    {
        var normalized = Normalize(value);
        if (normalized is null)
        {
            return null;
        }

        var separator = normalized.IndexOf(';', StringComparison.Ordinal);
        if (separator >= 0 && !normalized.AsSpan(0, separator).Contains('@'))
        {
            normalized = normalized[(separator + 1)..].Trim();
        }

        return normalized.Length == 0 ? null : normalized;
    }

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private async Task ConnectAsync(ImapClient client, EmailSettings cfg, CancellationToken cancellationToken)
    {
        var socketOptions = cfg.ImapUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
        await client.ConnectAsync(cfg.ImapHost, cfg.ImapPort, socketOptions, cancellationToken);

        var user = string.IsNullOrWhiteSpace(cfg.ImapUsername) ? cfg.Username : cfg.ImapUsername;
        var password = string.IsNullOrWhiteSpace(cfg.ImapPassword) ? cfg.Password : cfg.ImapPassword;
        if (!string.IsNullOrWhiteSpace(user))
        {
            await client.AuthenticateAsync(user, password ?? string.Empty, cancellationToken);
        }
    }

    private static async Task<IMailFolder?> ResolveFolderAsync(ImapClient client, string folderName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folderName) ||
            string.Equals(folderName, "INBOX", StringComparison.OrdinalIgnoreCase))
        {
            return client.Inbox;
        }

        try
        {
            return await client.GetFolderAsync(folderName, cancellationToken);
        }
        catch (FolderNotFoundException)
        {
            // Fall through to a namespace scan: servers name the folder "Sent", "Gesendet",
            // "INBOX.Sent", … and GetFolderAsync only matches the full path.
        }

        foreach (var ns in client.PersonalNamespaces)
        {
            IMailFolder root;
            try
            {
                root = client.GetFolder(ns);
            }
            catch (FolderNotFoundException)
            {
                continue;
            }

            var subfolders = await root.GetSubfoldersAsync(false, cancellationToken);
            var match = subfolders.FirstOrDefault(f => string.Equals(f.Name, folderName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private async Task<EmailSettings> LoadSettingsAsync(CancellationToken cancellationToken)
    {
        var defaults = new EmailSettings();

        var host = await ReadAsync(nameof(EmailSettings.ImapHost), cancellationToken);
        var portValue = await ReadAsync(nameof(EmailSettings.ImapPort), cancellationToken);
        var useSslValue = await ReadAsync(nameof(EmailSettings.ImapUseSsl), cancellationToken);
        var imapUsername = await ReadAsync(nameof(EmailSettings.ImapUsername), cancellationToken);
        var imapPassword = await ReadAsync(nameof(EmailSettings.ImapPassword), cancellationToken);
        var sentFolder = await ReadAsync(nameof(EmailSettings.SentFolder), cancellationToken);
        var appendSentValue = await ReadAsync(nameof(EmailSettings.AppendSentEnabled), cancellationToken);
        var bounceFolder = await ReadAsync(nameof(EmailSettings.BounceFolder), cancellationToken);
        var bounceCheckValue = await ReadAsync(nameof(EmailSettings.BounceCheckEnabled), cancellationToken);
        var bounceIntervalValue = await ReadAsync(nameof(EmailSettings.BouncePollIntervalMinutes), cancellationToken);
        var username = await ReadAsync(nameof(EmailSettings.Username), cancellationToken);
        var password = await ReadAsync(nameof(EmailSettings.Password), cancellationToken);

        return new EmailSettings
        {
            ImapHost = string.IsNullOrWhiteSpace(host) ? defaults.ImapHost : host.Trim(),
            ImapPort = int.TryParse(portValue, out var port) ? port : defaults.ImapPort,
            ImapUseSsl = bool.TryParse(useSslValue, out var useSsl) ? useSsl : defaults.ImapUseSsl,
            ImapUsername = string.IsNullOrWhiteSpace(imapUsername) ? null : imapUsername,
            ImapPassword = string.IsNullOrWhiteSpace(imapPassword) ? null : imapPassword,
            SentFolder = string.IsNullOrWhiteSpace(sentFolder) ? defaults.SentFolder : sentFolder.Trim(),
            AppendSentEnabled = bool.TryParse(appendSentValue, out var appendSent) && appendSent,
            BounceFolder = string.IsNullOrWhiteSpace(bounceFolder) ? defaults.BounceFolder : bounceFolder.Trim(),
            BounceCheckEnabled = bool.TryParse(bounceCheckValue, out var bounceCheck) && bounceCheck,
            BouncePollIntervalMinutes = int.TryParse(bounceIntervalValue, out var interval) ? interval : defaults.BouncePollIntervalMinutes,
            Username = string.IsNullOrWhiteSpace(username) ? null : username,
            Password = string.IsNullOrWhiteSpace(password) ? null : password
        };
    }

    private Task<string?> ReadAsync(string name, CancellationToken cancellationToken)
        => _systemConfigService.GetValueAsync(EmailSection, name, cancellationToken);
}
