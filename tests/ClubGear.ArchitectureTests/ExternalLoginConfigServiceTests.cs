using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.ExternalLogin;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class ExternalLoginConfigServiceTests
{
    // ---------------------------------------------------------------
    // helpers — stub ISystemConfigService
    // ---------------------------------------------------------------

    private sealed class StubSystemConfigService : ISystemConfigService
    {
        /// <summary>In-memory store keyed by (Section, Name).</summary>
        private readonly Dictionary<(string Section, string Name), string> _store;

        /// <summary>Records all UpsertAsync calls in order.</summary>
        public List<(string Section, string Name, string Value)> UpsertCalls { get; } = new();

        public StubSystemConfigService(
            Dictionary<(string, string), string>? initialValues = null)
        {
            _store = initialValues ?? new();
        }

        public Task<string?> GetValueAsync(string section, string name, CancellationToken ct = default)
        {
            _store.TryGetValue((section, name), out var val);
            return Task.FromResult(val);
        }

        public Task<IReadOnlyList<SystemConfigEntry>> GetBySectionAsync(string section, CancellationToken ct = default)
        {
            var entries = _store
                .Where(kv => string.Equals(kv.Key.Section, section, StringComparison.OrdinalIgnoreCase))
                .Select(kv => new SystemConfigEntry { Section = kv.Key.Section, Name = kv.Key.Name, Value = kv.Value })
                .ToList();
            return Task.FromResult<IReadOnlyList<SystemConfigEntry>>(entries);
        }

        public Task UpsertAsync(string section, string name, string value, string description, CancellationToken ct = default)
        {
            _store[(section, name)] = value;
            UpsertCalls.Add((section, name, value));
            return Task.CompletedTask;
        }

        // Unused members
        public Task<IReadOnlyList<SystemConfigEntry>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SystemConfigEntry>>(Array.Empty<SystemConfigEntry>());
        public Task UpsertManyAsync(IEnumerable<SystemConfigEntry> entries, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task DeleteByIdAsync(int id, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    // ---------------------------------------------------------------
    // helpers — stub IPluginRegistryReader
    // ---------------------------------------------------------------

    private sealed class StubRegistryReader : IPluginRegistryReader
    {
        private readonly IReadOnlyList<RegisteredPluginRuntime> _runtimes;
        private readonly Func<string, string, IIdentityProviderPlugin?> _pluginFactory;

        public StubRegistryReader(
            IReadOnlyList<RegisteredPluginRuntime> runtimes,
            Func<string, string, IIdentityProviderPlugin?>? pluginFactory = null)
        {
            _runtimes      = runtimes;
            _pluginFactory = pluginFactory ?? ((_, _) => null);
        }

        public IReadOnlyList<RegisteredPluginRuntime> GetRegisteredPlugins() => _runtimes;

        public RegisteredPluginRuntime? GetByModuleId(string moduleId)
            => _runtimes.FirstOrDefault(r => string.Equals(r.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase));

        public IPluginModule? GetModule(string moduleId) => null;

        public TProvider? CreateMemberProvider<TProvider>(string moduleId, string providerType)
            where TProvider : class
        {
            return _pluginFactory(moduleId, providerType) as TProvider;
        }
    }

    // ---------------------------------------------------------------
    // helper — build RegisteredPluginRuntime with identity providers
    // ---------------------------------------------------------------

    private static RegisteredPluginRuntime MakeRuntime(
        string moduleId,
        IReadOnlyList<PluginIdentityProviderContribution> providers)
        => new RegisteredPluginRuntime(
            moduleId,
            DisplayName:     moduleId,
            PluginVersion:   new Version(1, 0, 0),
            LoadContextName: $"test:{moduleId}",
            Routes:          Array.Empty<PluginRouteContribution>(),
            Services:        Array.Empty<PluginServiceContribution>(),
            MemberProviders: Array.Empty<PluginMemberProviderContribution>(),
            BackgroundJobs:  Array.Empty<PluginBackgroundJobContribution>(),
            NavEntries:      Array.Empty<PluginNavEntry>(),
            AuditSinks:      Array.Empty<PluginAuditSinkContribution>(),
            IdentityProviders: providers,
            SelfServiceProfileProviders: Array.Empty<PluginSelfServiceProfileProviderContribution>());

    // ---------------------------------------------------------------
    // helper — build service under test
    // ---------------------------------------------------------------

    private static ExternalLoginConfigService Create(
        StubSystemConfigService configService,
        StubRegistryReader registryReader)
        => new ExternalLoginConfigService(configService, registryReader);

    // ---------------------------------------------------------------
    // GetConfigAsync tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetConfigAsync_ReturnsEmptyDictionary_WhenNoValuesStored()
    {
        var config   = new StubSystemConfigService();
        var registry = new StubRegistryReader(Array.Empty<RegisteredPluginRuntime>());
        var sut      = Create(config, registry);

        var result = await sut.GetConfigAsync("someprovider");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetConfigAsync_ReturnsStoredValues_ForProviderSection()
    {
        var config = new StubSystemConfigService(new Dictionary<(string, string), string>
        {
            { ("externallogin.myprovider", "authority"),    "https://idp.example.com" },
            { ("externallogin.myprovider", "clientid"),     "my-client"               },
            { ("externallogin.myprovider", "clientsecret"), "my-secret"               },
            { ("externallogin.otherprovider", "authority"), "https://other.example.com" }, // different provider, must not appear
        });
        var registry = new StubRegistryReader(Array.Empty<RegisteredPluginRuntime>());
        var sut      = Create(config, registry);

        var result = await sut.GetConfigAsync("myprovider");

        Assert.Equal(3, result.Count);
        Assert.Equal("https://idp.example.com", result["authority"]);
        Assert.Equal("my-client",               result["clientid"]);
        Assert.Equal("my-secret",               result["clientsecret"]);
        Assert.False(result.ContainsKey("otherprovider"));
    }

    // ---------------------------------------------------------------
    // SaveConfigAsync tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task SaveConfigAsync_CallsUpsertForEachConfigEntry()
    {
        var config   = new StubSystemConfigService();
        var registry = new StubRegistryReader(Array.Empty<RegisteredPluginRuntime>());
        var sut      = Create(config, registry);

        var values = new Dictionary<string, string>
        {
            ["authority"]    = "https://idp.example.com",
            ["clientid"]     = "my-client",
            ["clientsecret"] = "my-secret",
        };

        await sut.SaveConfigAsync("myprovider", values);

        Assert.Equal(3, config.UpsertCalls.Count);

        // Every upsert must target the correct section.
        foreach (var (section, _, _) in config.UpsertCalls)
            Assert.Equal("externallogin.myprovider", section);

        // The saved names/values must match the input dictionary.
        foreach (var (name, value) in values)
        {
            Assert.Contains(config.UpsertCalls,
                call => call.Name == name && call.Value == value);
        }
    }

    [Fact]
    public async Task SaveConfigAsync_RoundTrip_GetConfigAsync_ReturnsCorrectValues()
    {
        var config   = new StubSystemConfigService();
        var registry = new StubRegistryReader(Array.Empty<RegisteredPluginRuntime>());
        var sut      = Create(config, registry);

        var valuesToSave = new Dictionary<string, string>
        {
            ["authority"]    = "https://roundtrip.example.com",
            ["clientid"]     = "rt-client",
            ["clientsecret"] = "rt-secret",
        };

        await sut.SaveConfigAsync("rtprovider", valuesToSave);
        var loaded = await sut.GetConfigAsync("rtprovider");

        Assert.Equal(valuesToSave.Count, loaded.Count);
        foreach (var (name, expected) in valuesToSave)
            Assert.Equal(expected, loaded[name]);
    }

    // ---------------------------------------------------------------
    // GetAllDeclaredProvidersAsync tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAllDeclaredProvidersAsync_ReturnsEmpty_WhenNoPluginsLoaded()
    {
        var config   = new StubSystemConfigService();
        var registry = new StubRegistryReader(Array.Empty<RegisteredPluginRuntime>());
        var sut      = Create(config, registry);

        var result = await sut.GetAllDeclaredProvidersAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllDeclaredProvidersAsync_ReturnsOneEntry_PerDeclaredProvider()
    {
        var runtime = MakeRuntime("plugin.idp", new[]
        {
            new PluginIdentityProviderContribution("myprovider", "MyIdpPlugin"),
        });
        var config   = new StubSystemConfigService();
        var registry = new StubRegistryReader(new[] { runtime });
        var sut      = Create(config, registry);

        var result = await sut.GetAllDeclaredProvidersAsync();

        Assert.Single(result);
        Assert.Equal("myprovider",  result[0].ProviderKey);
        Assert.Equal("plugin.idp",  result[0].ModuleId);
    }

    [Fact]
    public async Task GetAllDeclaredProvidersAsync_UsesPluginDisplayName_WhenProviderInstantiates()
    {
        const string expectedDisplayName = "My Identity Provider";

        var runtime = MakeRuntime("plugin.idp", new[]
        {
            new PluginIdentityProviderContribution("myprovider", "MyIdpPlugin"),
        });

        var fakePlugin = new FakeIdentityProviderPlugin("myprovider", expectedDisplayName);

        var config   = new StubSystemConfigService();
        var registry = new StubRegistryReader(
            new[] { runtime },
            (_, providerType) => providerType == "MyIdpPlugin" ? fakePlugin : null);
        var sut = Create(config, registry);

        var result = await sut.GetAllDeclaredProvidersAsync();

        Assert.Single(result);
        Assert.Equal(expectedDisplayName, result[0].DisplayName);
    }

    [Fact]
    public async Task GetAllDeclaredProvidersAsync_IsEnabled_ReflectsStoredEnabledFlag()
    {
        var runtime = MakeRuntime("plugin.idp", new[]
        {
            new PluginIdentityProviderContribution("enabled-provider",  "PluginA"),
            new PluginIdentityProviderContribution("disabled-provider", "PluginB"),
        });

        var config = new StubSystemConfigService(new Dictionary<(string, string), string>
        {
            { ("externallogin.enabled-provider",  "enabled"), "true"  },
            { ("externallogin.disabled-provider", "enabled"), "false" },
        });
        var registry = new StubRegistryReader(new[] { runtime });
        var sut      = Create(config, registry);

        var result = await sut.GetAllDeclaredProvidersAsync();

        Assert.Equal(2, result.Count);
        var enabled  = result.First(p => p.ProviderKey == "enabled-provider");
        var disabled = result.First(p => p.ProviderKey == "disabled-provider");
        Assert.True(enabled.IsEnabled);
        Assert.False(disabled.IsEnabled);
    }

    [Fact]
    public async Task GetAllDeclaredProvidersAsync_IsEnabled_FalseByDefault_WhenNoFlagStored()
    {
        var runtime = MakeRuntime("plugin.idp", new[]
        {
            new PluginIdentityProviderContribution("myprovider", "MyIdpPlugin"),
        });
        var config   = new StubSystemConfigService(); // no enabled flag stored
        var registry = new StubRegistryReader(new[] { runtime });
        var sut      = Create(config, registry);

        var result = await sut.GetAllDeclaredProvidersAsync();

        Assert.Single(result);
        Assert.False(result[0].IsEnabled);
    }

    [Fact]
    public async Task GetAllDeclaredProvidersAsync_AggregatesProviders_AcrossMultiplePlugins()
    {
        var runtimeA = MakeRuntime("plugin.a", new[]
        {
            new PluginIdentityProviderContribution("provider-a", "ProviderTypeA"),
        });
        var runtimeB = MakeRuntime("plugin.b", new[]
        {
            new PluginIdentityProviderContribution("provider-b", "ProviderTypeB"),
        });

        var config   = new StubSystemConfigService();
        var registry = new StubRegistryReader(new[] { runtimeA, runtimeB });
        var sut      = Create(config, registry);

        var result = await sut.GetAllDeclaredProvidersAsync();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.ProviderKey == "provider-a" && p.ModuleId == "plugin.a");
        Assert.Contains(result, p => p.ProviderKey == "provider-b" && p.ModuleId == "plugin.b");
    }

    // ---------------------------------------------------------------
    // TestConnectionAsync tests
    // ---------------------------------------------------------------

    [Fact]
    public async Task TestConnectionAsync_ReturnsFailure_WhenProviderKeyNotFound()
    {
        var config   = new StubSystemConfigService();
        var registry = new StubRegistryReader(Array.Empty<RegisteredPluginRuntime>());
        var sut      = Create(config, registry);

        var result = await sut.TestConnectionAsync("unknown-provider");

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_ReturnsPluginResult_WhenProviderFound()
    {
        var expectedResult = new PluginExternalLoginTestResult(Success: true, Message: "OK");
        var fakePlugin     = new FakeIdentityProviderPlugin("myprovider", "My Provider", testResult: expectedResult);

        var runtime = MakeRuntime("plugin.idp", new[]
        {
            new PluginIdentityProviderContribution("myprovider", "MyIdpPlugin"),
        });

        var config = new StubSystemConfigService(new Dictionary<(string, string), string>
        {
            { ("externallogin.myprovider", "authority"),    "https://idp.example.com" },
            { ("externallogin.myprovider", "clientid"),     "my-client"               },
        });
        var registry = new StubRegistryReader(
            new[] { runtime },
            (_, providerType) => providerType == "MyIdpPlugin" ? fakePlugin : null);
        var sut = Create(config, registry);

        var result = await sut.TestConnectionAsync("myprovider");

        Assert.True(result.Success);
        Assert.Equal("OK", result.Message);

        // Plugin must have received the saved config values.
        Assert.NotNull(fakePlugin.LastTestConfig);
        Assert.Equal("https://idp.example.com", fakePlugin.LastTestConfig["authority"]);
    }

    // ---------------------------------------------------------------
    // fake IIdentityProviderPlugin
    // ---------------------------------------------------------------

    private sealed class FakeIdentityProviderPlugin : IIdentityProviderPlugin
    {
        private readonly PluginExternalLoginTestResult _testResult;

        public FakeIdentityProviderPlugin(
            string providerKey,
            string displayName,
            PluginExternalLoginTestResult? testResult = null)
        {
            ProviderKey  = providerKey;
            DisplayName  = displayName;
            _testResult  = testResult ?? new PluginExternalLoginTestResult(true);
        }

        public string ProviderKey  { get; }
        public string DisplayName  { get; }

        public IReadOnlyDictionary<string, string>? LastTestConfig { get; private set; }

        public IReadOnlyList<PluginFieldSchema> GetConfigSchema()
            => Array.Empty<PluginFieldSchema>();

        public Task<PluginExternalLoginTestResult> TestConnectionAsync(
            IReadOnlyDictionary<string, string> config,
            CancellationToken ct = default)
        {
            LastTestConfig = config;
            return Task.FromResult(_testResult);
        }

        public Task<IReadOnlyList<PluginClaimEntry>> MapClaimsAsync(
            PluginExternalLoginContext context,
            CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PluginClaimEntry>>(Array.Empty<PluginClaimEntry>());
    }
}
