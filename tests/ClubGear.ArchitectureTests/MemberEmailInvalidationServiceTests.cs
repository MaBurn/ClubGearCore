using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 2, checkbox 2.2 DoD: MemberEmailInvalidationService matches members case-insensitively,
/// is idempotent for an already-flagged member, and writes both the audit entry and the
/// "Bounced" notification record.
/// </summary>
public sealed class MemberEmailInvalidationServiceTests
{
    [Fact]
    public async Task MarkInvalidByEmailAsync_MatchesCaseInsensitively_AndFlagsAllMatchingMembers()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();

        db.Members.AddRange(
            NewMember("M-1", "Erika", "Muster", "Erika.Muster@Example.ORG"),
            NewMember("M-2", "Max", "Muster", "erika.muster@example.org"),
            NewMember("M-3", "Other", "Person", "other@example.org"));
        await db.SaveChangesAsync();

        var audit = new CapturingAuditLog();
        var service = CreateService(db, audit);

        var flagged = await service.MarkInvalidByEmailAsync("  ERIKA.MUSTER@example.org  ", "550 5.1.1 unknown mailbox");

        Assert.Equal(2, flagged);

        var members = await db.Members.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
        Assert.True(members[0].EmailInvalid);
        Assert.True(members[1].EmailInvalid);
        Assert.False(members[2].EmailInvalid);

        Assert.NotNull(members[0].EmailInvalidatedAtUtc);
        Assert.Equal("550 5.1.1 unknown mailbox", members[0].EmailInvalidReason);
        Assert.Null(members[2].EmailInvalidatedAtUtc);
        Assert.Null(members[2].EmailInvalidReason);
    }

    [Fact]
    public async Task MarkInvalidByEmailAsync_ForAlreadyFlaggedMember_IsIdempotent()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();

        var firstFlaggedAt = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var member = NewMember("M-1", "Erika", "Muster", "erika@example.org");
        member.EmailInvalid = true;
        member.EmailInvalidatedAtUtc = firstFlaggedAt;
        member.EmailInvalidReason = "erster Bounce";
        db.Members.Add(member);
        await db.SaveChangesAsync();

        var audit = new CapturingAuditLog();
        var service = CreateService(db, audit);

        var flagged = await service.MarkInvalidByEmailAsync("erika@example.org", "zweiter Bounce");

        Assert.Equal(0, flagged);
        Assert.Empty(audit.Records);

        var persisted = await db.Members.AsNoTracking().SingleAsync();
        Assert.True(persisted.EmailInvalid);
        Assert.Equal(firstFlaggedAt, persisted.EmailInvalidatedAtUtc);
        Assert.Equal("erster Bounce", persisted.EmailInvalidReason);
    }

    [Fact]
    public async Task MarkInvalidByEmailAsync_WritesAuditEntryAndBouncedNotificationRecord()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();

        db.Members.Add(NewMember("M-1", "Erika", "Muster", "erika@example.org"));
        await db.SaveChangesAsync();

        var audit = new CapturingAuditLog();
        var service = CreateService(db, audit);

        await service.MarkInvalidByEmailAsync("erika@example.org", "550 5.1.1 unknown mailbox");

        var auditRecord = Assert.Single(audit.Records);
        Assert.Equal("member.email.invalidated", auditRecord.Action);
        Assert.Equal(nameof(Member), auditRecord.TargetType);

        var notification = await db.NotificationRecords.AsNoTracking().SingleAsync();
        Assert.Equal("Bounced", notification.Status);
        Assert.Equal("erika@example.org", notification.Recipient);
        Assert.Equal("email", notification.Channel);
        Assert.Contains("550 5.1.1", notification.Error);
    }

    [Fact]
    public async Task MarkInvalidByEmailAsync_WithUnknownAddress_FlagsNobodyButStillRecordsTheBounce()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();

        db.Members.Add(NewMember("M-1", "Erika", "Muster", "erika@example.org"));
        await db.SaveChangesAsync();

        var audit = new CapturingAuditLog();
        var service = CreateService(db, audit);

        var flagged = await service.MarkInvalidByEmailAsync("stranger@example.org", null);

        Assert.Equal(0, flagged);
        Assert.Empty(audit.Records);
        var untouched = await db.Members.AsNoTracking().SingleAsync();
        Assert.False(untouched.EmailInvalid);

        var notification = await db.NotificationRecords.AsNoTracking().SingleAsync();
        Assert.Equal("Bounced", notification.Status);
        Assert.Equal("stranger@example.org", notification.Recipient);
    }

    [Fact]
    public async Task MarkInvalidByEmailAsync_WithBlankAddress_DoesNothing()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();

        var audit = new CapturingAuditLog();
        var service = CreateService(db, audit);

        var flagged = await service.MarkInvalidByEmailAsync("   ", "irrelevant");

        Assert.Equal(0, flagged);
        Assert.Empty(audit.Records);
        Assert.Empty(await db.NotificationRecords.AsNoTracking().ToListAsync());
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static ApplicationDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static MemberEmailInvalidationService CreateService(ApplicationDbContext db, IAuditLogService audit)
        => new(db, audit, NullLogger<MemberEmailInvalidationService>.Instance);

    private static Member NewMember(string number, string firstName, string lastName, string email) => new()
    {
        MemberNumber = number,
        FirstName = firstName,
        LastName = lastName,
        Email = email,
        IsActive = true,
        JoinedAt = DateTime.UtcNow
    };

    private sealed class CapturingAuditLog : IAuditLogService
    {
        public List<AuditLogRecord> Records { get; } = new();

        public Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }

        public Task LogChangeAsync(
            string action,
            object? before,
            object? after,
            string? actor = null,
            string? source = null,
            string? targetType = null,
            string? targetId = null,
            object? metadata = null,
            CancellationToken cancellationToken = default)
        {
            Records.Add(new AuditLogRecord(action, actor, source, targetType, targetId, before, after, metadata));
            return Task.CompletedTask;
        }
    }
}
