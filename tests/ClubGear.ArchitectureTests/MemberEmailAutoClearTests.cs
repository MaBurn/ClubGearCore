using System.Security.Claims;
using System.Text;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Core;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 2, checkbox 2.3 DoD: all three write paths that can change Member.Email
/// (admin edit, CSV import update, self-service profile) clear the email-invalid state only
/// when the address really changes — an identical address or a casing-only change keeps the flag.
/// </summary>
public sealed class MemberEmailAutoClearTests
{
    // ── admin edit: MemberFeatureService.UpdateAsync ─────────────────────────

    [Theory]
    [InlineData("erika@example.org", false)]        // identical address: flag kept
    [InlineData("Erika@Example.ORG", false)]        // casing-only change: flag kept
    [InlineData("  erika@example.org  ", false)]    // whitespace-only change: flag kept
    [InlineData("erika.neu@example.org", true)]     // real change: flag cleared
    public async Task UpdateAsync_ClearsInvalidFlagOnlyWhenAddressChanges(string newEmail, bool expectCleared)
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();

        var member = FlaggedMember("M-1", "erika@example.org");
        db.Members.Add(member);
        await db.SaveChangesAsync();

        var service = new MemberFeatureService(db, new NoopAuditLog(), new MemberMetadataService());

        var status = await service.UpdateAsync(new Member
        {
            Id = member.Id,
            MemberNumber = member.MemberNumber,
            FirstName = member.FirstName,
            LastName = member.LastName,
            Email = newEmail,
            IsActive = true,
            JoinedAt = member.JoinedAt
        }, "tester");

        Assert.Equal(MemberMutationStatus.Success, status);

        var persisted = await db.Members.AsNoTracking().SingleAsync();
        AssertInvalidState(persisted, expectCleared);
    }

    // ── CSV import update path: MemberFeatureService.ImportCsvAsync ──────────

    [Theory]
    [InlineData("erika@example.org", false)]
    [InlineData("Erika@Example.ORG", false)]
    [InlineData("erika.neu@example.org", true)]
    public async Task ImportCsvAsync_UpdatePath_ClearsInvalidFlagOnlyWhenAddressChanges(string newEmail, bool expectCleared)
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();

        db.MembershipTypes.Add(new MembershipType { Key = "Standard", Name = "Standard", IsActive = true, IsSystemDefined = true });
        db.Members.Add(FlaggedMember("M-1", "erika@example.org"));
        await db.SaveChangesAsync();

        var service = new MemberFeatureService(db, new NoopAuditLog(), new MemberMetadataService());

        // Fixed legacy column layout: [0]=MemberNumber,[1]=FirstName,[2]=LastName,[3]=Email,
        // [4]=Phone,[5]=IsActive,[6]=JoinedAt,[7]=Title,[8]=Gender,[9]=IsClub,[10]=ClubName,
        // [11]=IsCompany,[12]=CompanyName.
        var row = string.Join(";", new[]
        {
            "M-1", "Erika", "Muster", newEmail, "0170-0000000", "true", "2020-01-01",
            string.Empty, string.Empty, "false", string.Empty, "false", string.Empty
        });

        var result = await service.ImportCsvAsync(new MemoryStream(Encoding.UTF8.GetBytes(row)), "tester");

        Assert.Empty(result.Errors);
        Assert.Equal(1, result.Updated);

        var persisted = await db.Members.AsNoTracking().SingleAsync();
        AssertInvalidState(persisted, expectCleared);
    }

    // ── self-service profile: SelfServiceFeatureService.UpdateProfileAsync ───

    [Theory]
    [InlineData("erika@example.org", false)]
    [InlineData("Erika@Example.ORG", false)]
    [InlineData("erika.neu@example.org", true)]
    public async Task UpdateProfileAsync_ClearsInvalidFlagOnlyWhenAddressChanges(string newEmail, bool expectCleared)
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync();

        var user = new ApplicationUser
        {
            Id = "user-1",
            FullName = "Erika Muster",
            Email = "erika@example.org",
            UserName = "erika@example.org",
            PhoneNumber = "111"
        };
        db.Users.Add(user);

        var member = FlaggedMember("M-1", "erika@example.org");
        member.ApplicationUserId = user.Id;
        db.Members.Add(member);
        await db.SaveChangesAsync();

        using var store = new InMemoryUserStore(user);
        using var userManager = CreateUserManager(store);
        var service = new SelfServiceFeatureService(
            db,
            userManager,
            new NoopAuditLog(),
            new NoopNotificationService(),
            new NoopMessageComposer(),
            new NoopProfileImageStorageService());

        var outcome = await service.UpdateProfileAsync(
            CreatePrincipal(user.Id),
            new SelfServiceProfileViewModel
            {
                FullName = "Erika Muster",
                Email = newEmail,
                PhoneNumber = "111"
            });

        Assert.True(outcome.Succeeded);

        var persisted = await db.Members.AsNoTracking().SingleAsync();
        AssertInvalidState(persisted, expectCleared);
    }

    // ── the helper itself ───────────────────────────────────────────────────

    [Fact]
    public void ResetInvalidOnChange_OnUnflaggedMember_ChangesNothing()
    {
        var member = new Member { Email = "erika@example.org" };

        Assert.False(MemberEmailInvalidationService.ResetInvalidOnChange(member, "neu@example.org"));
        Assert.False(member.EmailInvalid);
        Assert.Null(member.EmailInvalidatedAtUtc);
        Assert.Null(member.EmailInvalidReason);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static void AssertInvalidState(Member persisted, bool expectCleared)
    {
        if (expectCleared)
        {
            Assert.False(persisted.EmailInvalid);
            Assert.Null(persisted.EmailInvalidatedAtUtc);
            Assert.Null(persisted.EmailInvalidReason);
        }
        else
        {
            Assert.True(persisted.EmailInvalid);
            Assert.NotNull(persisted.EmailInvalidatedAtUtc);
            Assert.Equal("550 5.1.1 unknown mailbox", persisted.EmailInvalidReason);
        }
    }

    private static ApplicationDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Member FlaggedMember(string number, string email) => new()
    {
        MemberNumber = number,
        FirstName = "Erika",
        LastName = "Muster",
        Email = email,
        PhoneNumber = "111",
        IsActive = true,
        JoinedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EmailInvalid = true,
        EmailInvalidatedAtUtc = new DateTime(2026, 7, 24, 10, 0, 0, DateTimeKind.Utc),
        EmailInvalidReason = "550 5.1.1 unknown mailbox"
    };

    private static ClaimsPrincipal CreatePrincipal(string userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId) },
            "TestAuth"));
    }

    private static UserManager<ApplicationUser> CreateUserManager(InMemoryUserStore store)
    {
        return new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    private sealed class InMemoryUserStore : IUserStore<ApplicationUser>
    {
        public InMemoryUserStore(ApplicationUser user)
        {
            User = user;
        }

        public ApplicationUser? User { get; private set; }

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            User = user;
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            User = user;
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            User = null;
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
        {
            return Task.FromResult(User is not null && User.Id == userId ? User : null);
        }

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
        {
            return Task.FromResult(User);
        }

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.Id);
        }

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.UserName);
        }

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
        {
            return Task.FromResult(user.UserName?.ToUpperInvariant());
        }

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    private sealed class NoopAuditLog : IAuditLogService
    {
        public Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LogChangeAsync(
            string action,
            object? before,
            object? after,
            string? actor = null,
            string? source = null,
            string? targetType = null,
            string? targetId = null,
            object? metadata = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoopNotificationService : INotificationService
    {
        public Task<NotificationResult> NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new NotificationResult(true, message.Channel, message.Recipient));
        }
    }

    private sealed class NoopMessageComposer : IMessageComposer
    {
        public (string Subject, string Body) Compose(string subjectTemplate, string bodyTemplate, IReadOnlyDictionary<string, string>? values = null)
        {
            return (subjectTemplate, bodyTemplate);
        }
    }

    private sealed class NoopProfileImageStorageService : IProfileImageStorageService
    {
        public Task<string> SaveProfileImageAsync(
            int memberId,
            string extension,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"/uploads/profile-images/member-{memberId}-test{extension}");
        }

        public Task DeleteProfileImageAsync(string? imagePath, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
