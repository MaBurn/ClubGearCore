using System.Security.Claims;
using ClubGear.Data;
using ClubGear.Data.Migrations;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using ClubGear.Services.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// CORE-09: API keys and service accounts.
///
/// The security-relevant promises are asserted here rather than left to review: the secret is
/// never stored, a key cannot carry the wildcard, and a revoked or expired key stops working.
/// </summary>
public sealed class ApiKeyServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _dbContext;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero));
    private readonly RecordingAuditLog _audit = new();
    private readonly ApiKeyService _sut;

    public ApiKeyServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new ApplicationDbContext(options);
        _dbContext.Database.EnsureCreated();
        AddApiKeys_202608310101.ApplyAsync(_dbContext).GetAwaiter().GetResult();

        _sut = new ApiKeyService(_dbContext, _audit, _time, NullLogger<ApiKeyService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    // ---------------------------------------------------------------------------------------
    // The secret
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_Returns_A_Secret_That_Is_Never_Stored()
    {
        var issued = await _sut.CreateAsync("Mobile Inventur", ["inventory.read"], null, "admin");

        var stored = await _dbContext.ApiKeys.AsNoTracking().SingleAsync();

        Assert.NotEqual(issued.Secret, stored.KeyHash);
        Assert.DoesNotContain(issued.Secret, stored.KeyHash, StringComparison.Ordinal);
        Assert.Equal(ApiKeyService.Hash(issued.Secret), stored.KeyHash);
    }

    [Fact]
    public async Task Create_Stores_Only_The_Non_Secret_Prefix_In_Clear()
    {
        var issued = await _sut.CreateAsync("k", ["members.read"], null, "admin");

        var stored = await _dbContext.ApiKeys.AsNoTracking().SingleAsync();

        Assert.Equal(issued.Secret[..12], stored.KeyPrefix);
        Assert.True(issued.Secret.Length > stored.KeyPrefix.Length + 20,
            "The prefix must be a small fraction of the secret, not most of it.");
    }

    [Fact]
    public void GenerateSecret_Is_Marker_Prefixed_And_Url_Safe()
    {
        var secret = ApiKeyService.GenerateSecret();

        Assert.StartsWith("cgk_", secret, StringComparison.Ordinal);
        Assert.DoesNotContain('+', secret);
        Assert.DoesNotContain('/', secret);
        Assert.DoesNotContain('=', secret);
    }

    [Fact]
    public void GenerateSecret_Does_Not_Repeat()
    {
        var secrets = Enumerable.Range(0, 200).Select(_ => ApiKeyService.GenerateSecret()).ToList();

        Assert.Equal(secrets.Count, secrets.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Audit_Record_Does_Not_Carry_The_Secret()
    {
        var issued = await _sut.CreateAsync("k", ["members.read"], null, "admin");

        var serialised = System.Text.Json.JsonSerializer.Serialize(_audit.Records);

        Assert.DoesNotContain(issued.Secret, serialised, StringComparison.Ordinal);
        Assert.Contains(_audit.Records, r => r.Action == "apikey.created");
    }

    // ---------------------------------------------------------------------------------------
    // Scopes
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Wildcard_Cannot_Be_Granted_To_An_ApiKey()
    {
        var ex = await Assert.ThrowsAsync<ApiKeyValidationException>(
            () => _sut.CreateAsync("god mode", [PermissionKeys.Wildcard], null, "admin"));

        Assert.Contains("*", ex.Message, StringComparison.Ordinal);
        Assert.Empty(await _dbContext.ApiKeys.ToListAsync());
    }

    [Fact]
    public async Task A_Key_Without_Any_Scope_Is_Rejected()
        => await Assert.ThrowsAsync<ApiKeyValidationException>(
            () => _sut.CreateAsync("useless", [], null, "admin"));

    [Fact]
    public async Task Scopes_Are_Deduplicated()
    {
        await _sut.CreateAsync("k", ["members.read", "Members.Read", "members.manage"], null, "admin");

        var stored = await _dbContext.ApiKeys.AsNoTracking().SingleAsync();

        Assert.Equal(2, stored.Scopes.Count);
    }

    [Fact]
    public async Task A_Scope_Containing_A_Comma_Is_Rejected_So_It_Cannot_Smuggle_A_Second_Scope()
        => await Assert.ThrowsAsync<ApiKeyValidationException>(
            () => _sut.CreateAsync("k", ["members.read,apikeys.manage"], null, "admin"));

    // ---------------------------------------------------------------------------------------
    // Authentication
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task A_Valid_Key_Authenticates_And_Carries_Its_Scopes_As_Permission_Claims()
    {
        var issued = await _sut.CreateAsync("k", ["members.read", "inventory.read"], null, "admin");

        var principal = await _sut.AuthenticateAsync(issued.Secret, "ApiKey");

        Assert.NotNull(principal);
        Assert.True(principal!.Identity!.IsAuthenticated);

        var permissions = principal.FindAll("permission").Select(c => c.Value).ToList();
        Assert.Equal(["members.read", "inventory.read"], permissions);
    }

    [Fact]
    public async Task An_Authenticated_Key_Is_Marked_As_A_Service_Account_And_Holds_No_Roles()
    {
        var issued = await _sut.CreateAsync("k", ["members.read"], null, "admin");

        var principal = await _sut.AuthenticateAsync(issued.Secret, "ApiKey");

        Assert.Equal("true", principal!.FindFirst(ApiKeyService.ServiceAccountClaimType)?.Value);
        Assert.Empty(principal.FindAll(ClaimTypes.Role));
    }

    [Fact]
    public async Task An_ApiKey_Never_Receives_The_MasterAdmin_Claim()
    {
        var issued = await _sut.CreateAsync("k", ["members.read"], null, "admin");

        var principal = await _sut.AuthenticateAsync(issued.Secret, "ApiKey");

        Assert.DoesNotContain(principal!.Claims, c =>
            string.Equals(c.Type, "clubgear.system-role", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("cgk_wrong")]
    [InlineData("completely-unrelated-token")]
    public async Task A_Bad_Key_Does_Not_Authenticate(string presented)
        => Assert.Null(await _sut.AuthenticateAsync(presented, "ApiKey"));

    [Fact]
    public async Task A_Key_Differing_Only_After_The_Prefix_Does_Not_Authenticate()
    {
        var issued = await _sut.CreateAsync("k", ["members.read"], null, "admin");

        // Same prefix, different secret: the prefix narrows the lookup but decides nothing.
        var forged = issued.Secret[..12] + new string('A', issued.Secret.Length - 12);

        Assert.Null(await _sut.AuthenticateAsync(forged, "ApiKey"));
    }

    [Fact]
    public async Task A_Revoked_Key_Stops_Working_Immediately()
    {
        var issued = await _sut.CreateAsync("k", ["members.read"], null, "admin");
        Assert.NotNull(await _sut.AuthenticateAsync(issued.Secret, "ApiKey"));

        Assert.True(await _sut.RevokeAsync(issued.Key.Id, "admin"));

        Assert.Null(await _sut.AuthenticateAsync(issued.Secret, "ApiKey"));
    }

    [Fact]
    public async Task Revoking_Twice_Reports_That_Nothing_Happened()
    {
        var issued = await _sut.CreateAsync("k", ["members.read"], null, "admin");

        Assert.True(await _sut.RevokeAsync(issued.Key.Id, "admin"));
        Assert.False(await _sut.RevokeAsync(issued.Key.Id, "admin"));
    }

    [Fact]
    public async Task An_Expired_Key_Stops_Working_When_The_Clock_Passes_Its_Expiry()
    {
        var expiry = _time.GetUtcNow().UtcDateTime.AddHours(1);
        var issued = await _sut.CreateAsync("k", ["members.read"], expiry, "admin");

        Assert.NotNull(await _sut.AuthenticateAsync(issued.Secret, "ApiKey"));

        _time.Advance(TimeSpan.FromHours(2));

        Assert.Null(await _sut.AuthenticateAsync(issued.Secret, "ApiKey"));
    }

    [Fact]
    public async Task An_Expiry_In_The_Past_Is_Rejected_At_Creation()
        => await Assert.ThrowsAsync<ApiKeyValidationException>(
            () => _sut.CreateAsync("k", ["members.read"], _time.GetUtcNow().UtcDateTime.AddMinutes(-1), "admin"));

    [Fact]
    public async Task LastUsed_Is_Recorded_But_Not_Rewritten_On_Every_Request()
    {
        var issued = await _sut.CreateAsync("k", ["members.read"], null, "admin");

        await _sut.AuthenticateAsync(issued.Secret, "ApiKey");
        var first = (await _dbContext.ApiKeys.AsNoTracking().SingleAsync()).LastUsedAtUtc;
        Assert.NotNull(first);

        _time.Advance(TimeSpan.FromSeconds(5));
        await _sut.AuthenticateAsync(issued.Secret, "ApiKey");
        Assert.Equal(first, (await _dbContext.ApiKeys.AsNoTracking().SingleAsync()).LastUsedAtUtc);

        _time.Advance(TimeSpan.FromMinutes(2));
        await _sut.AuthenticateAsync(issued.Secret, "ApiKey");
        Assert.NotEqual(first, (await _dbContext.ApiKeys.AsNoTracking().SingleAsync()).LastUsedAtUtc);
    }

    // ---------------------------------------------------------------------------------------
    // Listing
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task Revoked_Keys_Are_Hidden_From_The_Default_Listing_But_Not_Deleted()
    {
        var a = await _sut.CreateAsync("a", ["members.read"], null, "admin");
        await _sut.CreateAsync("b", ["members.read"], null, "admin");
        await _sut.RevokeAsync(a.Key.Id, "admin");

        Assert.Single(await _sut.ListAsync());
        Assert.Equal(2, (await _sut.ListAsync(includeRevoked: true)).Count);
    }

    // ---------------------------------------------------------------------------------------

    private sealed class RecordingAuditLog : IAuditLogService
    {
        public List<AuditLogRecord> Records { get; } = [];

        public Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }

        public Task LogChangeAsync(
            string action, object? before, object? after, string? actor = null, string? source = null,
            string? targetType = null, string? targetId = null, object? metadata = null,
            CancellationToken cancellationToken = default)
        {
            Records.Add(new AuditLogRecord(action, actor, source, targetType, targetId, before, after, metadata));
            return Task.CompletedTask;
        }
    }
}
