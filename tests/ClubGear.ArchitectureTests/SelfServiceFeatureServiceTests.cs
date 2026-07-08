using System.Security.Claims;
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

public sealed class SelfServiceFeatureServiceTests
{
    [Fact]
    public async Task GetProfileAndUpdateProfile_SyncLinkedMemberData()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var user = new ApplicationUser
        {
            Id = "user-1",
            FullName = "Berta Beispiel",
            Email = "berta.old@example.org",
            UserName = "berta.old@example.org",
            PhoneNumber = "111"
        };

        db.Users.Add(user);
        db.Members.Add(new Member
        {
            MemberNumber = "M-2000",
            FirstName = "Berta",
            LastName = "Beispiel",
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsActive = true,
            JoinedAt = DateTime.UtcNow.AddDays(-20),
            ApplicationUserId = user.Id,
            NotifyViaEmail = true,
            NotifyViaMatrix = false,
            DataprivacyAccepted = true
        });
        await db.SaveChangesAsync();

        using var store = new InMemoryUserStore(user);
        using var userManager = CreateUserManager(store);
        var imageStorage = new NoopProfileImageStorageService();
        var service = new SelfServiceFeatureService(
            db,
            userManager,
            new NoopAuditLogService(),
            new NoopNotificationService(),
            new NoopMessageComposer(),
            imageStorage);
        var principal = CreatePrincipal(user.Id);

        var profileOutcome = await service.GetProfileAsync(principal);

        Assert.False(profileOutcome.RequiresChallenge);
        Assert.NotNull(profileOutcome.Profile);
        Assert.True(profileOutcome.Profile!.MemberLinked);
        Assert.Equal("M-2000", profileOutcome.Profile.MemberNumber);
        Assert.True(profileOutcome.Profile.MemberActive);
        Assert.Equal("berta.old@example.org", profileOutcome.Profile.Email);
        Assert.Equal("111", profileOutcome.Profile.PhoneNumber);

        var updateOutcome = await service.UpdateProfileAsync(
            principal,
            new SelfServiceProfileViewModel
            {
                FullName = "Berta Neu",
                Email = "berta.new@example.org",
                PhoneNumber = "222"
            });

        Assert.False(updateOutcome.RequiresChallenge);
        Assert.True(updateOutcome.Succeeded);

        var persistedMember = await db.Members.AsNoTracking().SingleAsync();
        Assert.Equal("berta.new@example.org", persistedMember.Email);
        Assert.Equal("222", persistedMember.PhoneNumber);
        Assert.NotNull(persistedMember.LastUpdated);

        Assert.Equal("Berta Neu", store.User!.FullName);
        Assert.Equal("berta.new@example.org", store.User.Email);
        Assert.Equal("berta.new@example.org", store.User.UserName);
        Assert.Equal("222", store.User.PhoneNumber);
    }

    [Fact]
    public async Task UploadProfileImage_StoresImageAndUpdatesMemberPath()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var user = new ApplicationUser
        {
            Id = "user-2",
            FullName = "Max Muster",
            Email = "max@example.org",
            UserName = "max@example.org"
        };

        db.Users.Add(user);
        db.Members.Add(new Member
        {
            MemberNumber = "M-3000",
            FirstName = "Max",
            LastName = "Muster",
            Email = user.Email,
            IsActive = true,
            JoinedAt = DateTime.UtcNow.AddDays(-1),
            ApplicationUserId = user.Id
        });
        await db.SaveChangesAsync();

        using var store = new InMemoryUserStore(user);
        using var userManager = CreateUserManager(store);
        var imageStorage = new NoopProfileImageStorageService();
        var service = new SelfServiceFeatureService(
            db,
            userManager,
            new NoopAuditLogService(),
            new NoopNotificationService(),
            new NoopMessageComposer(),
            imageStorage);

        var principal = CreatePrincipal(user.Id);
        await using var imageStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        var outcome = await service.UploadProfileImageAsync(
            principal,
            "avatar.png",
            "image/png",
            imageStream);

        Assert.False(outcome.RequiresChallenge);
        Assert.True(outcome.Succeeded);
        Assert.NotNull(outcome.ImagePath);

        var member = await db.Members.AsNoTracking().SingleAsync();
        Assert.Equal(outcome.ImagePath, member.ProfileImagePath);
        Assert.Contains("member-", member.ProfileImagePath!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadProfileImage_AutoLinksMemberByEmail_WhenLegacyLinkMissing()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var user = new ApplicationUser
        {
            Id = "user-3",
            FullName = "Nora Neu",
            Email = "nora@example.org",
            UserName = "nora@example.org"
        };

        db.Users.Add(user);
        db.Members.Add(new Member
        {
            MemberNumber = "M-3001",
            FirstName = "Nora",
            LastName = "Neu",
            Email = "nora@example.org",
            IsActive = true,
            JoinedAt = DateTime.UtcNow.AddDays(-5),
            ApplicationUserId = null
        });
        await db.SaveChangesAsync();

        using var store = new InMemoryUserStore(user);
        using var userManager = CreateUserManager(store);
        var imageStorage = new NoopProfileImageStorageService();
        var service = new SelfServiceFeatureService(
            db,
            userManager,
            new NoopAuditLogService(),
            new NoopNotificationService(),
            new NoopMessageComposer(),
            imageStorage);

        var principal = CreatePrincipal(user.Id);
        await using var imageStream = new MemoryStream(new byte[] { 7, 8, 9 });

        var outcome = await service.UploadProfileImageAsync(
            principal,
            "avatar.jpg",
            "image/jpeg",
            imageStream);

        Assert.True(outcome.Succeeded);

        var member = await db.Members.AsNoTracking().SingleAsync();
        Assert.Equal(user.Id, member.ApplicationUserId);
        Assert.NotNull(member.ProfileImagePath);
    }

    [Fact]
    public async Task UploadProfileImage_AutoLinksSingleLegacyMember_WhenOnlyOneUserAndOneMemberExist()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var user = new ApplicationUser
        {
            Id = "user-4",
            FullName = "Test User",
            Email = "test@clubgear.local",
            UserName = "test@clubgear.local"
        };

        db.Users.Add(user);
        db.Members.Add(new Member
        {
            MemberNumber = "M-4001",
            FirstName = "Demo",
            LastName = "Member",
            Email = "demo.member@clubgear.local",
            IsActive = true,
            JoinedAt = DateTime.UtcNow.AddDays(-2),
            ApplicationUserId = null
        });
        await db.SaveChangesAsync();

        using var store = new InMemoryUserStore(user);
        using var userManager = CreateUserManager(store);
        var imageStorage = new NoopProfileImageStorageService();
        var service = new SelfServiceFeatureService(
            db,
            userManager,
            new NoopAuditLogService(),
            new NoopNotificationService(),
            new NoopMessageComposer(),
            imageStorage);

        var principal = CreatePrincipal(user.Id);
        await using var imageStream = new MemoryStream(new byte[] { 1, 2, 3 });

        var outcome = await service.UploadProfileImageAsync(
            principal,
            "avatar.webp",
            "image/webp",
            imageStream);

        Assert.True(outcome.Succeeded);

        var member = await db.Members.AsNoTracking().SingleAsync();
        Assert.Equal(user.Id, member.ApplicationUserId);
        Assert.NotNull(member.ProfileImagePath);
    }

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

    private sealed class NoopAuditLogService : IAuditLogService
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
