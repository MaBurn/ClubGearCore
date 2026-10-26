using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Channels;
using ClubGear.Services.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 5, checkbox 5.3 DoD: NotificationService suppresses email to an address flagged
/// <see cref="Member.EmailInvalid"/> before the channel is touched, persists the send as
/// "Suppressed", leaves valid recipients and non-email channels untouched, and the guard also
/// applies to plugin sends routed through <see cref="ExtensionNotificationFacade"/>.
/// </summary>
public sealed class NotificationSuppressionTests
{
    private const string SuppressionMessage = "Empfängeradresse ungültig – Versand unterdrückt";

    [Fact]
    public async Task NotifyAsync_ToInvalidAddress_NeverInvokesTheChannel()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var db = CreateDbContext(connection);
        await SeedMemberAsync(db, "erika@example.org", emailInvalid: true);

        var channel = new RecordingChannel(SmtpEmailNotificationChannel.Name);
        var service = CreateService(db, channel);

        var result = await service.NotifyAsync(EmailTo("erika@example.org"));

        Assert.Empty(channel.Sent);
        Assert.False(result.Success);
        Assert.Equal(SuppressionMessage, result.Error);
        Assert.Equal(SmtpEmailNotificationChannel.Name, result.Channel);
        Assert.Equal("erika@example.org", result.Recipient);
    }

    [Fact]
    public async Task NotifyAsync_ToInvalidAddress_PersistsTheRecordAsSuppressed()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var db = CreateDbContext(connection);
        await SeedMemberAsync(db, "erika@example.org", emailInvalid: true);

        var channel = new RecordingChannel(SmtpEmailNotificationChannel.Name);
        var service = CreateService(db, channel);

        await service.NotifyAsync(EmailTo("erika@example.org"));

        var record = await db.NotificationRecords.AsNoTracking().SingleAsync();
        Assert.Equal("Suppressed", record.Status);
        Assert.Equal("erika@example.org", record.Recipient);
        Assert.Equal(SmtpEmailNotificationChannel.Name, record.Channel);
        Assert.Equal(SuppressionMessage, record.Error);
        Assert.Equal("Rechnung", record.Subject);
    }

    [Theory]
    [InlineData("ERIKA@EXAMPLE.ORG")]
    [InlineData("  erika@example.org  ")]
    [InlineData("Erika@Example.Org")]
    public async Task NotifyAsync_MatchesTheFlaggedAddressTrimmedAndCaseInsensitively(string recipient)
    {
        await using var connection = await OpenDatabaseAsync();
        await using var db = CreateDbContext(connection);
        await SeedMemberAsync(db, "erika@example.org", emailInvalid: true);

        var channel = new RecordingChannel(SmtpEmailNotificationChannel.Name);
        var service = CreateService(db, channel);

        var result = await service.NotifyAsync(EmailTo(recipient));

        Assert.Empty(channel.Sent);
        Assert.False(result.Success);
        Assert.Equal(SuppressionMessage, result.Error);
    }

    [Fact]
    public async Task NotifyAsync_ToValidAddress_SendsAsBeforeAndPersistsSent()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var db = CreateDbContext(connection);
        await SeedMemberAsync(db, "erika@example.org", emailInvalid: true);
        await SeedMemberAsync(db, "max@example.org", emailInvalid: false, memberNumber: "M-2");

        var channel = new RecordingChannel(SmtpEmailNotificationChannel.Name);
        var service = CreateService(db, channel);

        var result = await service.NotifyAsync(EmailTo("max@example.org"));

        var sent = Assert.Single(channel.Sent);
        Assert.Equal("max@example.org", sent.Recipient);
        Assert.True(result.Success);
        Assert.Null(result.Error);

        var record = await db.NotificationRecords.AsNoTracking().SingleAsync();
        Assert.Equal("Sent", record.Status);
    }

    [Fact]
    public async Task NotifyAsync_ToAnAddressNoMemberOwns_IsNotSuppressed()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var db = CreateDbContext(connection);
        await SeedMemberAsync(db, "erika@example.org", emailInvalid: true);

        var channel = new RecordingChannel(SmtpEmailNotificationChannel.Name);
        var service = CreateService(db, channel);

        var result = await service.NotifyAsync(EmailTo("stranger@example.org"));

        Assert.Single(channel.Sent);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task NotifyAsync_ForANonEmailChannel_IsNeverSuppressed()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var db = CreateDbContext(connection);
        await SeedMemberAsync(db, "erika@example.org", emailInvalid: true);

        var channel = new RecordingChannel("sms");
        var service = CreateService(db, channel);

        var result = await service.NotifyAsync(
            new NotificationMessage("erika@example.org", "Rechnung", "<p>Hallo</p>", "sms"));

        Assert.Single(channel.Sent);
        Assert.True(result.Success);

        var record = await db.NotificationRecords.AsNoTracking().SingleAsync();
        Assert.Equal("Sent", record.Status);
    }

    [Fact]
    public async Task NotifyAsync_ForAFailingChannel_StillPersistsFailed()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var db = CreateDbContext(connection);
        await SeedMemberAsync(db, "max@example.org", emailInvalid: false);

        var channel = new RecordingChannel(SmtpEmailNotificationChannel.Name) { FailWith = "SMTP timeout" };
        var service = CreateService(db, channel);

        var result = await service.NotifyAsync(EmailTo("max@example.org"));

        Assert.False(result.Success);
        var record = await db.NotificationRecords.AsNoTracking().SingleAsync();
        Assert.Equal("Failed", record.Status);
        Assert.Equal("SMTP timeout", record.Error);
    }

    [Fact]
    public async Task NotifyAsync_ForAnUnregisteredChannel_StillPersistsFailed()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var db = CreateDbContext(connection);

        var channel = new RecordingChannel("sms");
        var service = CreateService(db, channel);

        var result = await service.NotifyAsync(EmailTo("max@example.org"));

        Assert.False(result.Success);
        Assert.Equal("Channel nicht registriert", result.Error);
        var record = await db.NotificationRecords.AsNoTracking().SingleAsync();
        Assert.Equal("Failed", record.Status);
    }

    [Fact]
    public async Task PluginSendThroughTheExtensionFacade_InheritsTheSuppressionGuard()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var db = CreateDbContext(connection);
        await SeedMemberAsync(db, "erika@example.org", emailInvalid: true);

        var channel = new RecordingChannel(SmtpEmailNotificationChannel.Name);
        var facade = new ExtensionNotificationFacade(
            CreateService(db, channel),
            NullLogger<ExtensionNotificationFacade>.Instance);

        var result = await facade.NotifyAsync(EmailTo("erika@example.org"));

        Assert.Empty(channel.Sent);
        Assert.False(result.Success);
        Assert.Equal(SuppressionMessage, result.Error);

        var record = await db.NotificationRecords.AsNoTracking().SingleAsync();
        Assert.Equal("Suppressed", record.Status);
    }

    [Fact]
    public async Task PluginSendThroughTheExtensionFacade_ToAValidAddress_StillSends()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var db = CreateDbContext(connection);
        await SeedMemberAsync(db, "max@example.org", emailInvalid: false);

        var channel = new RecordingChannel(SmtpEmailNotificationChannel.Name);
        var facade = new ExtensionNotificationFacade(
            CreateService(db, channel),
            NullLogger<ExtensionNotificationFacade>.Instance);

        var result = await facade.NotifyAsync(EmailTo("max@example.org"));

        Assert.Single(channel.Sent);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task NotifyAsync_AfterTheFlagWasCleared_SendsAgain()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var db = CreateDbContext(connection);
        await SeedMemberAsync(db, "erika@example.org", emailInvalid: true);

        var channel = new RecordingChannel(SmtpEmailNotificationChannel.Name);
        var service = CreateService(db, channel);

        Assert.False((await service.NotifyAsync(EmailTo("erika@example.org"))).Success);

        var member = await db.Members.SingleAsync();
        MemberEmailInvalidationService.ResetInvalidOnChange(member, "erika.neu@example.org");
        member.Email = "erika.neu@example.org";
        await db.SaveChangesAsync();

        var result = await service.NotifyAsync(EmailTo("erika.neu@example.org"));

        Assert.True(result.Success);
        Assert.Single(channel.Sent);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static NotificationMessage EmailTo(string recipient)
        => new(recipient, "Rechnung", "<p>Hallo</p>", SmtpEmailNotificationChannel.Name, "corr-1");

    private static async Task<SqliteConnection> OpenDatabaseAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static ApplicationDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task SeedMemberAsync(
        ApplicationDbContext db,
        string email,
        bool emailInvalid,
        string memberNumber = "M-1")
    {
        db.Members.Add(new Member
        {
            MemberNumber = memberNumber,
            FirstName = "Test",
            LastName = "Person",
            Email = email,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            EmailInvalid = emailInvalid,
            EmailInvalidatedAtUtc = emailInvalid ? DateTime.UtcNow : null,
            EmailInvalidReason = emailInvalid ? "550 5.1.1 unknown mailbox" : null
        });

        await db.SaveChangesAsync();
    }

    private static NotificationService CreateService(ApplicationDbContext db, params INotificationChannel[] channels)
        => new(channels, db, NullLogger<NotificationService>.Instance);

    private sealed class RecordingChannel : INotificationChannel
    {
        public RecordingChannel(string channelName) => ChannelName = channelName;

        public string ChannelName { get; }

        public string? FailWith { get; init; }

        public List<NotificationMessage> Sent { get; } = new();

        public Task<NotificationResult> SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.FromResult(FailWith is null
                ? new NotificationResult(true, ChannelName, message.Recipient)
                : new NotificationResult(false, ChannelName, message.Recipient, FailWith));
        }
    }
}
