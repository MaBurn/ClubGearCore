using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClubGear.ArchitectureTests.Plugins;

/// <summary>
/// CORE-18. Builds the real <see cref="PluginContributionActivator"/> over a minimal container, so
/// tests exercise the actual canonical-constructor path (<c>T(IPluginHostContext)</c>) instead of a
/// stub that would hide it.
/// </summary>
public static class TestContributionActivatorFactory
{
    public static PluginContributionActivator Create(
        IPluginRegistryReader registryReader,
        IPluginRuntimeAdapter runtimeAdapter,
        IPluginActivityTracker? activityTracker = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(runtimeAdapter);

        var provider = services.BuildServiceProvider();

        return new PluginContributionActivator(
            registryReader,
            activityTracker ?? new PluginActivityTracker(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PluginContributionActivator>.Instance);
    }
}
