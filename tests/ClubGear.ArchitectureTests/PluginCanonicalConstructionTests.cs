using System.Runtime.Loader;
using ClubGear.ArchitectureTests.Fixtures;
using ClubGear.ArchitectureTests.Plugins;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Installation;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// CORE-18. Contributions are host-constructed through a single canonical public constructor,
/// <c>T(IPluginHostContext)</c>. These tests pin the two halves of that rule: the load-time gate
/// that refuses anything else, and the lease that construction now goes through.
/// </summary>
public sealed class PluginCanonicalConstructionTests
{
    private static FileSystemPluginPackageStore CreatePackageStore()
        => new(Path.Combine(
            Path.GetTempPath(),
            "clubgear-canonical-construction-tests",
            Guid.NewGuid().ToString("N")));

    [Fact]
    public async Task Loader_RejectsContributionType_WithoutTheCanonicalConstructor()
    {
        var packageStore = CreatePackageStore();

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            packageStore,
            "plugin.parameterless.contribution",
            "Parameterless Contribution Plugin",
            typeof(ForbiddenPlugin.ParameterlessContributionPluginModule).FullName!);

        var loader = new PluginLoader(packageStore, NullLogger<PluginLoader>.Instance);
        var loadResult = await loader.LoadAsync(record);

        Assert.False(loadResult.Success);
        Assert.Null(loadResult.LoadedPlugin);

        // The message has to name the module and the type: the administrator's next step is to
        // ask that plugin's author for a build, and they need to know which type is at fault.
        Assert.Contains("plugin.parameterless.contribution", loadResult.Error!, StringComparison.Ordinal);
        Assert.Contains(
            typeof(ForbiddenPlugin.ParameterlessContributionCapability).FullName!,
            loadResult.Error!,
            StringComparison.Ordinal);
        Assert.Contains("IPluginHostContext", loadResult.Error!, StringComparison.Ordinal);
        Assert.Contains("1.13.0", loadResult.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rent_ConstructsTheContribution_ThroughTheCanonicalConstructor()
    {
        var registry = new PluginRegistry();
        var module = new RuntimeLoadedPluginModuleA();
        await RegisterAsync(registry, module);

        var activator = TestContributionActivatorFactory.Create(registry, new StubPluginRuntimeAdapter());

        await using var lease = activator.Rent<IMemberDetailCardProvider>(
            module.Manifest.ModuleId,
            typeof(RuntimeLoadedDetailCardProviderA).FullName!);

        Assert.Null(lease.Error);
        var provider = Assert.IsType<RuntimeLoadedDetailCardProviderA>(lease.Instance);

        // The host context the constructor received is the one the runtime adapter built.
        Assert.NotNull(provider.HostContext);
        Assert.Equal(module.Manifest.ModuleId, lease.ModuleId);
    }

    [Fact]
    public async Task Rent_ReturnsAnEmptyLease_WithADrainingError_OnceTheModuleIsSealed()
    {
        var tracker = new PluginActivityTracker();
        var registry = new PluginRegistry();
        var module = new RuntimeLoadedPluginModuleA();
        await RegisterAsync(registry, module);

        var activator = TestContributionActivatorFactory.Create(
            registry, new StubPluginRuntimeAdapter(), tracker);

        // Deactivation seals the module before it drains (CORE-17). No new plugin code may start
        // after that point, or the drain could never finish.
        tracker.Seal(module.Manifest.ModuleId);

        await using var lease = activator.Rent<IMemberDetailCardProvider>(
            module.Manifest.ModuleId,
            typeof(RuntimeLoadedDetailCardProviderA).FullName!);

        Assert.Null(lease.Instance);
        Assert.NotNull(lease.Error);
        Assert.Contains(module.Manifest.ModuleId, lease.Error!, StringComparison.Ordinal);
        Assert.Contains("deaktiviert", lease.Error!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, tracker.GetInFlight(module.Manifest.ModuleId));
    }

    [Fact]
    public void Rent_ReturnsAnEmptyLease_WhenTheModuleIsNotActive()
    {
        var registry = new PluginRegistry();
        var activator = TestContributionActivatorFactory.Create(registry, new StubPluginRuntimeAdapter());

        using var lease = activator.Rent<IMemberDetailCardProvider>(
            "plugin.that.is.not.registered",
            typeof(RuntimeLoadedDetailCardProviderA).FullName!);

        Assert.Null(lease.Instance);
        Assert.Contains("plugin.that.is.not.registered", lease.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rent_CountsTheContributionAsInFlight_UntilTheLeaseIsDisposed()
    {
        var tracker = new PluginActivityTracker();
        var registry = new PluginRegistry();
        var module = new RuntimeLoadedPluginModuleA();
        await RegisterAsync(registry, module);

        var activator = TestContributionActivatorFactory.Create(
            registry, new StubPluginRuntimeAdapter(), tracker);

        var lease = activator.Rent<IMemberDetailCardProvider>(
            module.Manifest.ModuleId,
            typeof(RuntimeLoadedDetailCardProviderA).FullName!);

        // This is what makes the CORE-17 drain exact rather than approximate: while a caller holds
        // a contribution, the module counts as busy.
        Assert.NotNull(lease.Instance);
        Assert.Equal(1, tracker.GetInFlight(module.Manifest.ModuleId));

        await lease.DisposeAsync();

        Assert.Equal(0, tracker.GetInFlight(module.Manifest.ModuleId));
    }

    [Fact]
    public async Task Rent_ReleasesTheActivityToken_WhenTheTypeCannotBeResolved()
    {
        var tracker = new PluginActivityTracker();
        var registry = new PluginRegistry();
        var module = new RuntimeLoadedPluginModuleA();
        await RegisterAsync(registry, module);

        var activator = TestContributionActivatorFactory.Create(
            registry, new StubPluginRuntimeAdapter(), tracker);

        await using var lease = activator.Rent<IMemberDetailCardProvider>(
            module.Manifest.ModuleId,
            "ClubGear.ArchitectureTests.Plugins.TypeThatDoesNotExist");

        Assert.Null(lease.Instance);
        Assert.NotNull(lease.Error);

        // A failed rent that leaked its token would make the module undrainable forever.
        Assert.Equal(0, tracker.GetInFlight(module.Manifest.ModuleId));
    }

    private static Task RegisterAsync(PluginRegistry registry, IPluginModule module)
    {
        var runtime = new RegisteredPluginRuntime(
            module.Manifest.ModuleId,
            module.Manifest.DisplayName,
            module.Manifest.PluginVersion,
            $"test:{module.Manifest.ModuleId}",
            Array.Empty<PluginRouteContribution>(),
            Array.Empty<PluginServiceContribution>(),
            Array.Empty<PluginMemberProviderContribution>(),
            Array.Empty<PluginBackgroundJobContribution>(),
            Array.Empty<PluginNavEntry>(),
            Array.Empty<PluginAuditSinkContribution>(),
            Array.Empty<PluginIdentityProviderContribution>(),
            Array.Empty<PluginSelfServiceProfileProviderContribution>());

        return registry.RegisterAsync(runtime, module, AssemblyLoadContext.Default);
    }
}
