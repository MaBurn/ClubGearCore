using ClubGear.Models;
using ClubGear.Services;
using ClubGear.Services.Abstractions;
using ClubGear.Services.ExternalLogin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class OidcOptionsReloaderTests
{
    // ------------------------------------------------------------------
    // helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Stub ISystemConfigService that returns pre-canned values per (section, name).
    /// </summary>
    private sealed class StubSystemConfigService : ISystemConfigService
    {
        private readonly Dictionary<(string Section, string Name), string> _values;

        public StubSystemConfigService(Dictionary<(string, string), string> values)
            => _values = values;

        public Task<string?> GetValueAsync(string section, string name, CancellationToken cancellationToken = default)
        {
            _values.TryGetValue((section, name), out var value);
            return Task.FromResult(value);
        }

        // Unused members
        public Task<IReadOnlyList<SystemConfigEntry>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SystemConfigEntry>>(Array.Empty<SystemConfigEntry>());
        public Task<IReadOnlyList<SystemConfigEntry>> GetBySectionAsync(string section, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SystemConfigEntry>>(_values
                .Where(entry => string.Equals(
                    entry.Key.Section,
                    section,
                    StringComparison.OrdinalIgnoreCase))
                .Select(entry => new SystemConfigEntry
                {
                    Section = entry.Key.Section,
                    Name = entry.Key.Name,
                    Value = entry.Value
                })
                .ToList());
        public Task UpsertAsync(string section, string name, string value, string description, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task UpsertManyAsync(IEnumerable<SystemConfigEntry> entries, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task DeleteByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>
    /// Builds an OidcOptionsReloader backed by a stub config service registered in a
    /// real ServiceProvider so IServiceScopeFactory resolves correctly.
    /// </summary>
    private static OidcOptionsReloader CreateReloader(Dictionary<(string, string), string> values)
    {
        var services = new ServiceCollection();
        services.AddScoped<ISystemConfigService>(_ => new StubSystemConfigService(values));
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        return new OidcOptionsReloader(scopeFactory);
    }

    private static OidcOptionsReloader CreateReloader(
        string providerKey,
        string authority,
        string clientId,
        string clientSecret)
    {
        var section = $"externallogin.{providerKey}";
        return CreateReloader(new Dictionary<(string, string), string>
        {
            { (section, "enabled"),       "true"        },
            { (section, "authority"),     authority     },
            { (section, "clientid"),      clientId      },
            { (section, "clientsecret"),  clientSecret  },
        });
    }

    // ------------------------------------------------------------------
    // test (a): PostConfigure(name, options) populates provider-specific options
    // ------------------------------------------------------------------

    [Fact]
    public void Configure_PopulatesAuthorityClientIdClientSecret_FromMockData()
    {
        var reloader = CreateReloader(
            providerKey:  "generic",
            authority:    "https://idp.example.com",
            clientId:     "my-client-id",
            clientSecret: "my-secret");

        var options = new OpenIdConnectOptions();
        reloader.PostConfigure("oidc.generic", options);

        Assert.Equal("https://idp.example.com", options.Authority);
        Assert.Equal("my-client-id",            options.ClientId);
        Assert.Equal("my-secret",               options.ClientSecret);
        Assert.Equal("/signin-oidc-generic", options.CallbackPath);
        Assert.Equal(Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme, options.SignInScheme);
    }

    // ------------------------------------------------------------------
    // test (b): PostConfigure(name, options) strips "oidc." prefix correctly
    // ------------------------------------------------------------------

    [Fact]
    public void Configure_StripsPrefixAndResolvesCorrectSection()
    {
        const string providerKey = "myidp";
        var section = $"externallogin.{providerKey}";
        var reloader = CreateReloader(new Dictionary<(string, string), string>
        {
            { (section, "enabled"),      "true"                        },
            { (section, "authority"),    "https://myidp.example.com" },
            { (section, "clientid"),     "cid-myidp"                 },
            { (section, "clientsecret"), "sec-myidp"                 },
        });

        var options = new OpenIdConnectOptions();
        reloader.PostConfigure($"oidc.{providerKey}", options);

        Assert.Equal("https://myidp.example.com", options.Authority);
        Assert.Equal("cid-myidp",                 options.ClientId);
        Assert.Equal("sec-myidp",                 options.ClientSecret);
    }

    [Fact]
    public void Configure_ReadsCoreKeys_CaseInsensitively()
    {
        var reloader = CreateReloader(new Dictionary<(string, string), string>
        {
            { ("externallogin.keycloak", "Enabled"), "TRUE" },
            { ("externallogin.keycloak", "Authority"), "https://idp.example.test" },
            { ("externallogin.keycloak", "clientId"), "clubgear" },
            { ("externallogin.keycloak", "ClientSecret"), "secret" },
        });
        var options = new OpenIdConnectOptions();

        reloader.PostConfigure("oidc.keycloak", options);

        Assert.Equal("https://idp.example.test", options.Authority);
        Assert.Equal("clubgear", options.ClientId);
        Assert.Equal("secret", options.ClientSecret);
    }

    [Fact]
    public void Configure_AllowsLoopbackHttp_AndRetainsValidatedIssuerClaim()
    {
        var reloader = CreateReloader(
            providerKey: "keycloak",
            authority: "http://localhost:8080/realms/club",
            clientId: "clubgear",
            clientSecret: "secret");
        var options = new OpenIdConnectOptions();

        reloader.PostConfigure("oidc.keycloak", options);

        Assert.False(options.RequireHttpsMetadata);
        var identity = new ClaimsIdentity([new Claim("iss", "http://localhost:8080/realms/club")]);
        using var json = JsonDocument.Parse("{}");
        foreach (var action in options.ClaimActions)
        {
            action.Run(json.RootElement, identity, "OIDC");
        }
        Assert.Equal(
            "http://localhost:8080/realms/club",
            identity.FindFirst("iss")?.Value);
    }

    // ------------------------------------------------------------------
    // test (c): unusable providers are quarantined
    // ------------------------------------------------------------------

    [Fact]
    public void Configure_QuarantinesScheme_WhenConfigIsMissing()
    {
        var reloader = CreateReloader(new Dictionary<(string, string), string>());
        var options = CreatePreviouslyConfiguredOptions();

        reloader.PostConfigure("oidc.unknown", options);

        AssertQuarantined(options);
    }

    [Fact]
    public void Configure_QuarantinesScheme_WhenProviderIsDisabled()
    {
        var reloader = CreateReloader(new Dictionary<(string, string), string>
        {
            { ("externallogin.keycloak", "enabled"), "false" },
            { ("externallogin.keycloak", "authority"), "https://idp.example.test" },
            { ("externallogin.keycloak", "clientid"), "clubgear" },
            { ("externallogin.keycloak", "clientsecret"), "secret" },
        });
        var options = CreatePreviouslyConfiguredOptions();

        reloader.PostConfigure("oidc.keycloak", options);

        AssertQuarantined(options);
    }

    [Fact]
    public void Configure_QuarantinesScheme_WhenAuthorityIsInvalid()
    {
        var reloader = CreateReloader(new Dictionary<(string, string), string>
        {
            { ("externallogin.keycloak", "enabled"), "true" },
            { ("externallogin.keycloak", "authority"), "http://idp.example.test" },
            { ("externallogin.keycloak", "clientid"), "clubgear" },
            { ("externallogin.keycloak", "clientsecret"), "secret" },
        });
        var options = CreatePreviouslyConfiguredOptions();

        reloader.PostConfigure("oidc.keycloak", options);

        AssertQuarantined(options);
    }

    // ------------------------------------------------------------------
    // test (d): null/empty name — no exception
    // ------------------------------------------------------------------

    [Fact]
    public void Configure_WithoutName_DoesNotThrow()
    {
        var reloader = CreateReloader(new Dictionary<(string, string), string>());
        var options = new OpenIdConnectOptions();

        // Must not throw
        reloader.PostConfigure(name: null, options);
    }

    [Fact]
    public void RegisteredOptionsPipeline_ConfiguresProviderSpecificSchemeAfterPlaceholders()
    {
        var values = new Dictionary<(string, string), string>
        {
            { ("externallogin.generic", "enabled"), "true" },
            { ("externallogin.generic", "authority"), "https://keycloak.example/realms/club" },
            { ("externallogin.generic", "clientid"), "clubgear" },
            { ("externallogin.generic", "clientsecret"), "secret" },
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddClubGearCoreServices();
        services.AddScoped<ISystemConfigService>(_ => new StubSystemConfigService(values));
        services.AddAuthentication()
            .AddOpenIdConnect("oidc.generic", options =>
            {
                options.Authority = "https://unconfigured.invalid";
                options.ClientId = "unconfigured";
            });

        using var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get("oidc.generic");

        Assert.Equal("https://keycloak.example/realms/club", options.Authority);
        Assert.Equal("clubgear", options.ClientId);
        Assert.Equal("secret", options.ClientSecret);
        Assert.Equal("/signin-oidc-generic", options.CallbackPath);
        Assert.Equal(IdentityConstants.ExternalScheme, options.SignInScheme);
        Assert.NotNull(options.ConfigurationManager);
    }

    [Fact]
    public async Task AuthenticationMiddleware_DoesNotThrow_ForRegisteredSchemeWithInvalidConfig()
    {
        var values = new Dictionary<(string, string), string>
        {
            { ("externallogin.broken", "enabled"), "true" },
            { ("externallogin.broken", "authority"), "http://idp.example.test" },
            { ("externallogin.broken", "clientid"), "clubgear" },
            { ("externallogin.broken", "clientsecret"), "secret" },
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddScoped<ISystemConfigService>(_ => new StubSystemConfigService(values));
        services.AddTransient<IPostConfigureOptions<OpenIdConnectOptions>, OidcOptionsReloader>();
        services.AddAuthentication()
            .AddCookie(IdentityConstants.ExternalScheme)
            .AddOpenIdConnect("oidc.generic", options =>
            {
                options.Authority = "https://unconfigured.invalid";
                options.ClientId = "unconfigured";
            });

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var schemeProvider = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();
        schemeProvider.AddScheme(new AuthenticationScheme(
            "oidc.broken",
            "Broken provider",
            typeof(OpenIdConnectHandler)));

        var brokenOptions = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
            .Get("oidc.broken");
        AssertQuarantined(brokenOptions);
        Assert.IsType<StaticConfigurationManager<OpenIdConnectConfiguration>>(
            brokenOptions.ConfigurationManager);

        var nextWasCalled = false;
        var middleware = new AuthenticationMiddleware(
            _ =>
            {
                nextWasCalled = true;
                return Task.CompletedTask;
            },
            schemeProvider);
        var context = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
        };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("club.example");
        context.Request.Path = "/health";

        await middleware.Invoke(context);

        Assert.True(nextWasCalled);
    }

    private static OpenIdConnectOptions CreatePreviouslyConfiguredOptions()
        => new()
        {
            Authority = "https://original.example.test",
            MetadataAddress = "https://original.example.test/.well-known/openid-configuration",
            ClientId = "original-client",
            ClientSecret = "original-secret",
        };

    private static void AssertQuarantined(OpenIdConnectOptions options)
    {
        Assert.Null(options.Authority);
        Assert.Null(options.MetadataAddress);
        Assert.Equal("clubgear-disabled-provider", options.ClientId);
        Assert.Equal("clubgear-disabled-provider", options.ClientSecret);
        Assert.True(options.RequireHttpsMetadata);
        Assert.NotNull(options.Configuration);
        Assert.Equal("https://oidc-disabled.invalid", options.Configuration.Issuer);
        Assert.Equal("https://oidc-disabled.invalid/authorize", options.Configuration.AuthorizationEndpoint);
        Assert.Equal("https://oidc-disabled.invalid/token", options.Configuration.TokenEndpoint);
    }
}
