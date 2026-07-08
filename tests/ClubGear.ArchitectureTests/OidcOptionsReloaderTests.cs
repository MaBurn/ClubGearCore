using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.ExternalLogin;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
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
            => Task.FromResult<IReadOnlyList<SystemConfigEntry>>(Array.Empty<SystemConfigEntry>());
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
            { (section, "authority"),     authority     },
            { (section, "clientid"),      clientId      },
            { (section, "clientsecret"),  clientSecret  },
        });
    }

    // ------------------------------------------------------------------
    // test (a): Configure(name, options) populates Authority, ClientId, ClientSecret
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
        reloader.Configure("oidc.generic", options);

        Assert.Equal("https://idp.example.com", options.Authority);
        Assert.Equal("my-client-id",            options.ClientId);
        Assert.Equal("my-secret",               options.ClientSecret);
    }

    // ------------------------------------------------------------------
    // test (b): Configure(name, options) strips "oidc." prefix correctly
    // ------------------------------------------------------------------

    [Fact]
    public void Configure_StripsPrefixAndResolvesCorrectSection()
    {
        const string providerKey = "myidp";
        var section = $"externallogin.{providerKey}";
        var reloader = CreateReloader(new Dictionary<(string, string), string>
        {
            { (section, "authority"),    "https://myidp.example.com" },
            { (section, "clientid"),     "cid-myidp"                 },
            { (section, "clientsecret"), "sec-myidp"                 },
        });

        var options = new OpenIdConnectOptions();
        reloader.Configure($"oidc.{providerKey}", options);

        Assert.Equal("https://myidp.example.com", options.Authority);
        Assert.Equal("cid-myidp",                 options.ClientId);
        Assert.Equal("sec-myidp",                 options.ClientSecret);
    }

    // ------------------------------------------------------------------
    // test (c): missing values leave options unchanged
    // ------------------------------------------------------------------

    [Fact]
    public void Configure_DoesNotOverwrite_WhenValuesAreMissing()
    {
        var reloader = CreateReloader(new Dictionary<(string, string), string>());
        var options = new OpenIdConnectOptions
        {
            Authority    = "original-authority",
            ClientId     = "original-client",
            ClientSecret = "original-secret",
        };

        reloader.Configure("oidc.unknown", options);

        Assert.Equal("original-authority", options.Authority);
        Assert.Equal("original-client",    options.ClientId);
        Assert.Equal("original-secret",    options.ClientSecret);
    }

    // ------------------------------------------------------------------
    // test (d): null/empty name — Configure(options) overload — no exception
    // ------------------------------------------------------------------

    [Fact]
    public void Configure_WithoutName_DoesNotThrow()
    {
        var reloader = CreateReloader(new Dictionary<(string, string), string>());
        var options = new OpenIdConnectOptions();

        // Must not throw
        reloader.Configure(options);
    }
}
