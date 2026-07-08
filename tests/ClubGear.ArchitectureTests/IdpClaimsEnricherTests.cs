using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.ExternalLogin;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class IdpClaimsEnricherTests
{
    // ---------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------

    private static RegisteredPluginRuntime MakeRuntime(
        string moduleId,
        IReadOnlyList<PluginIdentityProviderContribution> providers)
        => new RegisteredPluginRuntime(
            moduleId,
            moduleId,
            new Version(1, 0, 0),
            $"test:{moduleId}",
            Array.Empty<PluginRouteContribution>(),
            Array.Empty<PluginServiceContribution>(),
            Array.Empty<PluginMemberProviderContribution>(),
            Array.Empty<PluginBackgroundJobContribution>(),
            Array.Empty<PluginNavEntry>(),
            Array.Empty<PluginAuditSinkContribution>(),
            providers,
            Array.Empty<PluginSelfServiceProfileProviderContribution>());

    private static PluginExternalLoginContext MakeContext(string providerKey)
        => new PluginExternalLoginContext(
            providerKey,
            "sub-123",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    // ---------------------------------------------------------------
    // test (a): no plugins registered — returns empty list
    // ---------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_ReturnsEmpty_WhenNoPluginsRegistered()
    {
        var registry = new StubRegistryReader(Array.Empty<RegisteredPluginRuntime>(), _ => null);
        var enricher = new IdpClaimsEnricher(registry, NullLogger<IdpClaimsEnricher>.Instance);

        var result = await enricher.EnrichAsync("any.provider", MakeContext("any.provider"));

        Assert.Empty(result);
    }

    // ---------------------------------------------------------------
    // test (b): plugin registered but providerKey does not match — returns empty
    // ---------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_ReturnsEmpty_WhenProviderKeyDoesNotMatch()
    {
        var runtime = MakeRuntime("plugin.a",
            [new PluginIdentityProviderContribution("other.provider", "ProviderTypeA")]);

        var registry = new StubRegistryReader([runtime], _ => new FakeIdentityProviderPlugin("other.provider",
            [new PluginClaimEntry("role", "admin")]));

        var enricher = new IdpClaimsEnricher(registry, NullLogger<IdpClaimsEnricher>.Instance);

        var result = await enricher.EnrichAsync("my.provider", MakeContext("my.provider"));

        Assert.Empty(result);
    }

    // ---------------------------------------------------------------
    // test (c): matching plugin returns claims — enricher returns those claims
    // ---------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_ReturnsClaims_FromMatchingPlugin()
    {
        var expectedClaims = new List<PluginClaimEntry>
        {
            new PluginClaimEntry("role",  "admin"),
            new PluginClaimEntry("email", "user@example.com"),
        };

        var runtime = MakeRuntime("plugin.a",
            [new PluginIdentityProviderContribution("my.provider", "ProviderTypeA")]);

        var registry = new StubRegistryReader(
            [runtime],
            _ => new FakeIdentityProviderPlugin("my.provider", expectedClaims));

        var enricher = new IdpClaimsEnricher(registry, NullLogger<IdpClaimsEnricher>.Instance);

        var result = await enricher.EnrichAsync("my.provider", MakeContext("my.provider"));

        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.Type == "role"  && c.Value == "admin");
        Assert.Contains(result, c => c.Type == "email" && c.Value == "user@example.com");
    }

    // ---------------------------------------------------------------
    // test (d): CreateMemberProvider returns null — returns empty list
    // ---------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_ReturnsEmpty_WhenCreateMemberProviderReturnsNull()
    {
        var runtime = MakeRuntime("plugin.a",
            [new PluginIdentityProviderContribution("my.provider", "UnresolvableType")]);

        var registry = new StubRegistryReader([runtime], _ => null);
        var enricher = new IdpClaimsEnricher(registry, NullLogger<IdpClaimsEnricher>.Instance);

        var result = await enricher.EnrichAsync("my.provider", MakeContext("my.provider"));

        Assert.Empty(result);
    }

    // ---------------------------------------------------------------
    // test (e): context is forwarded to the plugin unchanged
    // ---------------------------------------------------------------

    [Fact]
    public async Task EnrichAsync_ForwardsContext_ToPlugin()
    {
        var runtime = MakeRuntime("plugin.a",
            [new PluginIdentityProviderContribution("my.provider", "ProviderTypeA")]);

        var capturingPlugin = new CapturingIdentityProviderPlugin("my.provider");

        var registry = new StubRegistryReader([runtime], _ => capturingPlugin);
        var enricher = new IdpClaimsEnricher(registry, NullLogger<IdpClaimsEnricher>.Instance);

        var context = MakeContext("my.provider");
        await enricher.EnrichAsync("my.provider", context);

        Assert.Same(context, capturingPlugin.LastContext);
    }

    // ---------------------------------------------------------------
    // stubs / fakes
    // ---------------------------------------------------------------

    private sealed class StubRegistryReader : IPluginRegistryReader
    {
        private readonly IReadOnlyList<RegisteredPluginRuntime> _runtimes;
        private readonly Func<PluginIdentityProviderContribution, IIdentityProviderPlugin?> _providerFactory;

        public StubRegistryReader(
            IReadOnlyList<RegisteredPluginRuntime> runtimes,
            Func<PluginIdentityProviderContribution, IIdentityProviderPlugin?> providerFactory)
        {
            _runtimes        = runtimes;
            _providerFactory = providerFactory;
        }

        public IReadOnlyList<RegisteredPluginRuntime> GetRegisteredPlugins() => _runtimes;

        public RegisteredPluginRuntime? GetByModuleId(string moduleId)
            => _runtimes.FirstOrDefault(r => string.Equals(r.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase));

        public IPluginModule? GetModule(string moduleId) => null;

        public TProvider? CreateMemberProvider<TProvider>(string moduleId, string providerType)
            where TProvider : class
        {
            var runtime = GetByModuleId(moduleId);
            if (runtime is null)
                return null;

            var contribution = runtime.IdentityProviders.FirstOrDefault(c =>
                string.Equals(c.ProviderType, providerType, StringComparison.Ordinal));

            if (contribution is null)
                return null;

            return _providerFactory(contribution) as TProvider;
        }
    }

    private sealed class FakeIdentityProviderPlugin : IIdentityProviderPlugin
    {
        private readonly IReadOnlyList<PluginClaimEntry> _claims;

        public FakeIdentityProviderPlugin(string providerKey, IReadOnlyList<PluginClaimEntry> claims)
        {
            ProviderKey  = providerKey;
            _claims      = claims;
        }

        public string ProviderKey  { get; }
        public string DisplayName  => ProviderKey;

        public IReadOnlyList<PluginFieldSchema> GetConfigSchema()
            => Array.Empty<PluginFieldSchema>();

        public Task<PluginExternalLoginTestResult> TestConnectionAsync(
            IReadOnlyDictionary<string, string> config,
            CancellationToken ct = default)
            => Task.FromResult(new PluginExternalLoginTestResult(true));

        public Task<IReadOnlyList<PluginClaimEntry>> MapClaimsAsync(
            PluginExternalLoginContext context,
            CancellationToken ct = default)
            => Task.FromResult(_claims);
    }

    private sealed class CapturingIdentityProviderPlugin : IIdentityProviderPlugin
    {
        public CapturingIdentityProviderPlugin(string providerKey) => ProviderKey = providerKey;

        public string ProviderKey  { get; }
        public string DisplayName  => ProviderKey;
        public PluginExternalLoginContext? LastContext { get; private set; }

        public IReadOnlyList<PluginFieldSchema> GetConfigSchema()
            => Array.Empty<PluginFieldSchema>();

        public Task<PluginExternalLoginTestResult> TestConnectionAsync(
            IReadOnlyDictionary<string, string> config,
            CancellationToken ct = default)
            => Task.FromResult(new PluginExternalLoginTestResult(true));

        public Task<IReadOnlyList<PluginClaimEntry>> MapClaimsAsync(
            PluginExternalLoginContext context,
            CancellationToken ct = default)
        {
            LastContext = context;
            return Task.FromResult<IReadOnlyList<PluginClaimEntry>>(Array.Empty<PluginClaimEntry>());
        }
    }
}
