using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.ExternalLogin;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Unit tests for <see cref="ExternalLoginService"/>.
/// Uses an in-memory SQLite database for the member lookup and stub
/// implementations for <see cref="IExternalLoginConfigService"/>,
/// <see cref="IIdpClaimsEnricher"/>, and <see cref="IExternalLoginInfoProvider"/>.
/// </summary>
public sealed class ExternalLoginServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;

    public ExternalLoginServiceTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new ApplicationDbContext(options);
        _db.Database.EnsureCreated();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    // ------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------

    private ExternalLoginService BuildService(
        IExternalLoginConfigService configService,
        IIdpClaimsEnricher claimsEnricher,
        IExternalLoginInfoProvider loginInfoProvider)
        => new ExternalLoginService(
            configService,
            claimsEnricher,
            loginInfoProvider,
            _db,
            NullLogger<ExternalLoginService>.Instance);

    // ------------------------------------------------------------------
    // GetActiveProvidersAsync tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetActiveProvidersAsync_ReturnsOnlyEnabledProviders()
    {
        var providers = new List<ExternalProviderInfo>
        {
            new ExternalProviderInfo("provider.a", "Provider A", "module.a", IsEnabled: true),
            new ExternalProviderInfo("provider.b", "Provider B", "module.b", IsEnabled: false),
        };

        var configService     = new StubExternalLoginConfigService(providers: providers);
        var claimsEnricher    = new StubIdpClaimsEnricher();
        var loginInfoProvider = new StubExternalLoginInfoProvider(info: null);

        var svc    = BuildService(configService, claimsEnricher, loginInfoProvider);
        var active = await svc.GetActiveProvidersAsync();

        Assert.Single(active);
        Assert.Equal("provider.a", active[0].ProviderKey);
    }

    [Fact]
    public async Task GetActiveProvidersAsync_ReturnsEmpty_WhenNoProviders()
    {
        var configService     = new StubExternalLoginConfigService(providers: []);
        var claimsEnricher    = new StubIdpClaimsEnricher();
        var loginInfoProvider = new StubExternalLoginInfoProvider(info: null);

        var svc    = BuildService(configService, claimsEnricher, loginInfoProvider);
        var active = await svc.GetActiveProvidersAsync();

        Assert.Empty(active);
    }

    // ------------------------------------------------------------------
    // BuildChallengeAsync tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuildChallengeAsync_ReturnsNull_WhenProviderUnknown()
    {
        var configService     = new StubExternalLoginConfigService(providers: []);
        var claimsEnricher    = new StubIdpClaimsEnricher();
        var loginInfoProvider = new StubExternalLoginInfoProvider(info: null);

        var svc    = BuildService(configService, claimsEnricher, loginInfoProvider);
        var result = await svc.BuildChallengeAsync("unknown.provider", "/callback");

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildChallengeAsync_ReturnsNull_WhenConfigIncomplete()
    {
        var providers = new List<ExternalProviderInfo>
        {
            new ExternalProviderInfo("provider.a", "Provider A", "module.a", IsEnabled: true),
        };
        // No config entries — authority/clientid/clientsecret all missing.
        var configService     = new StubExternalLoginConfigService(providers: providers);
        var claimsEnricher    = new StubIdpClaimsEnricher();
        var loginInfoProvider = new StubExternalLoginInfoProvider(info: null);

        var svc    = BuildService(configService, claimsEnricher, loginInfoProvider);
        var result = await svc.BuildChallengeAsync("provider.a", "/callback");

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildChallengeAsync_ReturnsProperties_WhenConfigComplete()
    {
        var providers = new List<ExternalProviderInfo>
        {
            new ExternalProviderInfo("provider.a", "Provider A", "module.a", IsEnabled: true),
        };
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["authority"]     = "https://idp.example.com",
            ["clientid"]      = "my-client-id",
            ["clientsecret"]  = "my-secret",
        };
        var configService     = new StubExternalLoginConfigService(providers: providers, config: config);
        var claimsEnricher    = new StubIdpClaimsEnricher();
        var loginInfoProvider = new StubExternalLoginInfoProvider(info: null);

        var svc    = BuildService(configService, claimsEnricher, loginInfoProvider);
        var result = await svc.BuildChallengeAsync("provider.a", "/callback");

        Assert.NotNull(result);
        Assert.Equal("/callback", result.RedirectUri);
    }

    // ------------------------------------------------------------------
    // HandleCallbackAsync — Success path
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleCallbackAsync_ReturnsSuccess_WhenMemberLinked()
    {
        const string subject = "sub-linked-123";

        // Seed a member whose OauthID matches the subject.
        _db.Members.Add(new Member
        {
            MemberNumber = "M001",
            FirstName    = "Anna",
            LastName     = "Example",
            OauthID      = subject,
        });
        await _db.SaveChangesAsync();

        var info = BuildExternalLoginInfo("oidc.provider.a", subject);

        var configService     = new StubExternalLoginConfigService();
        var claimsEnricher    = new StubIdpClaimsEnricher();
        var loginInfoProvider = new StubExternalLoginInfoProvider(info: info);

        var svc    = BuildService(configService, claimsEnricher, loginInfoProvider);
        var result = await svc.HandleCallbackAsync();

        Assert.Equal(ExternalLoginStatus.Success, result.Status);
        Assert.Equal(subject, result.ExternalUserId);
    }

    // ------------------------------------------------------------------
    // HandleCallbackAsync — NoLinkedMember path
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleCallbackAsync_ReturnsNoLinkedMember_WhenNoMemberFound()
    {
        const string subject = "sub-unknown-456";

        var info = BuildExternalLoginInfo("oidc.provider.a", subject);

        var configService     = new StubExternalLoginConfigService();
        var claimsEnricher    = new StubIdpClaimsEnricher();
        var loginInfoProvider = new StubExternalLoginInfoProvider(info: info);

        var svc    = BuildService(configService, claimsEnricher, loginInfoProvider);
        var result = await svc.HandleCallbackAsync();

        Assert.Equal(ExternalLoginStatus.NoLinkedMember, result.Status);
        Assert.Equal(subject, result.ExternalUserId);
    }

    // ------------------------------------------------------------------
    // HandleCallbackAsync — ProviderError path (null info)
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleCallbackAsync_ReturnsProviderError_WhenNoExternalLoginInfo()
    {
        var configService     = new StubExternalLoginConfigService();
        var claimsEnricher    = new StubIdpClaimsEnricher();
        var loginInfoProvider = new StubExternalLoginInfoProvider(info: null);

        var svc    = BuildService(configService, claimsEnricher, loginInfoProvider);
        var result = await svc.HandleCallbackAsync();

        Assert.Equal(ExternalLoginStatus.ProviderError, result.Status);
    }

    // ------------------------------------------------------------------
    // HandleCallbackAsync — claims enricher is called
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleCallbackAsync_CallsClaimsEnricher_WithCorrectProviderKey()
    {
        const string subject = "sub-enricher-test";
        var info = BuildExternalLoginInfo("oidc.provider.xyz", subject);

        var configService     = new StubExternalLoginConfigService();
        var claimsEnricher    = new CapturingIdpClaimsEnricher();
        var loginInfoProvider = new StubExternalLoginInfoProvider(info: info);

        var svc = BuildService(configService, claimsEnricher, loginInfoProvider);
        await svc.HandleCallbackAsync();

        Assert.Equal("provider.xyz", claimsEnricher.LastProviderKey);
    }

    // ------------------------------------------------------------------
    // helper: build an ExternalLoginInfo without a real SignInManager
    // ------------------------------------------------------------------

    private static ExternalLoginInfo BuildExternalLoginInfo(string loginProvider, string subject)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, subject) };
        var identity = new ClaimsIdentity(claims, loginProvider);
        var principal = new ClaimsPrincipal(identity);

        return new ExternalLoginInfo(principal, loginProvider, subject, loginProvider);
    }

    // ------------------------------------------------------------------
    // stubs and fakes
    // ------------------------------------------------------------------

    private sealed class StubExternalLoginConfigService : IExternalLoginConfigService
    {
        private readonly IReadOnlyList<ExternalProviderInfo> _providers;
        private readonly IReadOnlyDictionary<string, string> _config;

        public StubExternalLoginConfigService(
            IReadOnlyList<ExternalProviderInfo>? providers = null,
            IReadOnlyDictionary<string, string>? config   = null)
        {
            _providers = providers ?? Array.Empty<ExternalProviderInfo>();
            _config    = config    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public Task<IReadOnlyList<ExternalProviderInfo>> GetAllDeclaredProvidersAsync(
            CancellationToken ct = default)
            => Task.FromResult(_providers);

        public Task<IReadOnlyDictionary<string, string>> GetConfigAsync(
            string providerKey, CancellationToken ct = default)
            => Task.FromResult(_config);

        public Task SaveConfigAsync(
            string providerKey,
            IReadOnlyDictionary<string, string> configValues,
            CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<PluginExternalLoginTestResult> TestConnectionAsync(
            string providerKey, CancellationToken ct = default)
            => Task.FromResult(new PluginExternalLoginTestResult(true));
    }

    private sealed class StubIdpClaimsEnricher : IIdpClaimsEnricher
    {
        public Task<IReadOnlyList<PluginClaimEntry>> EnrichAsync(
            string providerKey,
            PluginExternalLoginContext context,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PluginClaimEntry>>(Array.Empty<PluginClaimEntry>());
    }

    private sealed class CapturingIdpClaimsEnricher : IIdpClaimsEnricher
    {
        public string? LastProviderKey { get; private set; }

        public Task<IReadOnlyList<PluginClaimEntry>> EnrichAsync(
            string providerKey,
            PluginExternalLoginContext context,
            CancellationToken ct = default)
        {
            LastProviderKey = providerKey;
            return Task.FromResult<IReadOnlyList<PluginClaimEntry>>(Array.Empty<PluginClaimEntry>());
        }
    }

    private sealed class StubExternalLoginInfoProvider : IExternalLoginInfoProvider
    {
        private readonly ExternalLoginInfo? _info;

        public StubExternalLoginInfoProvider(ExternalLoginInfo? info)
        {
            _info = info;
        }

        public Task<ExternalLoginInfo?> GetExternalLoginInfoAsync(CancellationToken ct = default)
            => Task.FromResult(_info);
    }
}
