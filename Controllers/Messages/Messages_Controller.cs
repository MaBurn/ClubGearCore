using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.Controllers;

/// <summary>
/// CORE-04 slice 2. The member-facing inbox over the core message substrate.
/// </summary>
/// <remarks>
/// Guarded by <see cref="AuthorizeAttribute"/> alone, deliberately without a permission check.
/// Access is decided by participation inside <see cref="IMessageService"/>: a member may read the
/// threads they are part of and nothing else. Gating the inbox behind a grantable permission would
/// mean an administrator could leave a member unable to read messages addressed to them, and would
/// grow the permission surface without protecting anything.
/// </remarks>
[Authorize]
[Route("messages")]
public sealed class MessagesController : Controller
{
    private readonly IMessageService _messageService;
    private readonly ICurrentMemberAccessor _currentMember;

    public MessagesController(IMessageService messageService, ICurrentMemberAccessor currentMember)
    {
        _messageService = messageService;
        _currentMember = currentMember;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(bool includeClosed = false, CancellationToken ct = default)
    {
        var memberId = await _currentMember.GetMemberIdAsync(User, ct);

        if (memberId is null)
        {
            // A login without a member record has no inbox. That is a state to explain, not a 403.
            return View(new MessagesIndexViewModel([], includeClosed, MemberLinked: false));
        }

        var threads = await _messageService.ListForMemberAsync(memberId.Value, includeClosed, ct);
        return View(new MessagesIndexViewModel(threads, includeClosed, MemberLinked: true));
    }

    [HttpGet("{threadId:long}")]
    public async Task<IActionResult> Detail(long threadId, CancellationToken ct = default)
    {
        var memberId = await _currentMember.GetMemberIdAsync(User, ct);
        if (memberId is null)
        {
            return NotFound();
        }

        var detail = await _messageService.GetThreadAsync(threadId, memberId.Value, ct);
        if (detail is null)
        {
            // Null covers both "does not exist" and "not yours"; keep them indistinguishable here too.
            return NotFound();
        }

        // Opening a thread is what marks it read.
        if (detail.Messages.Count > 0)
        {
            await _messageService.MarkReadAsync(threadId, memberId.Value, detail.Messages[^1].Id, ct);
        }

        return View(new MessageThreadViewModel(detail, memberId.Value));
    }

    [HttpPost("{threadId:long}/reply")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(long threadId, string body, CancellationToken ct = default)
    {
        var memberId = await _currentMember.GetMemberIdAsync(User, ct);
        if (memberId is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["MessageError"] = "Die Nachricht darf nicht leer sein.";
            return RedirectToAction(nameof(Detail), new { threadId });
        }

        try
        {
            await _messageService.PostAsync(threadId, body.Trim(), MessageAuthorKinds.Member, memberId.Value, ct);
        }
        catch (MessagingException ex)
        {
            TempData["MessageError"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { threadId });
    }
}

public sealed record MessagesIndexViewModel(
    IReadOnlyList<MessageThreadSummary> Threads,
    bool IncludeClosed,
    bool MemberLinked);

public sealed record MessageThreadViewModel(MessageThreadDetail Detail, int CurrentMemberId);
