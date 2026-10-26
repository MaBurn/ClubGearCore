using ClubGear.Data;
using ClubGear.Data.Migrations;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Channels;
using ClubGear.Services.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// CORE-04, slice 1: the core message and thread substrate.
///
/// The decision this file guards is that notifications and chat are the *same* structure. If a
/// second inbox, a second read state or a second unread count ever appears, these tests are where
/// it should become visible.
/// </summary>
public sealed class MessageSubstrateTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero));
    private readonly MessageService _sut;

    private const int Anna = 1;
    private const int Ben = 2;
    private const int Clara = 3;

    public MessageSubstrateTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();
        AddMessageSubstrate_202608310201.ApplyAsync(_dbContext).GetAwaiter().GetResult();

        _sut = new MessageService(_dbContext, _time);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    // ---------------------------------------------------------------------------------------
    // Notification and chat are the same structure
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task A_Notification_And_A_Chat_Land_In_The_Same_Inbox()
    {
        var notification = await _sut.StartThreadAsync(
            MessageThreadKinds.Notification, "Beitrag fällig", [new MessageParticipantSpec(Anna, false)]);
        await _sut.PostAsync(notification.Id, "Dein Beitrag ist fällig.");

        var chat = await _sut.StartThreadAsync(
            MessageThreadKinds.Chat, "Werkstatt", [new MessageParticipantSpec(Anna, true), new MessageParticipantSpec(Ben, true)]);
        await _sut.PostAsync(chat.Id, "Hast du den Schlüssel?", MessageAuthorKinds.Member, Ben);

        var inbox = await _sut.ListForMemberAsync(Anna);

        Assert.Equal(2, inbox.Count);
        Assert.Equal(2, await _sut.UnreadCountAsync(Anna));
    }

    [Fact]
    public async Task A_Plugin_Thread_Uses_The_Same_Inbox_Rather_Than_Its_Own()
    {
        var rental = await _sut.StartThreadAsync(
            "rental",
            "Anhänger AB-123",
            [new MessageParticipantSpec(Anna, true), new MessageParticipantSpec(Ben, true)],
            sourceModuleId: "clubgear.rental",
            correlationId: "rental-42");

        await _sut.PostAsync(rental.Id, "Wann kann ich abholen?", MessageAuthorKinds.Member, Ben);

        var inbox = await _sut.ListForMemberAsync(Anna);

        Assert.Single(inbox);
        Assert.Equal("clubgear.rental", inbox[0].Thread.SourceModuleId);
        Assert.Equal("rental-42", inbox[0].Thread.CorrelationId);
    }

    // ---------------------------------------------------------------------------------------
    // Posting rights
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task A_Member_Cannot_Reply_To_A_Notification_Thread()
    {
        var thread = await _sut.StartThreadAsync(
            MessageThreadKinds.Notification, "Beitrag fällig", [new MessageParticipantSpec(Anna, false)]);

        await Assert.ThrowsAsync<MessagingException>(
            () => _sut.PostAsync(thread.Id, "Warum?", MessageAuthorKinds.Member, Anna));
    }

    [Fact]
    public async Task A_Non_Participant_Cannot_Post()
    {
        var thread = await _sut.StartThreadAsync(
            MessageThreadKinds.Chat, "Werkstatt", [new MessageParticipantSpec(Anna, true)]);

        await Assert.ThrowsAsync<MessagingException>(
            () => _sut.PostAsync(thread.Id, "Hallo", MessageAuthorKinds.Member, Clara));
    }

    [Fact]
    public async Task The_System_Can_Always_Post_Even_Into_A_Read_Only_Thread()
    {
        var thread = await _sut.StartThreadAsync(
            MessageThreadKinds.Notification, "Beitrag", [new MessageParticipantSpec(Anna, false)]);

        var message = await _sut.PostAsync(thread.Id, "Erinnerung");

        Assert.Equal(MessageAuthorKinds.System, message.AuthorKind);
        Assert.Null(message.AuthorMemberId);
    }

    [Fact]
    public async Task A_Closed_Thread_Accepts_No_Further_Posts_But_Stays_Readable()
    {
        var thread = await _sut.StartThreadAsync(
            MessageThreadKinds.Chat, "Werkstatt", [new MessageParticipantSpec(Anna, true)]);
        await _sut.PostAsync(thread.Id, "Erledigt", MessageAuthorKinds.Member, Anna);

        Assert.True(await _sut.CloseThreadAsync(thread.Id));

        await Assert.ThrowsAsync<MessagingException>(() => _sut.PostAsync(thread.Id, "Noch was"));

        var detail = await _sut.GetThreadAsync(thread.Id, Anna);
        Assert.NotNull(detail);
        Assert.Single(detail!.Messages);
        Assert.False(detail.CanPost);
    }

    // ---------------------------------------------------------------------------------------
    // Access
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task A_Non_Participant_Cannot_Tell_Whether_A_Thread_Exists()
    {
        var thread = await _sut.StartThreadAsync(
            MessageThreadKinds.Chat, "Privat", [new MessageParticipantSpec(Anna, true)]);

        Assert.Null(await _sut.GetThreadAsync(thread.Id, Clara));
        Assert.Null(await _sut.GetThreadAsync(999_999, Clara));
    }

    // ---------------------------------------------------------------------------------------
    // Read state and unread counting
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Your_Own_Messages_Never_Count_As_Unread()
    {
        var thread = await _sut.StartThreadAsync(
            MessageThreadKinds.Chat, "Werkstatt", [new MessageParticipantSpec(Anna, true), new MessageParticipantSpec(Ben, true)]);

        await _sut.PostAsync(thread.Id, "Erste", MessageAuthorKinds.Member, Anna);
        await _sut.PostAsync(thread.Id, "Zweite", MessageAuthorKinds.Member, Anna);

        Assert.Equal(0, await _sut.UnreadCountAsync(Anna));
        Assert.Equal(2, await _sut.UnreadCountAsync(Ben));
    }

    [Fact]
    public async Task Marking_Read_Clears_The_Count_And_New_Messages_Raise_It_Again()
    {
        var thread = await _sut.StartThreadAsync(
            MessageThreadKinds.Chat, "Werkstatt", [new MessageParticipantSpec(Anna, true), new MessageParticipantSpec(Ben, true)]);

        var first = await _sut.PostAsync(thread.Id, "Hallo", MessageAuthorKinds.Member, Ben);
        Assert.Equal(1, await _sut.UnreadCountAsync(Anna));

        await _sut.MarkReadAsync(thread.Id, Anna, first.Id);
        Assert.Equal(0, await _sut.UnreadCountAsync(Anna));

        await _sut.PostAsync(thread.Id, "Noch da?", MessageAuthorKinds.Member, Ben);
        Assert.Equal(1, await _sut.UnreadCountAsync(Anna));
    }

    [Fact]
    public async Task Re_Reading_An_Older_Message_Does_Not_Unread_The_Newer_Ones()
    {
        var thread = await _sut.StartThreadAsync(
            MessageThreadKinds.Chat, "Werkstatt", [new MessageParticipantSpec(Anna, true), new MessageParticipantSpec(Ben, true)]);

        var first = await _sut.PostAsync(thread.Id, "Eins", MessageAuthorKinds.Member, Ben);
        var second = await _sut.PostAsync(thread.Id, "Zwei", MessageAuthorKinds.Member, Ben);

        await _sut.MarkReadAsync(thread.Id, Anna, second.Id);
        await _sut.MarkReadAsync(thread.Id, Anna, first.Id);

        Assert.Equal(0, await _sut.UnreadCountAsync(Anna));
    }

    [Fact]
    public async Task A_Closed_Thread_Stops_Contributing_To_The_Unread_Badge()
    {
        var thread = await _sut.StartThreadAsync(
            MessageThreadKinds.Notification, "Alt", [new MessageParticipantSpec(Anna, false)]);
        await _sut.PostAsync(thread.Id, "Ungelesen");

        Assert.Equal(1, await _sut.UnreadCountAsync(Anna));

        await _sut.CloseThreadAsync(thread.Id);

        Assert.Equal(0, await _sut.UnreadCountAsync(Anna));
    }

    // ---------------------------------------------------------------------------------------
    // Listing
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task The_Inbox_Is_Ordered_By_Most_Recent_Activity()
    {
        var older = await _sut.StartThreadAsync(
            MessageThreadKinds.Chat, "Älter", [new MessageParticipantSpec(Anna, true), new MessageParticipantSpec(Ben, true)]);
        await _sut.PostAsync(older.Id, "alt", MessageAuthorKinds.Member, Ben);

        _time.Advance(TimeSpan.FromHours(1));

        var newer = await _sut.StartThreadAsync(
            MessageThreadKinds.Chat, "Neuer", [new MessageParticipantSpec(Anna, true), new MessageParticipantSpec(Ben, true)]);
        await _sut.PostAsync(newer.Id, "neu", MessageAuthorKinds.Member, Ben);

        var inbox = await _sut.ListForMemberAsync(Anna);

        Assert.Equal(["Neuer", "Älter"], inbox.Select(s => s.Thread.Subject));
    }

    [Fact]
    public async Task A_Duplicated_Participant_Keeps_The_More_Permissive_Entry()
    {
        var thread = await _sut.StartThreadAsync(
            MessageThreadKinds.Chat,
            "Werkstatt",
            [new MessageParticipantSpec(Anna, false), new MessageParticipantSpec(Anna, true)]);

        var detail = await _sut.GetThreadAsync(thread.Id, Anna);

        Assert.True(detail!.CanPost);
        Assert.Single(await _dbContext.MessageParticipants.Where(p => p.ThreadId == thread.Id).ToListAsync());
    }

    [Fact]
    public async Task A_Thread_Without_Participants_Is_Refused()
        => await Assert.ThrowsAsync<MessagingException>(
            () => _sut.StartThreadAsync(MessageThreadKinds.Chat, "Niemand", []));

    // ---------------------------------------------------------------------------------------
    // The in-app channel now delivers instead of pretending to
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task InAppChannel_Delivers_Into_The_Members_Inbox()
    {
        _dbContext.Members.Add(new Member { Id = Anna, FirstName = "Anna", LastName = "A", Email = "anna@example.org" });
        await _dbContext.SaveChangesAsync();

        var channel = NewChannel();

        var result = await channel.SendAsync(
            new NotificationMessage("anna@example.org", "Beitrag fällig", "Bitte überweisen.", InAppNotificationChannel.Name));

        Assert.True(result.Success);

        var inbox = await _sut.ListForMemberAsync(Anna);
        Assert.Single(inbox);
        Assert.Equal("Beitrag fällig", inbox[0].Thread.Subject);
        Assert.Equal(1, inbox[0].UnreadCount);
    }

    [Fact]
    public async Task InAppChannel_Reports_Failure_Instead_Of_Silently_Discarding()
    {
        var channel = NewChannel();

        var result = await channel.SendAsync(
            new NotificationMessage("nobody@example.org", "Hallo", "Text", InAppNotificationChannel.Name));

        // Before CORE-04 this returned Success = true and the message was gone.
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Empty(await _dbContext.MessageThreads.ToListAsync());
    }

    [Fact]
    public async Task InAppChannel_Can_Address_A_Member_Without_An_Email_Address()
    {
        _dbContext.Members.Add(new Member { Id = Ben, FirstName = "Ben", LastName = "B", Email = null });
        await _dbContext.SaveChangesAsync();

        var channel = NewChannel();

        var result = await channel.SendAsync(new NotificationMessage(
            Recipient: "",
            Subject: "Nur intern",
            Body: "Text",
            Channel: InAppNotificationChannel.Name,
            Metadata: new Dictionary<string, string> { [InAppNotificationChannel.MemberIdMetadataKey] = Ben.ToString() }));

        Assert.True(result.Success);
        Assert.Single(await _sut.ListForMemberAsync(Ben));
    }

    private InAppNotificationChannel NewChannel()
        => new(_dbContext, _sut, NullLogger<InAppNotificationChannel>.Instance);
}
