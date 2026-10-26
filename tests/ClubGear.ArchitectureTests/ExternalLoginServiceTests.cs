using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.ExternalLogin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
        IExternalLoginInfoProvider loginInfoProvider,
        IExternalLoginSessionService? sessionService = null,
        IAuthenticationSchemeProvider? schemeProvider = null)
        => new ExternalLoginService(
            configService,
            claimsEnricher,
            loginInfoProvider,
            sessionService ?? new StubExternalLoginSessionService(),
            schemeProvider ?? new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions())),
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

        var claimsEnricher    = new StubIdpClaimsEnricher();
        var loginInfoProvider = new StubExternalLoginInfoProvider(info: null);

        var svc = BuildService(
            new StubExternalLoginConfigService(
                providers,
                CompleteConfig()),
            claimsEnricher,
            loginInfoProvider);
        var active = await svc.GetActiveProvidersAsync();

        Assert.Single(active);
        Assert.Equal("provider.a", active[0].ProviderKey);
    }

    [Fact]
    public async Task GetActiveProvidersAsync_HidesEnabledProvider_WhenConfigIncomplete()
    {
        var service = BuildService(
            new StubExternalLoginConfigService(
                [new ExternalProviderInfo("provider.a", "Provider A", "module.a", IsEnabled: true)],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            new StubIdpClaimsEnricher(),
            new StubExternalLoginInfoProvider(null));

        Assert.Empty(await service.GetActiveProvidersAsync());
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
        var configService = new StubExternalLoginConfigService(
            providers,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
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
        Assert.Equal("oidc.provider.a", result.Items["LoginProvider"]);
    }

    [Fact]
    public async Task BuildChallengeAsync_ReturnsNull_WhenProviderDisabled()
    {
        var providers = new[]
        {
            new ExternalProviderInfo("provider.a", "Provider A", "module.a", IsEnabled: false),
        };
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["authority"] = "https://idp.example.com",
            ["clientid"] = "client",
            ["clientsecret"] = "secret",
        };
        var service = BuildService(
            new StubExternalLoginConfigService(providers, config),
            new StubIdpClaimsEnricher(),
            new StubExternalLoginInfoProvider(null));

        var result = await service.BuildChallengeAsync("provider.a", "/callback");

        Assert.Null(result);
    }

    [Fact]
    public async Task BuildChallengeAsync_RegistersProviderSpecificOidcScheme()
    {
        var providers = new[]
        {
            new ExternalProviderInfo("keycloak", "Keycloak", "module.iam", IsEnabled: true),
        };
        var config = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["authority"] = "https://idp.example.com",
            ["clientid"] = "client",
            ["clientsecret"] = "secret",
        };
        var schemes = new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions()));
        var service = BuildService(
            new StubExternalLoginConfigService(providers, config),
            new StubIdpClaimsEnricher(),
            new StubExternalLoginInfoProvider(null),
            schemeProvider: schemes);

        await service.BuildChallengeAsync("keycloak", "/callback");

        var scheme = await schemes.GetSchemeAsync("oidc.keycloak");
        Assert.NotNull(scheme);
        Assert.Equal(typeof(Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectHandler), scheme.HandlerType);
    }

    [Theory]
    [InlineData("http://idp.example.test")]
    [InlineData("ftp://idp.example.test")]
    [InlineData("https://user:password@idp.example.test")]
    [InlineData("https://idp.example.test?tenant=club")]
    [InlineData("https://idp.example.test/#fragment")]
    [InlineData("not-an-authority")]
    public async Task UnusableAuthority_IsNeitherRegisteredNorChallenged(string authority)
    {
        var providers = new[]
        {
            new ExternalProviderInfo("keycloak", "Keycloak", "module.iam", IsEnabled: true),
        };
        var schemes = new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions()));
        var service = BuildService(
            new StubExternalLoginConfigService(providers, CompleteConfig(authority)),
            new StubIdpClaimsEnricher(),
            new StubExternalLoginInfoProvider(null),
            schemeProvider: schemes);

        await service.RegisterDeclaredSchemesAsync();
        var challenge = await service.BuildChallengeAsync("keycloak", "/callback");

        Assert.Null(challenge);
        Assert.Null(await schemes.GetSchemeAsync("oidc.keycloak"));
    }

    [Theory]
    [InlineData("http://localhost:8080/realms/club")]
    [InlineData("http://127.0.0.1:8080/realms/club")]
    [InlineData("http://[::1]:8080/realms/club")]
    public async Task HttpLoopbackAuthority_IsRegisteredAndCanBeChallenged(string authority)
    {
        var providers = new[]
        {
            new ExternalProviderInfo("keycloak", "Keycloak", "module.iam", IsEnabled: true),
        };
        var schemes = new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions()));
        var service = BuildService(
            new StubExternalLoginConfigService(providers, CompleteConfig(authority)),
            new StubIdpClaimsEnricher(),
            new StubExternalLoginInfoProvider(null),
            schemeProvider: schemes);

        await service.RegisterDeclaredSchemesAsync();
        var challenge = await service.BuildChallengeAsync("keycloak", "/callback");

        Assert.NotNull(challenge);
        Assert.NotNull(await schemes.GetSchemeAsync("oidc.keycloak"));
    }

    [Fact]
    public async Task RegisterDeclaredSchemesAsync_RegistersOnlyUniqueEnabledAvailableValidProviders()
    {
        var providers = new[]
        {
            new ExternalProviderInfo("keycloak", "Keycloak", "module.iam", true),
            new ExternalProviderInfo("entra", "Entra", "module.iam", false),
            new ExternalProviderInfo("_invalid", "Invalid", "module.iam", true),
            new ExternalProviderInfo("broken", "Broken", "module.iam", true, IsAvailable: false),
            new ExternalProviderInfo("duplicate", "Duplicate A", "module.a", true),
            new ExternalProviderInfo("DUPLICATE", "Duplicate B", "module.b", true),
        };
        var schemes = new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions()));
        var service = BuildService(
            new StubExternalLoginConfigService(providers, CompleteConfig()),
            new StubIdpClaimsEnricher(),
            new StubExternalLoginInfoProvider(null),
            schemeProvider: schemes);

        await service.RegisterDeclaredSchemesAsync();

        Assert.NotNull(await schemes.GetSchemeAsync("oidc.keycloak"));
        Assert.Null(await schemes.GetSchemeAsync("oidc.entra"));
        Assert.Null(await schemes.GetSchemeAsync("oidc._invalid"));
        Assert.Null(await schemes.GetSchemeAsync("oidc.broken"));
        Assert.Null(await schemes.GetSchemeAsync("oidc.duplicate"));
    }

    // ------------------------------------------------------------------
    // HandleCallbackAsync — Success path
    // ------------------------------------------------------------------

    [Fact]
    public async Task HandleCallbackAsync_ReturnsSuccess_WhenMemberLinked()
    {
        const string subject = "sub-linked-123";

        // Seed a member whose OauthID matches the subject.
        _db.Users.Add(new ApplicationUser
        {
            Id = "application-user-1",
            UserName = "linked@example.test",
            NormalizedUserName = "LINKED@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString("N"),
        });
        _db.Members.Add(new Member
        {
            MemberNumber = "M001",
            FirstName    = "Anna",
            LastName     = "Example",
            OauthID      = subject,
            ApplicationUserId = "application-user-1",
        });
        await _db.SaveChangesAsync();

        var info = BuildExternalLoginInfo("oidc.provider.a", subject);

        var configService     = new StubExternalLoginConfigService();
        var claimsEnricher    = new StubIdpClaimsEnricher();
        var loginInfoProvider = new StubExternalLoginInfoProvider(info: info);

        var session = new StubExternalLoginSessionService();
        var svc = BuildService(configService, claimsEnricher, loginInfoProvider, session);
        var result = await svc.HandleCallbackAsync();

        Assert.Equal(ExternalLoginStatus.Success, result.Status);
        Assert.Equal(subject, result.ExternalUserId);
        Assert.Equal(1, session.ClearCalls);
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

        var session = new StubExternalLoginSessionService();
        var svc = BuildService(configService, claimsEnricher, loginInfoProvider, session);
        var result = await svc.HandleCallbackAsync();

        Assert.Equal(ExternalLoginStatus.NoLinkedMember, result.Status);
        Assert.Equal(subject, result.ExternalUserId);
        Assert.Equal(1, session.ClearCalls);
    }

    [Fact]
    public async Task HandleCallbackAsync_ReturnsNoLinkedMember_WhenMemberHasNoApplicationUser()
    {
        const string subject = "sub-without-user";
        _db.Members.Add(new Member
        {
            MemberNumber = "M-NO-USER",
            FirstName = "No",
            LastName = "User",
            OauthID = subject,
        });
        await _db.SaveChangesAsync();

        var service = BuildService(
            new StubExternalLoginConfigService(),
            new StubIdpClaimsEnricher(),
            new StubExternalLoginInfoProvider(BuildExternalLoginInfo("oidc.provider.a", subject)));

        var result = await service.HandleCallbackAsync();

        Assert.Equal(ExternalLoginStatus.NoLinkedMember, result.Status);
    }

    [Fact]
    public async Task HandleCallbackAsync_PassesMappedClaimsToApplicationSession()
    {
        const string rawSubject = "raw-subject";
        const string mappedSubject = "provider:stable-subject";
        _db.Users.Add(new ApplicationUser
        {
            Id = "application-user-claims",
            UserName = "claims@example.test",
            NormalizedUserName = "CLAIMS@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString("N"),
        });
        _db.Members.Add(new Member
        {
            MemberNumber = "M-CLAIMS",
            FirstName = "Mapped",
            LastName = "User",
            OauthID = mappedSubject,
            ApplicationUserId = "application-user-claims",
        });
        await _db.SaveChangesAsync();

        var mappedClaims = new[]
        {
            new PluginClaimEntry("sub", mappedSubject),
            new PluginClaimEntry("custom.role", "treasurer"),
        };
        var session = new StubExternalLoginSessionService();
        var service = BuildService(
            new StubExternalLoginConfigService(),
            new StaticClaimsEnricher(mappedClaims),
            new StubExternalLoginInfoProvider(BuildExternalLoginInfo("oidc.provider.a", rawSubject)),
            session);

        var result = await service.HandleCallbackAsync();

        Assert.Equal(ExternalLoginStatus.Success, result.Status);
        Assert.Equal("application-user-claims", session.ApplicationUserId);
        Assert.Equal(mappedClaims, session.AdditionalClaims);
    }

    [Fact]
    public async Task HandleCallbackAsync_SerializesRepeatedRawClaimsAsJsonArray()
    {
        const string subject = "repeated-claims-subject";
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, subject),
            new Claim("groups", "group-a"),
            new Claim("groups", "group-b"),
        };
        var info = new ExternalLoginInfo(
            new ClaimsPrincipal(new ClaimsIdentity(claims, "oidc.provider.a")),
            "oidc.provider.a",
            subject,
            "Provider A");
        var enricher = new CapturingIdpClaimsEnricher();
        var service = BuildService(
            new StubExternalLoginConfigService(),
            enricher,
            new StubExternalLoginInfoProvider(info));

        await service.HandleCallbackAsync();

        Assert.NotNull(enricher.LastContext);
        Assert.Equal("[\"group-a\",\"group-b\"]", enricher.LastContext.RawClaims["groups"]);
    }

    [Fact]
    public async Task HandleCallbackAsync_FailsClosed_WhenClaimMappingThrows()
    {
        var service = BuildService(
            new StubExternalLoginConfigService(),
            new ThrowingClaimsEnricher(),
            new StubExternalLoginInfoProvider(
                BuildExternalLoginInfo("oidc.provider.a", "subject")));

        var result = await service.HandleCallbackAsync();

        Assert.Equal(ExternalLoginStatus.ProviderError, result.Status);
    }

    [Fact]
    public async Task HandleCallbackAsync_FailsClosed_WhenClaimMappingReturnsNull()
    {
        var service = BuildService(
            new StubExternalLoginConfigService(),
            new NullClaimsEnricher(),
            new StubExternalLoginInfoProvider(
                BuildExternalLoginInfo("oidc.provider.a", "subject")));

        var result = await service.HandleCallbackAsync();

        Assert.Equal(ExternalLoginStatus.ProviderError, result.Status);
    }

    [Fact]
    public async Task HandleCallbackAsync_ReturnsProviderError_WhenProviderWasDisabled()
    {
        var config = new StubExternalLoginConfigService(
            providers:
            [
                new ExternalProviderInfo("provider.a", "Provider A", "module.a", IsEnabled: false),
            ]);
        var service = BuildService(
            config,
            new StubIdpClaimsEnricher(),
            new StubExternalLoginInfoProvider(
                BuildExternalLoginInfo("oidc.provider.a", "subject")));

        var result = await service.HandleCallbackAsync();

        Assert.Equal(ExternalLoginStatus.ProviderError, result.Status);
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

        var session = new StubExternalLoginSessionService();
        var svc = BuildService(configService, claimsEnricher, loginInfoProvider, session);
        var result = await svc.HandleCallbackAsync();

        Assert.Equal(ExternalLoginStatus.ProviderError, result.Status);
        Assert.Equal(1, session.ClearCalls);
    }

    [Fact]
    public async Task HandleCallbackAsync_ReturnsInternalError_WhenExternalCookieCannotBeCleared()
    {
        var session = new StubExternalLoginSessionService { ThrowOnClear = true };
        var service = BuildService(
            new StubExternalLoginConfigService(),
            new StubIdpClaimsEnricher(),
            new StubExternalLoginInfoProvider(
                BuildExternalLoginInfo("oidc.provider.a", "subject")),
            session);

        var result = await service.HandleCallbackAsync();

        Assert.Equal(ExternalLoginStatus.InternalError, result.Status);
        Assert.Null(session.ApplicationUserId);
    }

    [Fact]
    public async Task HandleCallbackAsync_ReturnsInternalError_WhenLocalSessionThrows()
    {
        const string subject = "session-error-subject";
        _db.Users.Add(new ApplicationUser
        {
            Id = "session-error-user",
            UserName = "session-error@example.test",
            NormalizedUserName = "SESSION-ERROR@EXAMPLE.TEST",
            SecurityStamp = Guid.NewGuid().ToString("N"),
        });
        _db.Members.Add(new Member
        {
            MemberNumber = "M-SESSION-ERROR",
            FirstName = "Session",
            LastName = "Error",
            OauthID = subject,
            ApplicationUserId = "session-error-user",
        });
        await _db.SaveChangesAsync();
        var session = new StubExternalLoginSessionService { ThrowOnSignIn = true };
        var service = BuildService(
            new StubExternalLoginConfigService(),
            new StubIdpClaimsEnricher(),
            new StubExternalLoginInfoProvider(
                BuildExternalLoginInfo("oidc.provider.a", subject)),
            session);

        var result = await service.HandleCallbackAsync();

        Assert.Equal(ExternalLoginStatus.InternalError, result.Status);
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

    private static IReadOnlyDictionary<string, string> CompleteConfig(
        string authority = "https://idp.example.test")
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["authority"] = authority,
            ["clientid"] = "client",
            ["clientsecret"] = "secret",
        };

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
            _providers = providers ??
            [
                new ExternalProviderInfo("provider.a", "Provider A", "module.a", IsEnabled: true),
                new ExternalProviderInfo("provider.xyz", "Provider XYZ", "module.xyz", IsEnabled: true),
            ];
            _config = config ?? CompleteConfig();
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
        public PluginExternalLoginContext? LastContext { get; private set; }

        public Task<IReadOnlyList<PluginClaimEntry>> EnrichAsync(
            string providerKey,
            PluginExternalLoginContext context,
            CancellationToken ct = default)
        {
            LastProviderKey = providerKey;
            LastContext = context;
            return Task.FromResult<IReadOnlyList<PluginClaimEntry>>(Array.Empty<PluginClaimEntry>());
        }
    }

    private sealed class StaticClaimsEnricher(IReadOnlyList<PluginClaimEntry> claims) : IIdpClaimsEnricher
    {
        public Task<IReadOnlyList<PluginClaimEntry>> EnrichAsync(
            string providerKey,
            PluginExternalLoginContext context,
            CancellationToken ct = default)
            => Task.FromResult(claims);
    }

    private sealed class ThrowingClaimsEnricher : IIdpClaimsEnricher
    {
        public Task<IReadOnlyList<PluginClaimEntry>> EnrichAsync(
            string providerKey,
            PluginExternalLoginContext context,
            CancellationToken ct = default)
            => throw new InvalidOperationException("mapping failed");
    }

    private sealed class NullClaimsEnricher : IIdpClaimsEnricher
    {
        public Task<IReadOnlyList<PluginClaimEntry>> EnrichAsync(
            string providerKey,
            PluginExternalLoginContext context,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PluginClaimEntry>>(null!);
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

    private sealed class StubExternalLoginSessionService : IExternalLoginSessionService
    {
        public string? ApplicationUserId { get; private set; }
        public IReadOnlyList<PluginClaimEntry> AdditionalClaims { get; private set; } = [];
        public bool Succeeds { get; init; } = true;
        public int ClearCalls { get; private set; }
        public bool ThrowOnClear { get; init; }
        public bool ThrowOnSignIn { get; init; }

        public Task ClearExternalAsync(CancellationToken cancellationToken = default)
        {
            ClearCalls++;
            if (ThrowOnClear)
            {
                throw new InvalidOperationException("clear failed");
            }

            return Task.CompletedTask;
        }

        public Task<bool> SignInAsync(
            string? applicationUserId,
            IReadOnlyList<PluginClaimEntry> additionalClaims,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnSignIn)
            {
                throw new InvalidOperationException("sign-in failed");
            }

            ApplicationUserId = applicationUserId;
            AdditionalClaims = additionalClaims;
            return Task.FromResult(Succeeds);
        }
    }
}
