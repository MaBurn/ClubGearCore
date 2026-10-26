using System.Text;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 3, checkbox 3.4 DoD: delivery-status notifications are recognised in all three shapes and
/// their failed recipients are extracted correctly, ordinary mail is left alone, and the scan is a
/// no-op while the feature is off. Everything runs against in-memory <see cref="MimeMessage"/>
/// fixtures — no IMAP server is involved.
/// </summary>
public sealed class BounceDsnParsingTests
{
    [Fact]
    public void IsDsn_RecognisesMultipartReport_WithDeliveryStatusReportType()
    {
        var message = Parse(MultipartReportDsn);

        Assert.True(ImapMailboxService.IsDeliveryStatusNotification(message));
    }

    [Fact]
    public void IsDsn_RecognisesMessage_ContainingDeliveryStatusPart_WithoutReportType()
    {
        var message = Parse(MixedWithDeliveryStatusPart);

        Assert.Equal("multipart/mixed", message.Body!.ContentType.MimeType);
        Assert.True(ImapMailboxService.IsDeliveryStatusNotification(message));
    }

    [Theory]
    [InlineData("MAILER-DAEMON@mail.example.org")]
    [InlineData("mailer-daemon@mail.example.org")]
    [InlineData("postmaster@mail.example.org")]
    public void IsDsn_RecognisesMailerDaemonAndPostmasterSenders(string sender)
    {
        var message = Parse(DaemonPlainTextBounce(sender));

        Assert.True(ImapMailboxService.IsDeliveryStatusNotification(message));
    }

    [Fact]
    public void IsDsn_RejectsOrdinaryMail()
    {
        var message = Parse(OrdinaryMail);

        Assert.False(ImapMailboxService.IsDeliveryStatusNotification(message));
        Assert.Empty(ImapMailboxService.ExtractBounces(message));
    }

    [Fact]
    public void ExtractBounces_ReturnsFailedRecipient_WithDiagnosticCode()
    {
        var message = Parse(MultipartReportDsn);

        var notice = Assert.Single(ImapMailboxService.ExtractBounces(message));

        Assert.Equal("ghost@example.org", notice.FailedRecipient);
        Assert.NotNull(notice.Diagnostic);
        Assert.Contains("550 5.1.1", notice.Diagnostic!, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractBounces_StripsRfc822Prefix_FromFinalRecipient()
    {
        var message = Parse(MultipartReportDsn);

        var notice = Assert.Single(ImapMailboxService.ExtractBounces(message));

        Assert.DoesNotContain("rfc822", notice.FailedRecipient, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(";", notice.FailedRecipient, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractBounces_FallsBackToOriginalRecipient_WhenFinalRecipientMissing()
    {
        var message = Parse(OriginalRecipientOnlyDsn);

        var notice = Assert.Single(ImapMailboxService.ExtractBounces(message));

        Assert.Equal("legacy@example.org", notice.FailedRecipient);
    }

    [Fact]
    public void ExtractBounces_FallsBackToStatus_WhenDiagnosticCodeMissing()
    {
        var message = Parse(OriginalRecipientOnlyDsn);

        var notice = Assert.Single(ImapMailboxService.ExtractBounces(message));

        Assert.Equal("5.1.1", notice.Diagnostic);
    }

    [Fact]
    public void ExtractBounces_ReturnsEveryFailedRecipient_OfAMultiRecipientReport()
    {
        var message = Parse(MultiRecipientDsn);

        var notices = ImapMailboxService.ExtractBounces(message);

        Assert.Equal(
            new[] { "first@example.org", "second@example.org" },
            notices.Select(n => n.FailedRecipient).ToArray());
    }

    [Fact]
    public void ExtractBounces_IgnoresDeliveredAndDelayedRecipients()
    {
        var message = Parse(MixedActionsDsn);

        var notice = Assert.Single(ImapMailboxService.ExtractBounces(message));

        Assert.Equal("failed@example.org", notice.FailedRecipient);
    }

    [Fact]
    public void ExtractBounces_ReturnsEmpty_ForDaemonMailWithoutDeliveryStatusPart()
    {
        var message = Parse(DaemonPlainTextBounce("MAILER-DAEMON@mail.example.org"));

        Assert.True(ImapMailboxService.IsDeliveryStatusNotification(message));
        Assert.Empty(ImapMailboxService.ExtractBounces(message));
    }

    [Fact]
    public async Task FetchBouncesAsync_ReturnsEmpty_WhenBounceCheckDisabled()
    {
        var service = CreateService(bounceCheckEnabled: false, imapHost: "imap.example.org");

        Assert.Empty(await service.FetchBouncesAsync());
    }

    [Fact]
    public async Task FetchBouncesAsync_ReturnsEmpty_WhenImapHostIsEmpty()
    {
        var service = CreateService(bounceCheckEnabled: true, imapHost: string.Empty);

        Assert.Empty(await service.FetchBouncesAsync());
    }

    private static ImapMailboxService CreateService(bool bounceCheckEnabled, string imapHost)
    {
        var config = new FakeSystemConfigService
        {
            Values =
            {
                [nameof(EmailSettings.ImapHost)] = imapHost,
                [nameof(EmailSettings.ImapPort)] = "993",
                [nameof(EmailSettings.BounceFolder)] = "INBOX",
                [nameof(EmailSettings.BounceCheckEnabled)] = bounceCheckEnabled ? "true" : "false"
            }
        };

        return new ImapMailboxService(config, NullLogger<ImapMailboxService>.Instance);
    }

    private static MimeMessage Parse(string raw)
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(raw.ReplaceLineEndings("\r\n")));
        return MimeMessage.Load(stream);
    }

    private const string MultipartReportDsn = """
From: Mail Delivery System <MAILER-DAEMON@mail.example.org>
To: noreply@example.org
Subject: Undelivered Mail Returned to Sender
Date: Fri, 24 Jul 2026 10:00:00 +0000
MIME-Version: 1.0
Content-Type: multipart/report; report-type=delivery-status; boundary="BOUND"

--BOUND
Content-Type: text/plain; charset=us-ascii

This is the mail system at host mail.example.org.

--BOUND
Content-Type: message/delivery-status

Reporting-MTA: dns; mail.example.org

Final-Recipient: rfc822; ghost@example.org
Original-Recipient: rfc822; ghost@example.org
Action: failed
Status: 5.1.1
Diagnostic-Code: smtp; 550 5.1.1 Recipient address rejected: User unknown

--BOUND--

""";

    private const string MixedWithDeliveryStatusPart = """
From: Postfach <mail@mail.example.org>
To: noreply@example.org
Subject: Zustellung fehlgeschlagen
Date: Fri, 24 Jul 2026 10:00:00 +0000
MIME-Version: 1.0
Content-Type: multipart/mixed; boundary="BOUND"

--BOUND
Content-Type: text/plain; charset=us-ascii

Die Nachricht konnte nicht zugestellt werden.

--BOUND
Content-Type: message/delivery-status

Reporting-MTA: dns; mail.example.org

Final-Recipient: rfc822; ghost@example.org
Action: failed
Status: 5.1.1
Diagnostic-Code: smtp; 550 5.1.1 User unknown

--BOUND--

""";

    private const string OriginalRecipientOnlyDsn = """
From: Mail Delivery System <MAILER-DAEMON@mail.example.org>
To: noreply@example.org
Subject: Undelivered Mail Returned to Sender
Date: Fri, 24 Jul 2026 10:00:00 +0000
MIME-Version: 1.0
Content-Type: multipart/report; report-type=delivery-status; boundary="BOUND"

--BOUND
Content-Type: message/delivery-status

Reporting-MTA: dns; mail.example.org

Original-Recipient: rfc822; legacy@example.org
Action: failed
Status: 5.1.1

--BOUND--

""";

    private const string MultiRecipientDsn = """
From: Mail Delivery System <MAILER-DAEMON@mail.example.org>
To: noreply@example.org
Subject: Undelivered Mail Returned to Sender
Date: Fri, 24 Jul 2026 10:00:00 +0000
MIME-Version: 1.0
Content-Type: multipart/report; report-type=delivery-status; boundary="BOUND"

--BOUND
Content-Type: message/delivery-status

Reporting-MTA: dns; mail.example.org

Final-Recipient: rfc822; first@example.org
Action: failed
Status: 5.1.1
Diagnostic-Code: smtp; 550 5.1.1 User unknown

Final-Recipient: rfc822; second@example.org
Action: failed
Status: 5.2.1
Diagnostic-Code: smtp; 550 5.2.1 Mailbox disabled

--BOUND--

""";

    private const string MixedActionsDsn = """
From: Mail Delivery System <MAILER-DAEMON@mail.example.org>
To: noreply@example.org
Subject: Delivery Status Notification
Date: Fri, 24 Jul 2026 10:00:00 +0000
MIME-Version: 1.0
Content-Type: multipart/report; report-type=delivery-status; boundary="BOUND"

--BOUND
Content-Type: message/delivery-status

Reporting-MTA: dns; mail.example.org

Final-Recipient: rfc822; delivered@example.org
Action: delivered
Status: 2.0.0

Final-Recipient: rfc822; delayed@example.org
Action: delayed
Status: 4.4.1

Final-Recipient: rfc822; failed@example.org
Action: failed
Status: 5.1.1
Diagnostic-Code: smtp; 550 5.1.1 User unknown

--BOUND--

""";

    private const string OrdinaryMail = """
From: Mitglied <member@example.org>
To: noreply@example.org
Subject: Frage zur Mitgliedschaft
Date: Fri, 24 Jul 2026 10:00:00 +0000
MIME-Version: 1.0
Content-Type: text/plain; charset=us-ascii

Hallo, ich habe eine Frage zu meinem Beitrag.

""";

    private static string DaemonPlainTextBounce(string sender) => $"""
From: Mail Delivery System <{sender}>
To: noreply@example.org
Subject: Undeliverable
Date: Fri, 24 Jul 2026 10:00:00 +0000
MIME-Version: 1.0
Content-Type: text/plain; charset=us-ascii

Your message could not be delivered to ghost@example.org.

""";

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
