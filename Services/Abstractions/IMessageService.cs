using ClubGear.Models;

namespace ClubGear.Services.Abstractions;

/// <summary>
/// CORE-04. The core's single message and thread substrate.
/// </summary>
/// <remarks>
/// Notifications, member chat and plugin-owned conversations are all threads here. There is one
/// inbox, one unread count and one read state per member — deliberately, so a member never has to
/// look in two places and the core never grows a second messaging system.
/// </remarks>
public interface IMessageService
{
    /// <summary>
    /// Starts a thread. <paramref name="participants"/> decides both who can see it and who may
    /// post: a notification adds its recipient with <c>canPost: false</c>, a chat adds everyone
    /// with <c>canPost: true</c>.
    /// </summary>
    Task<MessageThread> StartThreadAsync(
        string kind,
        string subject,
        IReadOnlyCollection<MessageParticipantSpec> participants,
        string? sourceModuleId = null,
        string? correlationId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Appends a message. A member author must be a participant with <see cref="MessageParticipant.CanPost"/>;
    /// system and service authors bypass that, because a notification thread has no posting members.
    /// </summary>
    Task<Message> PostAsync(
        long threadId,
        string body,
        string authorKind = MessageAuthorKinds.System,
        int? authorMemberId = null,
        CancellationToken ct = default);

    /// <summary>Threads the member participates in, newest activity first, with unread counts.</summary>
    Task<IReadOnlyList<MessageThreadSummary>> ListForMemberAsync(
        int memberId,
        bool includeClosed = false,
        CancellationToken ct = default);

    /// <summary>
    /// The thread and its messages, or <c>null</c> when the member does not participate. Absence
    /// and denial are the same answer on purpose: a non-participant learns nothing about whether a
    /// thread exists.
    /// </summary>
    Task<MessageThreadDetail?> GetThreadAsync(long threadId, int memberId, CancellationToken ct = default);

    /// <summary>Marks everything up to and including <paramref name="upToMessageId"/> as read.</summary>
    Task<bool> MarkReadAsync(long threadId, int memberId, long upToMessageId, CancellationToken ct = default);

    /// <summary>Unread messages across every thread the member participates in.</summary>
    Task<int> UnreadCountAsync(int memberId, CancellationToken ct = default);

    /// <summary>Closes a thread: still readable, accepts no further posts.</summary>
    Task<bool> CloseThreadAsync(long threadId, CancellationToken ct = default);
}

public sealed record MessageParticipantSpec(int MemberId, bool CanPost);

public sealed record MessageThreadSummary(
    MessageThread Thread,
    int UnreadCount,
    DateTime? LastMessageAtUtc,
    string? LastMessagePreview);

public sealed record MessageThreadDetail(
    MessageThread Thread,
    IReadOnlyList<Message> Messages,
    bool CanPost,
    int UnreadCount);

public sealed class MessagingException : Exception
{
    public MessagingException(string message) : base(message)
    {
    }
}
