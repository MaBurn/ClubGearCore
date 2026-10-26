using System.Security.Claims;
using ClubGear.Controllers;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// CORE-04 slice 2: the member-facing inbox.
/// </summary>
public sealed class MessagesControllerTests
{
    private const int Anna = 1;

    [Fact]
    public async Task Index_Explains_Itself_When_The_Login_Has_No_Member()
    {
        using var controller = Create(new StubMessageService(), memberId: null);

        var view = Assert.IsType<ViewResult>(await controller.Index());
        var model = Assert.IsType<MessagesIndexViewModel>(view.Model);

        // A login without a member is a state to explain, not a 403 and not a crash.
        Assert.False(model.MemberLinked);
        Assert.Empty(model.Threads);
    }

    [Fact]
    public async Task Index_Lists_Only_The_Current_Members_Threads()
    {
        var service = new StubMessageService();
        service.Threads[Anna] =
        [
            Summary(1, "Beitrag fällig", unread: 2),
            Summary(2, "Werkstatt", unread: 0)
        ];

        using var controller = Create(service, Anna);

        var view = Assert.IsType<ViewResult>(await controller.Index());
        var model = Assert.IsType<MessagesIndexViewModel>(view.Model);

        Assert.True(model.MemberLinked);
        Assert.Equal(2, model.Threads.Count);
        Assert.Equal(Anna, service.LastListedMemberId);
    }

    [Fact]
    public async Task Detail_Is_NotFound_For_A_Thread_The_Member_Does_Not_Participate_In()
    {
        // The service returns null for both "missing" and "not yours"; the controller must not
        // turn that into anything more informative.
        using var controller = Create(new StubMessageService(), Anna);

        Assert.IsType<NotFoundResult>(await controller.Detail(42));
    }

    [Fact]
    public async Task Opening_A_Thread_Marks_It_Read_Up_To_Its_Last_Message()
    {
        var service = new StubMessageService
        {
            Detail = new MessageThreadDetail(
                new MessageThread { Id = 7, Subject = "Werkstatt", Kind = MessageThreadKinds.Chat },
                [new Message { Id = 10 }, new Message { Id = 11 }],
                CanPost: true,
                UnreadCount: 2)
        };

        using var controller = Create(service, Anna);

        Assert.IsType<ViewResult>(await controller.Detail(7));
        Assert.Equal((7L, Anna, 11L), service.LastMarkRead);
    }

    [Fact]
    public async Task Opening_An_Empty_Thread_Does_Not_Mark_Anything_Read()
    {
        var service = new StubMessageService
        {
            Detail = new MessageThreadDetail(
                new MessageThread { Id = 7 }, [], CanPost: true, UnreadCount: 0)
        };

        using var controller = Create(service, Anna);

        Assert.IsType<ViewResult>(await controller.Detail(7));
        Assert.Null(service.LastMarkRead);
    }

    [Fact]
    public async Task An_Empty_Reply_Is_Refused_Without_Reaching_The_Service()
    {
        var service = new StubMessageService();
        using var controller = Create(service, Anna);

        var result = Assert.IsType<RedirectToActionResult>(await controller.Reply(7, "   "));

        Assert.Equal(nameof(MessagesController.Detail), result.ActionName);
        Assert.Null(service.LastPostBody);
    }

    [Fact]
    public async Task A_Reply_Is_Trimmed_And_Attributed_To_The_Current_Member()
    {
        var service = new StubMessageService();
        using var controller = Create(service, Anna);

        await controller.Reply(7, "  Hallo  ");

        Assert.Equal("Hallo", service.LastPostBody);
        Assert.Equal(MessageAuthorKinds.Member, service.LastPostAuthorKind);
        Assert.Equal(Anna, service.LastPostAuthorMemberId);
    }

    [Fact]
    public async Task A_Refused_Reply_Surfaces_The_Reason_Instead_Of_Failing()
    {
        var service = new StubMessageService { PostThrows = "Keine Berechtigung, in diesem Thread zu schreiben." };
        using var controller = Create(service, Anna);

        var result = Assert.IsType<RedirectToActionResult>(await controller.Reply(7, "Hallo"));

        Assert.Equal(nameof(MessagesController.Detail), result.ActionName);
        Assert.Equal("Keine Berechtigung, in diesem Thread zu schreiben.", controller.TempData["MessageError"]);
    }

    // ---------------------------------------------------------------------------------------

    private static MessagesController Create(IMessageService service, int? memberId)
    {
        var controller = new MessagesController(service, new StubCurrentMember(memberId))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("test")) }
            }
        };

        controller.TempData = new TempDataDictionary(
            controller.ControllerContext.HttpContext, new StubTempDataProvider());

        return controller;
    }

    private static MessageThreadSummary Summary(long id, string subject, int unread)
        => new(new MessageThread { Id = id, Subject = subject }, unread, DateTime.UtcNow, subject);

    private sealed class StubCurrentMember(int? memberId) : ICurrentMemberAccessor
    {
        public Task<int?> GetMemberIdAsync(ClaimsPrincipal user, CancellationToken ct = default)
            => Task.FromResult(memberId);
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    private sealed class StubMessageService : IMessageService
    {
        public Dictionary<int, IReadOnlyList<MessageThreadSummary>> Threads { get; } = [];
        public MessageThreadDetail? Detail { get; set; }
        public string? PostThrows { get; set; }

        public int? LastListedMemberId { get; private set; }
        public (long ThreadId, int MemberId, long UpTo)? LastMarkRead { get; private set; }
        public string? LastPostBody { get; private set; }
        public string? LastPostAuthorKind { get; private set; }
        public int? LastPostAuthorMemberId { get; private set; }

        public Task<MessageThread> StartThreadAsync(
            string kind, string subject, IReadOnlyCollection<MessageParticipantSpec> participants,
            string? sourceModuleId = null, string? correlationId = null, CancellationToken ct = default)
            => Task.FromResult(new MessageThread());

        public Task<Message> PostAsync(
            long threadId, string body, string authorKind = MessageAuthorKinds.System,
            int? authorMemberId = null, CancellationToken ct = default)
        {
            if (PostThrows is not null)
            {
                throw new MessagingException(PostThrows);
            }

            LastPostBody = body;
            LastPostAuthorKind = authorKind;
            LastPostAuthorMemberId = authorMemberId;
            return Task.FromResult(new Message { Id = 1, ThreadId = threadId, Body = body });
        }

        public Task<IReadOnlyList<MessageThreadSummary>> ListForMemberAsync(
            int memberId, bool includeClosed = false, CancellationToken ct = default)
        {
            LastListedMemberId = memberId;
            return Task.FromResult(Threads.GetValueOrDefault(memberId, []));
        }

        public Task<MessageThreadDetail?> GetThreadAsync(long threadId, int memberId, CancellationToken ct = default)
            => Task.FromResult(Detail);

        public Task<bool> MarkReadAsync(long threadId, int memberId, long upToMessageId, CancellationToken ct = default)
        {
            LastMarkRead = (threadId, memberId, upToMessageId);
            return Task.FromResult(true);
        }

        public Task<int> UnreadCountAsync(int memberId, CancellationToken ct = default) => Task.FromResult(0);

        public Task<bool> CloseThreadAsync(long threadId, CancellationToken ct = default) => Task.FromResult(true);
    }
}
