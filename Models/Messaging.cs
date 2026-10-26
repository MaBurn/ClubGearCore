namespace ClubGear.Models;

/// <summary>
/// Well-known thread kinds. The set is open — a plugin may define its own — but these two are the
/// reason the substrate exists: a notification and a chat are the same structure seen from two
/// directions, so the core stores them once.
/// </summary>
public static class MessageThreadKinds
{
    /// <summary>System-to-member. The member reads; only the system posts.</summary>
    public const string Notification = "notification";

    /// <summary>Member-to-member. Every participant may post.</summary>
    public const string Chat = "chat";
}

public static class MessageAuthorKinds
{
    public const string System = "system";
    public const string Member = "member";
    public const string Service = "service";
}

/// <summary>
/// CORE-04. One conversation. A notification is a thread the system posts to and a member reads;
/// a chat is a thread its participants post to. A plugin attaches its own conversation — a rental
/// negotiation, say — by starting a thread with its module id and the business key in
/// <see cref="CorrelationId"/>, rather than building a second messaging system.
/// </summary>
public class MessageThread
{
    public long Id { get; set; }

    /// <summary>See <see cref="MessageThreadKinds"/>. Free-form so plugins can add their own.</summary>
    public string Kind { get; set; } = MessageThreadKinds.Notification;

    public string Subject { get; set; } = string.Empty;

    /// <summary><c>null</c> for the core; otherwise the plugin module id that owns the thread.</summary>
    public string? SourceModuleId { get; set; }

    /// <summary>The owner's business key, e.g. a rental id. Opaque to the core.</summary>
    public string? CorrelationId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>A closed thread stays readable but accepts no further posts.</summary>
    public DateTime? ClosedAtUtc { get; set; }

    public bool IsClosed => ClosedAtUtc is not null;
}

public class Message
{
    public long Id { get; set; }

    public long ThreadId { get; set; }

    /// <summary>See <see cref="MessageAuthorKinds"/>.</summary>
    public string AuthorKind { get; set; } = MessageAuthorKinds.System;

    /// <summary><c>null</c> for system and service authors.</summary>
    public int? AuthorMemberId { get; set; }

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A member's membership in a thread. Participation is the access rule — a member who is not a
/// participant cannot read the thread — and carries the per-member read state.
/// </summary>
public class MessageParticipant
{
    public long Id { get; set; }

    public long ThreadId { get; set; }

    public int MemberId { get; set; }

    /// <summary>
    /// False for notification threads: the member reads but does not reply. True for chats.
    /// </summary>
    public bool CanPost { get; set; }

    /// <summary>Highest message id this member has read. <c>null</c> means nothing read yet.</summary>
    public long? LastReadMessageId { get; set; }

    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}
