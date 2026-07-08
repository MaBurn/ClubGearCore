using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 2: a blank-MemberNumber Create whose auto-generated candidate collides with a number
/// another concurrent Create just committed must retry with a freshly generated candidate instead
/// of surfacing a raw duplicate-key <see cref="DbUpdateException"/>, and must give up with a clear,
/// German, user-facing <see cref="BusinessLogicException"/> once every retry attempt is exhausted.
/// </summary>
public sealed class MemberCreateConcurrentDuplicateTests
{
    private const string ExpectedExhaustionMessage =
        "Es konnte keine eindeutige Mitgliedsnummer automatisch vergeben werden. Bitte erneut speichern.";

    [Fact]
    public async Task CreateAsync_WhenEveryGeneratedCandidateCollides_ThrowsBusinessLogicExceptionWithGermanMessage()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var seedDb = new ApplicationDbContext(options))
        {
            await seedDb.Database.EnsureCreatedAsync();
            seedDb.SystemConfigEntries.AddRange(
                new SystemConfigEntry { Section = "Members", Name = "MemberNumberPrefix", Value = "M-", Description = string.Empty, UpdatedAtUtc = DateTime.UtcNow },
                new SystemConfigEntry { Section = "Members", Name = "MemberNumberNextNumber", Value = "1", Description = string.Empty, UpdatedAtUtc = DateTime.UtcNow },
                new SystemConfigEntry { Section = "Members", Name = "MemberNumberPadding", Value = "4", Description = string.Empty, UpdatedAtUtc = DateTime.UtcNow });
            await seedDb.SaveChangesAsync();
        }

        // Always inject a colliding "concurrent" Member row (via an independent DbContext sharing
        // the same connection) right before every SaveChangesAsync attempt, so every one of
        // MaxMemberNumberGenerationAttempts (3) generated candidates loses the race.
        await using var db = new ConflictInjectingDbContext(options, alwaysConflict: true);

        var service = new MemberFeatureService(db, new NoopAuditLogService());

        var member = new Member
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            MemberNumber = string.Empty
        };

        var exception = await Assert.ThrowsAsync<BusinessLogicException>(() => service.CreateAsync(member, actor: "tester"));

        Assert.Equal(ExpectedExhaustionMessage, exception.Message);

        // The colliding rows planted by the injector (one per attempt) exist, but the member being
        // created itself must NOT have been persisted.
        var lovelaceCount = await db.Members.CountAsync(m => m.LastName == "Lovelace");
        Assert.Equal(0, lovelaceCount);
    }

    [Fact]
    public async Task CreateAsync_WhenFirstCandidateCollidesOnce_RetriesAndPersistsDistinctMemberNumber()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var seedDb = new ApplicationDbContext(options))
        {
            await seedDb.Database.EnsureCreatedAsync();
            seedDb.SystemConfigEntries.AddRange(
                new SystemConfigEntry { Section = "Members", Name = "MemberNumberPrefix", Value = "M-", Description = string.Empty, UpdatedAtUtc = DateTime.UtcNow },
                new SystemConfigEntry { Section = "Members", Name = "MemberNumberNextNumber", Value = "1", Description = string.Empty, UpdatedAtUtc = DateTime.UtcNow },
                new SystemConfigEntry { Section = "Members", Name = "MemberNumberPadding", Value = "4", Description = string.Empty, UpdatedAtUtc = DateTime.UtcNow });
            await seedDb.SaveChangesAsync();
        }

        // Simulates two overlapping blank-MemberNumber CreateAsync calls: on the first
        // SaveChangesAsync attempt only, a "concurrent" Create commits the same generated
        // candidate first (via an independent DbContext instance on the same connection),
        // forcing this attempt's own SaveChangesAsync to fail on the unique MemberNumber index.
        await using var db = new ConflictInjectingDbContext(options, alwaysConflict: false);

        var service = new MemberFeatureService(db, new NoopAuditLogService());

        var member = new Member
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            MemberNumber = string.Empty
        };

        await service.CreateAsync(member, actor: "tester");

        Assert.Equal(1, db.ConflictsInjected);

        var persisted = await db.Members.AsNoTracking().SingleAsync(m => m.LastName == "Lovelace");
        Assert.False(string.IsNullOrWhiteSpace(persisted.MemberNumber));

        var concurrentRacer = await db.Members.AsNoTracking().SingleAsync(m => m.LastName == "Racer");
        Assert.NotEqual(concurrentRacer.MemberNumber, persisted.MemberNumber);
    }

    /// <summary>
    /// Injects a colliding "concurrent" Member row (same MemberNumber the entity currently staged
    /// for insert would use) immediately before delegating to the real <c>SaveChangesAsync</c>,
    /// via a second, independent <see cref="ApplicationDbContext"/> sharing the same underlying
    /// connection - exactly mirroring how a second, overlapping web request's own scoped
    /// DbContext would race to commit first in production.
    /// </summary>
    private sealed class ConflictInjectingDbContext : ApplicationDbContext
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;
        private readonly bool _alwaysConflict;

        public ConflictInjectingDbContext(DbContextOptions<ApplicationDbContext> options, bool alwaysConflict)
            : base(options)
        {
            _options = options;
            _alwaysConflict = alwaysConflict;
        }

        public int ConflictsInjected { get; private set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var addedMember = ChangeTracker.Entries<Member>()
                .FirstOrDefault(e => e.State == EntityState.Added)?.Entity;

            var shouldInject = addedMember is not null
                && !string.IsNullOrWhiteSpace(addedMember.MemberNumber)
                && (_alwaysConflict || ConflictsInjected == 0);

            if (shouldInject)
            {
                await using var racerDb = new ApplicationDbContext(_options);
                racerDb.Members.Add(new Member
                {
                    FirstName = "Concurrent",
                    LastName = "Racer",
                    MemberNumber = addedMember!.MemberNumber
                });
                await racerDb.SaveChangesAsync(cancellationToken);
                ConflictsInjected++;
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class NoopAuditLogService : IAuditLogService
    {
        public Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
        {
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
            return Task.CompletedTask;
        }
    }
}
