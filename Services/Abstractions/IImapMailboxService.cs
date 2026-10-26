using MimeKit;

namespace ClubGear.Services.Abstractions;

/// <summary>
/// Mailbox-side companion to the SMTP transport: writes a copy of an outgoing message into the
/// configured IMAP Sent folder so mail sent by ClubGear is visible in the mailbox client.
/// </summary>
public interface IImapMailboxService
{
    /// <summary>
    /// Appends <paramref name="message"/> to the configured Sent folder. A no-op when the feature
    /// is disabled or no IMAP host is configured. Never throws for an unreachable mailbox — the
    /// send it mirrors has already succeeded.
    /// </summary>
    Task AppendToSentAsync(MimeMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans the configured bounce folder for unread delivery-status notifications and returns one
    /// <see cref="BounceNotice"/> per failed recipient. Every message that was looked at is marked
    /// <c>\Seen</c>, so a later scan never reports the same bounce twice. Returns an empty list when
    /// the feature is disabled or no IMAP host is configured.
    /// </summary>
    Task<IReadOnlyList<BounceNotice>> FetchBouncesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A single failed recipient extracted from a delivery-status notification.
/// </summary>
public sealed record BounceNotice(string FailedRecipient, string? Diagnostic);
