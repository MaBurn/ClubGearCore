using System.Runtime.Loader;
using ClubGear.ArchitectureTests.Plugins;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// CORE-13 increment 1: the provider contribution seam. These tests pin the two behaviours the
/// backlog calls out explicitly — capability keys are an allowlist, and a collision is tolerated
/// rather than thrown.
/// </summary>
public class PluginProviderSeamTests
{
    // ----------------------------------------------------------------
    // Allowlist
    // ----------------------------------------------------------------

    [Fact]
    public void CapabilityKeys_AreAnAllowlist_NotFreeText()
    {
        Assert.True(PluginCapabilityKeys.IsKnown(PluginCapabilityKeys.StorageBlob));
        Assert.False(PluginCapabilityKeys.IsKnown("storage.blobb"));
        Assert.False(PluginCapabilityKeys.IsKnown("anything.at.all"));
    }

    [Fact]
    public void CapabilityKeys_EveryKnownKey_DeclaresAnArity()
    {
        foreach (var key in PluginCapabilityKeys.All)
        {
            // Throws if a key was added to the allowlist without deciding whether it is
            // single-active or fan-out, which is the distinction CORE-14 builds on.
            var arity = PluginCapabilityKeys.GetArity(key);
            Assert.True(Enum.IsDefined(arity));
        }
    }

    [Fact]
    public void CapabilityKeys_GetArity_RejectsUnknownKey()
        => Assert.Throws<ArgumentException>(() => PluginCapabilityKeys.GetArity("not.a.capability"));

    [Fact]
    public void CapabilityKeys_AreSeparateFromExtensionPoints()
    {
        // Two namespaces on purpose: a capability key says what a provider does, an extension
        // point says where a plugin shows up. Overlap would make the loader's two validations
        // ambiguous.
        Assert.Empty(PluginCapabilityKeys.All.Intersect(PluginExtensionPoints.All, StringComparer.Ordinal));
    }

    // ----------------------------------------------------------------
    // Resolution
    // ----------------------------------------------------------------

    [Fact]
    public void ResolveAll_ReturnsProvidersFromEveryRegisteredPlugin()
    {
        var registry = BuildRegistry(
            MakeRuntime("beta", (PluginCapabilityKeys.NotificationChannel, typeof(TestChannelProvider).FullName!)),
            MakeRuntime("alpha", (PluginCapabilityKeys.NotificationChannel, typeof(TestChannelProvider).FullName!)));

        var providers = registry.ResolveAll<ITestChannel>(PluginCapabilityKeys.NotificationChannel);

        Assert.Equal(2, providers.Count);
        // Ordered by module id, so a restart cannot reshuffle a fan-out.
        Assert.Equal(new[] { "alpha", "beta" }, providers.Select(p => p.ModuleId).ToArray());
        Assert.All(providers, p => Assert.NotNull(p.Instance));
    }

    [Fact]
    public void ResolveAll_IgnoresOtherCapabilities()
    {
        var registry = BuildRegistry(
            MakeRuntime("alpha", (PluginCapabilityKeys.StorageBlob, typeof(TestChannelProvider).FullName!)));

        Assert.Empty(registry.ResolveAll<ITestChannel>(PluginCapabilityKeys.NotificationChannel));
    }

    [Fact]
    public void Resolve_ReturnsAnEmptyLease_WhenNoPluginProvidesTheCapability()
    {
        var registry = BuildRegistry(MakeRuntime("alpha"));

        // CORE-18: the caller gets a lease, not a bare instance. "Nobody provides it" is an empty
        // lease carrying the reason, so the caller can still fall back to the core default.
        using var resolved = registry.Resolve<ITestChannel>(PluginCapabilityKeys.StorageBlob);

        Assert.Null(resolved.Instance);
        Assert.NotNull(resolved.Error);
    }

    [Fact]
    public void Resolve_ToleratesACollision_InsteadOfThrowing()
    {
        // The finding this seam exists to fix: NotificationService builds a name-keyed dictionary
        // with ToDictionary in its constructor, so two plugins claiming one key is a scope
        // construction failure. Here it must be a logged, deterministic choice.
        var registry = BuildRegistry(
            MakeRuntime("zulu", (PluginCapabilityKeys.StorageBlob, typeof(TestChannelProvider).FullName!)),
            MakeRuntime("alpha", (PluginCapabilityKeys.StorageBlob, typeof(TestChannelProvider).FullName!)));

        using var resolved = registry.Resolve<ITestChannel>(PluginCapabilityKeys.StorageBlob);

        Assert.NotNull(resolved.Instance);
        Assert.Equal("alpha", resolved.ModuleId);
        Assert.Equal("alpha", registry.GetRegistrations(PluginCapabilityKeys.StorageBlob)[0].ModuleId);
    }

    [Fact]
    public void ResolveAll_SkipsAProviderThatDoesNotImplementTheContract()
    {
        // An authoring error in the plugin must not take down the caller.
        var registry = BuildRegistry(
            MakeRuntime("alpha", (PluginCapabilityKeys.NotificationChannel, typeof(NotAChannel).FullName!)),
            MakeRuntime("beta", (PluginCapabilityKeys.NotificationChannel, typeof(TestChannelProvider).FullName!)));

        var providers = registry.ResolveAll<ITestChannel>(PluginCapabilityKeys.NotificationChannel);

        Assert.Single(providers);
        Assert.Equal("beta", providers[0].ModuleId);
    }

    [Fact]
    public void ResolveAll_SkipsAProviderTypeThatDoesNotExist()
    {
        var registry = BuildRegistry(
            MakeRuntime("alpha", (PluginCapabilityKeys.NotificationChannel, "Acme.Missing.Provider")));

        Assert.Empty(registry.ResolveAll<ITestChannel>(PluginCapabilityKeys.NotificationChannel));
    }

    [Fact]
    public void GetRegistrations_DoesNotInstantiateAnything()
    {
        TestChannelProvider.ConstructionCount = 0;

        var registry = BuildRegistry(
            MakeRuntime("alpha", (PluginCapabilityKeys.StorageBlob, typeof(TestChannelProvider).FullName!)));

        var registrations = registry.GetRegistrations(PluginCapabilityKeys.StorageBlob);

        Assert.Single(registrations);
        Assert.Equal(0, TestChannelProvider.ConstructionCount);
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static IPluginProviderRegistry BuildRegistry(params RegisteredPluginRuntime[] runtimes)
    {
        var runtimeRegistry = new PluginRegistry();
        foreach (var runtime in runtimes)
        {
            // The provider type is looked up in the module's own assembly, so the no-op module has
            // to live in this test assembly alongside the test providers.
            runtimeRegistry.RegisterAsync(runtime, new SeamTestModule(runtime.ModuleId), AssemblyLoadContext.Default).GetAwaiter().GetResult();
        }

        return new PluginProviderRegistry(
            runtimeRegistry,
            TestContributionActivatorFactory.Create(runtimeRegistry, new StubPluginRuntimeAdapter()),
            NullLogger<PluginProviderRegistry>.Instance);
    }

    private static RegisteredPluginRuntime MakeRuntime(
        string moduleId,
        params (string CapabilityKey, string ProviderType)[] services)
        => new(
            moduleId,
            moduleId,
            new Version(1, 0, 0),
            $"test:{moduleId}",
            Array.Empty<PluginRouteContribution>(),
            services.Select(s => new PluginServiceContribution(s.CapabilityKey, s.ProviderType)).ToArray(),
            Array.Empty<PluginMemberProviderContribution>(),
            Array.Empty<PluginBackgroundJobContribution>(),
            Array.Empty<PluginNavEntry>(),
            Array.Empty<PluginAuditSinkContribution>(),
            Array.Empty<PluginIdentityProviderContribution>(),
            Array.Empty<PluginSelfServiceProfileProviderContribution>());

    public interface ITestChannel
    {
        string Send(string message);
    }

    public sealed class TestChannelProvider : ITestChannel
    {
        public static int ConstructionCount;

        // CORE-18: the canonical constructor. There is no parameterless one any more, which is
        // exactly why GetRegistrations_DoesNotInstantiateAnything is still meaningful.
        public TestChannelProvider(IPluginHostContext host)
        {
            Host = host;
            ConstructionCount++;
        }

        public IPluginHostContext Host { get; }

        public string Send(string message) => message;
    }

    public sealed class NotAChannel
    {
        public NotAChannel(IPluginHostContext host)
        {
            Host = host;
        }

        public IPluginHostContext Host { get; }
    }

    private sealed class SeamTestModule : IPluginModule
    {
        public SeamTestModule(string moduleId)
        {
            Manifest = new PluginManifest(
                Key: moduleId,
                Name: moduleId,
                Version: new Version(1, 0, 0),
                Author: "tests",
                License: "proprietary",
                EntryPoint: typeof(SeamTestModule).FullName!,
                RequiredCoreVersion: ">=1.13.0",
                Permissions: Array.Empty<string>(),
                ExtensionPoints: Array.Empty<string>());
        }

        public PluginManifest Manifest { get; }

        public void RegisterContributions(IPluginContributionSink sink)
        {
        }
    }
}
