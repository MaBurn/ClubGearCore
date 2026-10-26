using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Core;

/// <summary>CORE-04. See <see cref="IMessageService"/>.</summary>
public sealed class MessageService : IMessageService
{
    private const int PreviewLength = 160;

    private readonly ApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public MessageService(ApplicationDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<MessageThread> StartThreadAsync(
        string kind,
        string subject,
        IReadOnlyCollection<MessageParticipantSpec> participants,
        string? sourceModuleId = null,
        string? correlationId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(participants);

        var distinct = participants
            .GroupBy(p => p.MemberId)
            // A member listed twice keeps the more permissive entry rather than whichever came last.
            .Select(g => new MessageParticipantSpec(g.Key, g.Any(p => p.CanPost)))
            .ToList();

        if (distinct.Count == 0)
        {
            throw new MessagingException("Ein Thread braucht mindestens einen Teilnehmer.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var thread = new MessageThread
        {
            Kind = kind.Trim(),
            Subject = subject?.Trim() ?? string.Empty,
            SourceModuleId = sourceModuleId,
            CorrelationId = correlationId,
            CreatedAtUtc = now
        };

        _dbContext.MessageThreads.Add(thread);
        await _dbContext.SaveChangesAsync(ct);

        foreach (var spec in distinct)
        {
            _dbContext.MessageParticipants.Add(new MessageParticipant
            {
                ThreadId = thread.Id,
                MemberId = spec.MemberId,
                CanPost = spec.CanPost,
                AddedAtUtc = now
            });
        }

        await _dbContext.SaveChangesAsync(ct);
        return thread;
    }

    public async Task<Message> PostAsync(
        long threadId,
        string body,
        string authorKind = MessageAuthorKinds.System,
        int? authorMemberId = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        var thread = await _dbContext.MessageThreads.FirstOrDefaultAsync(t => t.Id == threadId, ct)
            ?? throw new MessagingException("Thread nicht gefunden.");

        if (thread.IsClosed)
        {
            throw new MessagingException("Der Thread ist geschlossen.");
        }

        if (authorKind == MessageAuthorKinds.Member)
        {
            if (authorMemberId is null)
            {
                throw new MessagingException("Ein Mitgliedsbeitrag braucht eine Mitglieds-Id.");
            }

            var participant = await _dbContext.MessageParticipants
                .FirstOrDefaultAsync(p => p.ThreadId == threadId && p.MemberId == authorMemberId, ct);

            if (participant is null || !participant.CanPost)
            {
                // Covers both "not a participant" and "read-only participant": a notification
                // thread must not become a reply channel just because it is addressed to someone.
                throw new MessagingException("Keine Berechtigung, in diesem Thread zu schreiben.");
            }
        }

        var message = new Message
        {
            ThreadId = threadId,
            AuthorKind = authorKind,
            AuthorMemberId = authorKind == MessageAuthorKinds.Member ? authorMemberId : null,
            Body = body,
            CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
        };

        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync(ct);

        // The author has by definition read their own message.
        if (authorKind == MessageAuthorKinds.Member && authorMemberId is int authorId)
        {
            await MarkReadAsync(threadId, authorId, message.Id, ct);
        }

        return message;
    }

    public async Task<IReadOnlyList<MessageThreadSummary>> ListForMemberAsync(
        int memberId,
        bool includeClosed = false,
        CancellationToken ct = default)
    {
        var participations = await _dbContext.MessageParticipants
            .AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .ToListAsync(ct);

        if (participations.Count == 0)
        {
            return Array.Empty<MessageThreadSummary>();
        }

        var threadIds = participations.Select(p => p.ThreadId).ToList();

        var threads = await _dbContext.MessageThreads
            .AsNoTracking()
            .Where(t => threadIds.Contains(t.Id) && (includeClosed || t.ClosedAtUtc == null))
            .ToListAsync(ct);

        var messages = await _dbContext.Messages
            .AsNoTracking()
            .Where(m => threadIds.Contains(m.ThreadId))
            .ToListAsync(ct);

        var readState = participations.ToDictionary(p => p.ThreadId, p => p.LastReadMessageId);

        var summaries = new List<MessageThreadSummary>(threads.Count);

        foreach (var thread in threads)
        {
            var threadMessages = messages.Where(m => m.ThreadId == thread.Id).ToList();
            var last = threadMessages.OrderByDescending(m => m.Id).FirstOrDefault();

            summaries.Add(new MessageThreadSummary(
                thread,
                CountUnread(threadMessages, readState.GetValueOrDefault(thread.Id), memberId),
                last?.CreatedAtUtc,
                Preview(last?.Body)));
        }

        // Newest activity first; a thread with no message yet falls back to when it was created.
        return summaries
            .OrderByDescending(s => s.LastMessageAtUtc ?? s.Thread.CreatedAtUtc)
            .ToList();
    }

    public async Task<MessageThreadDetail?> GetThreadAsync(long threadId, int memberId, CancellationToken ct = default)
    {
        var participant = await _dbContext.MessageParticipants
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ThreadId == threadId && p.MemberId == memberId, ct);

        if (participant is null)
        {
            return null;
        }

        var thread = await _dbContext.MessageThreads
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == threadId, ct);

        if (thread is null)
        {
            return null;
        }

        var messages = await _dbContext.Messages
            .AsNoTracking()
            .Where(m => m.ThreadId == threadId)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);

        return new MessageThreadDetail(
            thread,
            messages,
            CanPost: participant.CanPost && !thread.IsClosed,
            UnreadCount: CountUnread(messages, participant.LastReadMessageId, memberId));
    }

    public async Task<bool> MarkReadAsync(long threadId, int memberId, long upToMessageId, CancellationToken ct = default)
    {
        var participant = await _dbContext.MessageParticipants
            .FirstOrDefaultAsync(p => p.ThreadId == threadId && p.MemberId == memberId, ct);

        if (participant is null)
        {
            return false;
        }

        // Never move the marker backwards: a member re-opening an older message has not unread
        // everything that came after it.
        if (participant.LastReadMessageId is not null && participant.LastReadMessageId >= upToMessageId)
        {
            return true;
        }

        participant.LastReadMessageId = upToMessageId;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> UnreadCountAsync(int memberId, CancellationToken ct = default)
    {
        var participations = await _dbContext.MessageParticipants
            .AsNoTracking()
            .Where(p => p.MemberId == memberId)
            .ToListAsync(ct);

        if (participations.Count == 0)
        {
            return 0;
        }

        var threadIds = participations.Select(p => p.ThreadId).ToList();

        var openThreadIds = await _dbContext.MessageThreads
            .AsNoTracking()
            .Where(t => threadIds.Contains(t.Id) && t.ClosedAtUtc == null)
            .Select(t => t.Id)
            .ToListAsync(ct);

        var messages = await _dbContext.Messages
            .AsNoTracking()
            .Where(m => openThreadIds.Contains(m.ThreadId))
            .ToListAsync(ct);

        return participations
            .Where(p => openThreadIds.Contains(p.ThreadId))
            .Sum(p => CountUnread(
                messages.Where(m => m.ThreadId == p.ThreadId).ToList(),
                p.LastReadMessageId,
                memberId));
    }

    public async Task<bool> CloseThreadAsync(long threadId, CancellationToken ct = default)
    {
        var thread = await _dbContext.MessageThreads.FirstOrDefaultAsync(t => t.Id == threadId, ct);

        if (thread is null || thread.IsClosed)
        {
            return false;
        }

        thread.ClosedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// A member's own messages never count as unread, so posting into a chat does not raise the
    /// author's own badge.
    /// </summary>
    private static int CountUnread(IReadOnlyList<Message> threadMessages, long? lastReadMessageId, int memberId)
        => threadMessages.Count(m =>
            m.Id > (lastReadMessageId ?? 0)
            && !(m.AuthorKind == MessageAuthorKinds.Member && m.AuthorMemberId == memberId));

    private static string? Preview(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var collapsed = body.ReplaceLineEndings(" ").Trim();
        return collapsed.Length <= PreviewLength ? collapsed : collapsed[..PreviewLength] + "…";
    }
}
