using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// DI integration tests for Slice 3: verifies that IPluginBackgroundJobRunner is
/// registered as a singleton and resolves to the same instance on consecutive calls.
/// </summary>
public sealed class PluginBackgroundJobRunnerDiTests
{
    // ---------------------------------------------------------------------------
    // Test 1: IPluginBackgroundJobRunner resolves to the same singleton instance
    // ---------------------------------------------------------------------------

    [Fact]
    public void IPluginBackgroundJobRunner_IsSingleton_SameInstanceOnConsecutiveResolutions()
    {
        // Arrange — build the minimal service collection that satisfies
        // PluginBackgroundJobRunner's constructor (IPluginRuntimeRegistry,
        // IServiceScopeFactory, IHostApplicationLifetime).
        var services = new ServiceCollection();

        services.AddSingleton<IPluginRuntimeRegistry, PluginRegistry>();
        services.AddSingleton<IPluginBackgroundJobRunner, PluginBackgroundJobRunner>();
        services.AddSingleton<IHostApplicationLifetime, StubHostApplicationLifetime>();

        // IServiceScopeFactory is provided automatically by BuildServiceProvider.
        using var provider = services.BuildServiceProvider();

        // Act — resolve twice from the root container.
        var first = provider.GetRequiredService<IPluginBackgroundJobRunner>();
        var second = provider.GetRequiredService<IPluginBackgroundJobRunner>();

        // Assert — must be the same singleton instance.
        Assert.Same(first, second);
    }

    // ---------------------------------------------------------------------------
    // Test 2: resolved instance is the concrete PluginBackgroundJobRunner type
    // ---------------------------------------------------------------------------

    [Fact]
    public void IPluginBackgroundJobRunner_ResolvesToConcreteType_PluginBackgroundJobRunner()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IPluginRuntimeRegistry, PluginRegistry>();
        services.AddSingleton<IPluginBackgroundJobRunner, PluginBackgroundJobRunner>();
        services.AddSingleton<IHostApplicationLifetime, StubHostApplicationLifetime>();

        using var provider = services.BuildServiceProvider();

        var instance = provider.GetRequiredService<IPluginBackgroundJobRunner>();

        Assert.IsType<PluginBackgroundJobRunner>(instance);
    }

    // ---------------------------------------------------------------------------
    // Stub
    // ---------------------------------------------------------------------------

    private sealed class StubHostApplicationLifetime : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => CancellationToken.None;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() { }
    }
}
