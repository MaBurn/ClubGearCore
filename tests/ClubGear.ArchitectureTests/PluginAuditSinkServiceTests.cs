using System.Runtime.Loader;
using ClubGear.ArchitectureTests.Plugins;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.AuditSink;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginAuditSinkServiceTests
{
    // ---------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------

    private static PluginAuditEvent MakeEvent()
        => new PluginAuditEvent("test.action", "actor1", "source1", "Member", "42", DateTime.UtcNow);

    private static RegisteredPluginRuntime MakeRuntime(
        string moduleId,
        IReadOnlyList<PluginAuditSinkContribution> sinks)
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
            sinks,
            Array.Empty<PluginIdentityProviderContribution>(),
            Array.Empty<PluginSelfServiceProfileProviderContribution>());

    // ---------------------------------------------------------------
    // test (a): no registered sinks — DispatchAsync completes without any provider call
    // ---------------------------------------------------------------

    [Fact]
    public async Task DispatchAsync_CompletesWithoutProviderCall_WhenNoSinksRegistered()
    {
        var registry = new StubRegistryReader(Array.Empty<RegisteredPluginRuntime>(), _ => null);
        var service = new PluginAuditSinkService(registry, new StubPluginContributionActivator(registry.CreateProvider), NullLogger<PluginAuditSinkService>.Instance);

        // Must complete without throwing.
        await service.DispatchAsync(MakeEvent());

        Assert.Equal(0, registry.CreateProviderCallCount);
    }

    // ---------------------------------------------------------------
    // test (b): one sink — OnAuditEventAsync is called once with the correct event
    // ---------------------------------------------------------------

    [Fact]
    public async Task DispatchAsync_CallsOnAuditEventAsync_OnceWithCorrectEvent()
    {
        var auditEvent = MakeEvent();
        var capturingProvider = new CapturingAuditSinkProvider();

        var runtime = MakeRuntime("plugin.a", [new PluginAuditSinkContribution("ProviderA")]);
        var registry = new StubRegistryReader([runtime], contribution => capturingProvider);
        var service = new PluginAuditSinkService(registry, new StubPluginContributionActivator(registry.CreateProvider), NullLogger<PluginAuditSinkService>.Instance);

        await service.DispatchAsync(auditEvent);

        Assert.Equal(1, capturingProvider.CallCount);
        Assert.Same(auditEvent, capturingProvider.LastEvent);
    }

    // ---------------------------------------------------------------
    // test (c): two sinks where the first throws — second sink is still
    //           called and no exception escapes DispatchAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task DispatchAsync_ContinuesToNextSink_WhenFirstSinkThrows()
    {
        var auditEvent = MakeEvent();
        var throwingProvider = new ThrowingAuditSinkProvider();
        var secondProvider = new CapturingAuditSinkProvider();

        var runtime = MakeRuntime("plugin.a", [
            new PluginAuditSinkContribution("ThrowingProvider"),
            new PluginAuditSinkContribution("SecondProvider")
        ]);

        var providers = new Dictionary<string, IAuditSinkProvider>(StringComparer.Ordinal)
        {
            ["ThrowingProvider"] = throwingProvider,
            ["SecondProvider"] = secondProvider
        };

        var registry = new StubRegistryReader(
            [runtime],
            contribution => providers.TryGetValue(contribution.ProviderType, out var p) ? p : null);

        var service = new PluginAuditSinkService(registry, new StubPluginContributionActivator(registry.CreateProvider), NullLogger<PluginAuditSinkService>.Instance);

        // Must not throw despite first sink throwing.
        await service.DispatchAsync(auditEvent);

        Assert.Equal(1, throwingProvider.CallCount);
        Assert.Equal(1, secondProvider.CallCount);
    }

    // ---------------------------------------------------------------
    // test (d): CreateMemberProvider returns null — contribution is
    //           skipped silently and no exception is thrown
    // ---------------------------------------------------------------

    [Fact]
    public async Task DispatchAsync_SkipsContribution_WhenCreateMemberProviderReturnsNull()
    {
        var runtime = MakeRuntime("plugin.a", [new PluginAuditSinkContribution("UnresolvableProvider")]);
        var registry = new StubRegistryReader([runtime], _ => null);
        var service = new PluginAuditSinkService(registry, new StubPluginContributionActivator(registry.CreateProvider), NullLogger<PluginAuditSinkService>.Instance);

        // Must complete without throwing.
        await service.DispatchAsync(MakeEvent());

        // CreateMemberProvider was called once (for the single contribution) but returned null.
        Assert.Equal(1, registry.CreateProviderCallCount);
    }

    // ---------------------------------------------------------------
    // stub / fake helpers
    // ---------------------------------------------------------------

    private sealed class StubRegistryReader : IPluginRegistryReader
    {
        private readonly IReadOnlyList<RegisteredPluginRuntime> _runtimes;
        private readonly Func<PluginAuditSinkContribution, IAuditSinkProvider?> _providerFactory;
        private int _createMemberProviderCallCount;

        public StubRegistryReader(
            IReadOnlyList<RegisteredPluginRuntime> runtimes,
            Func<PluginAuditSinkContribution, IAuditSinkProvider?> providerFactory)
        {
            _runtimes = runtimes;
            _providerFactory = providerFactory;
        }

        public int CreateProviderCallCount => _createMemberProviderCallCount;

        public IReadOnlyList<RegisteredPluginRuntime> GetRegisteredPlugins() => _runtimes;

        public RegisteredPluginRuntime? GetByModuleId(string moduleId)
            => _runtimes.FirstOrDefault(r => string.Equals(r.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase));

        public IPluginModule? GetModule(string moduleId) => null;

        public Type? ResolveContributionType(string moduleId, string contributionType) => null;

        /// <summary>
        /// CORE-18. Construction moved to <see cref="IPluginContributionActivator"/>; this stub
        /// keeps the same lookup so the tests still describe "which sink does this module offer",
        /// and counts the calls the way it used to.
        /// </summary>
        public object? CreateProvider(string moduleId, string providerType)
        {
            Interlocked.Increment(ref _createMemberProviderCallCount);

            var runtime = GetByModuleId(moduleId);
            if (runtime is null)
            {
                return null;
            }

            var contribution = runtime.AuditSinks.FirstOrDefault(s =>
                string.Equals(s.ProviderType, providerType, StringComparison.Ordinal));

            if (contribution is null)
            {
                return null;
            }

            return _providerFactory(contribution);
        }
    }

    private sealed class CapturingAuditSinkProvider : IAuditSinkProvider
    {
        private int _callCount;
        public int CallCount => _callCount;
        public PluginAuditEvent? LastEvent { get; private set; }

        public Task OnAuditEventAsync(PluginAuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            LastEvent = auditEvent;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingAuditSinkProvider : IAuditSinkProvider
    {
        private int _callCount;
        public int CallCount => _callCount;

        public Task OnAuditEventAsync(PluginAuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            throw new InvalidOperationException("Simulated audit sink failure.");
        }
    }
}
