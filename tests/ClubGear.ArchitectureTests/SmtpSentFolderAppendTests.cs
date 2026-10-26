using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using MimeKit.Text;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 1, checkbox 1.4 DoD: a successful SMTP send mirrors the message into the Sent folder,
/// the mirror is skipped while the feature is off, and an APPEND failure never fails the send.
/// The SMTP transport uses the pickup-directory mode, so no network is involved.
/// </summary>
public sealed class SmtpSentFolderAppendTests : IDisposable
{
    private readonly string _pickupDirectory =
        Path.Combine(Path.GetTempPath(), "clubgear-sent-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SuccessfulSend_AppendsMirror_WithSenderRecipientSubjectAndBody()
    {
        var imap = new RecordingImapMailboxService();
        var channel = CreateChannel(imap, appendSentEnabled: true, imapHost: "imap.example.org");

        var result = await channel.SendAsync(
            new NotificationMessage("member@example.org", "Willkommen", "<p>Hallo Welt</p>", SmtpEmailNotificationChannel.Name));

        Assert.True(result.Success);
        var mirror = Assert.Single(imap.Appended);
        Assert.Equal("ClubGear Testverein", mirror.FromName);
        Assert.Equal("noreply@example.org", mirror.FromAddress);
        Assert.Equal("member@example.org", mirror.To);
        Assert.Equal("Willkommen", mirror.Subject);
        Assert.Contains("Hallo Welt", mirror.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Send_DoesNotAppend_WhenAppendSentDisabled()
    {
        var imap = new RecordingImapMailboxService();
        var channel = CreateChannel(imap, appendSentEnabled: false, imapHost: "imap.example.org");

        var result = await channel.SendAsync(
            new NotificationMessage("member@example.org", "Willkommen", "<p>Hallo</p>", SmtpEmailNotificationChannel.Name));

        Assert.True(result.Success);
        Assert.Empty(imap.Appended);
    }

    [Fact]
    public async Task Send_DoesNotAppend_WhenImapHostIsEmpty()
    {
        var imap = new RecordingImapMailboxService();
        var channel = CreateChannel(imap, appendSentEnabled: true, imapHost: string.Empty);

        var result = await channel.SendAsync(
            new NotificationMessage("member@example.org", "Willkommen", "<p>Hallo</p>", SmtpEmailNotificationChannel.Name));

        Assert.True(result.Success);
        Assert.Empty(imap.Appended);
    }

    [Fact]
    public async Task Send_StillSucceeds_WhenAppendThrows()
    {
        var imap = new RecordingImapMailboxService { ThrowOnAppend = true };
        var channel = CreateChannel(imap, appendSentEnabled: true, imapHost: "imap.example.org");

        var result = await channel.SendAsync(
            new NotificationMessage("member@example.org", "Willkommen", "<p>Hallo</p>", SmtpEmailNotificationChannel.Name));

        Assert.True(result.Success);
        Assert.Null(result.Error);
        Assert.True(imap.AppendAttempted);
    }

    private SmtpEmailNotificationChannel CreateChannel(IImapMailboxService imap, bool appendSentEnabled, string imapHost)
    {
        var config = new FakeSystemConfigService
        {
            Values =
            {
                [nameof(EmailSettings.SenderName)] = "ClubGear Testverein",
                [nameof(EmailSettings.SenderAddress)] = "noreply@example.org",
                [nameof(EmailSettings.Host)] = "localhost",
                [nameof(EmailSettings.Port)] = "25",
                [nameof(EmailSettings.UsePickupDirectory)] = "true",
                [nameof(EmailSettings.PickupDirectory)] = _pickupDirectory,
                [nameof(EmailSettings.ImapHost)] = imapHost,
                [nameof(EmailSettings.AppendSentEnabled)] = appendSentEnabled ? "true" : "false"
            }
        };

        return new SmtpEmailNotificationChannel(config, imap, NullLogger<SmtpEmailNotificationChannel>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_pickupDirectory))
        {
            Directory.Delete(_pickupDirectory, true);
        }
    }

    private sealed record AppendedMirror(string FromName, string FromAddress, string To, string Subject, string HtmlBody);

    private sealed class RecordingImapMailboxService : IImapMailboxService
    {
        public List<AppendedMirror> Appended { get; } = new();
        public bool ThrowOnAppend { get; init; }
        public bool AppendAttempted { get; private set; }

        public Task AppendToSentAsync(MimeMessage message, CancellationToken cancellationToken = default)
        {
            AppendAttempted = true;
            if (ThrowOnAppend)
            {
                throw new InvalidOperationException("IMAP nicht erreichbar");
            }

            var from = (MailboxAddress)message.From[0];
            var to = (MailboxAddress)message.To[0];
            Appended.Add(new AppendedMirror(
                from.Name ?? string.Empty,
                from.Address,
                to.Address,
                message.Subject ?? string.Empty,
                message.GetTextBody(TextFormat.Html) ?? string.Empty));

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BounceNotice>> FetchBouncesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BounceNotice>>(Array.Empty<BounceNotice>());
    }

    private sealed class FakeSystemConfigService : ISystemConfigService
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetValueAsync(string section, string name, CancellationToken cancellationToken = default)
            => Task.FromResult(Values.TryGetValue(name, out var value) ? value : null);

        public Task<IReadOnlyList<SystemConfigEntry>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SystemConfigEntry>>(Array.Empty<SystemConfigEntry>());

        public Task<IReadOnlyList<SystemConfigEntry>> GetBySectionAsync(string section, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SystemConfigEntry>>(Array.Empty<SystemConfigEntry>());

        public Task UpsertAsync(string section, string name, string value, string description, CancellationToken cancellationToken = default)
        {
            Values[name] = value;
            return Task.CompletedTask;
        }

        public Task UpsertManyAsync(IEnumerable<SystemConfigEntry> entries, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
