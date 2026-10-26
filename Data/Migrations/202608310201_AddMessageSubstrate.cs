using Microsoft.EntityFrameworkCore;

namespace ClubGear.Data.Migrations;

/// <summary>
/// CORE-04. Creates the core message substrate: threads, messages and per-member participation.
/// Notifications, member chat and plugin-owned conversations all live here, which is why there is
/// no separate notification-inbox table. Idempotent raw SQL; nothing is dropped or backfilled.
/// </summary>
public static class AddMessageSubstrate_202608310201
{
    public static async Task ApplyAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS MessageThreads (
                Id INTEGER NOT NULL CONSTRAINT PK_MessageThreads PRIMARY KEY AUTOINCREMENT,
                Kind TEXT NOT NULL DEFAULT 'notification',
                Subject TEXT NOT NULL DEFAULT '',
                SourceModuleId TEXT NULL,
                CorrelationId TEXT NULL,
                CreatedAtUtc TEXT NOT NULL,
                ClosedAtUtc TEXT NULL
            );",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS Messages (
                Id INTEGER NOT NULL CONSTRAINT PK_Messages PRIMARY KEY AUTOINCREMENT,
                ThreadId INTEGER NOT NULL,
                AuthorKind TEXT NOT NULL DEFAULT 'system',
                AuthorMemberId INTEGER NULL,
                Body TEXT NOT NULL DEFAULT '',
                CreatedAtUtc TEXT NOT NULL
            );",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            @"CREATE TABLE IF NOT EXISTS MessageParticipants (
                Id INTEGER NOT NULL CONSTRAINT PK_MessageParticipants PRIMARY KEY AUTOINCREMENT,
                ThreadId INTEGER NOT NULL,
                MemberId INTEGER NOT NULL,
                CanPost INTEGER NOT NULL DEFAULT 0,
                LastReadMessageId INTEGER NULL,
                AddedAtUtc TEXT NOT NULL
            );",
            cancellationToken);

        // Reading a thread and counting unread both start from these.
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_Messages_ThreadId ON Messages (ThreadId);",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_MessageParticipants_MemberId ON MessageParticipants (MemberId);",
            cancellationToken);

        // A member participates in a thread at most once; the unique index makes that a database
        // guarantee rather than something the service has to remember.
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS UX_MessageParticipants_Thread_Member ON MessageParticipants (ThreadId, MemberId);",
            cancellationToken);
    }
}
