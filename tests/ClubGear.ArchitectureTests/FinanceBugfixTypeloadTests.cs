using ClubGear.ArchitectureTests.Fixtures;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Installation;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// CORE-18. This suite used to prove that <c>PluginRegistry.CreateMemberProvider</c> could
/// instantiate a packaged provider. That member is gone: contributions are host-constructed
/// through <see cref="IPluginContributionActivator"/> as <c>T(IPluginHostContext)</c>. The two
/// tests below pin the replacement — the contract floor at load time, and the rented provider.
/// </summary>
public sealed class FinanceBugfixTypeloadTests
{
    [Fact]
    public async Task LoadAsync_RejectsPlugin_WhenManifestIsBelowTheContractFloor()
    {
        var packageStore = new FileSystemPluginPackageStore(
            Path.Combine(Path.GetTempPath(), "clubgear-finance-bugfix-typeload-tests", Guid.NewGuid().ToString("N")));

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            packageStore,
            "plugin.fixture.belowfloor",
            "Below Floor Module",
            typeof(Plugins.BelowContractFloorPluginModule).FullName!);

        var loader = new PluginLoader(packageStore, NullLogger<PluginLoader>.Instance);

        var loadResult = await loader.LoadAsync(record);

        Assert.False(loadResult.Success);
        Assert.NotNull(loadResult.Error);

        // The administrator has to be able to read the diagnosis off the plugin admin page, so the
        // message names the plugin, what it asks for, and both bounds the host supports.
        Assert.Contains("plugin.fixture.belowfloor", loadResult.Error!, StringComparison.Ordinal);
        Assert.Contains("1.12.0", loadResult.Error, StringComparison.Ordinal);
        Assert.Contains(ContractVersion.MinimumSupported.ToString(), loadResult.Error, StringComparison.Ordinal);
        Assert.Contains(ContractVersion.Current.ToString(), loadResult.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rent_ReturnsProvider_WhenTheContributionDeclaresTheCanonicalConstructor()
    {
        var packageStore = new FileSystemPluginPackageStore(
            Path.Combine(Path.GetTempPath(), "clubgear-finance-bugfix-typeload-tests", Guid.NewGuid().ToString("N")));

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            packageStore,
            "plugin.fixture.selfservice",
            "Fixture SelfService Module",
            typeof(Plugins.FixtureSelfServiceProviderModule).FullName!);

        var loader = new PluginLoader(packageStore, NullLogger<PluginLoader>.Instance);

        var loadResult = await loader.LoadAsync(record);

        Assert.True(loadResult.Success, loadResult.Error);
        Assert.NotNull(loadResult.LoadedPlugin);

        var registry = new PluginRegistry();
        await registry.RegisterAsync(
            loadResult.LoadedPlugin!.Runtime,
            loadResult.LoadedPlugin.Module,
            loadResult.LoadedPlugin.LoadContext);

        var activator = Plugins.TestContributionActivatorFactory.Create(
            registry, new Plugins.StubPluginRuntimeAdapter());

        await using var lease = activator.Rent<ISelfServiceProfileSectionProvider>(
            "plugin.fixture.selfservice",
            typeof(Plugins.FixtureSelfServiceProvider).FullName!);

        Assert.Null(lease.Error);
        Assert.NotNull(lease.Instance);
        Assert.IsAssignableFrom<ISelfServiceProfileSectionProvider>(lease.Instance!);
    }
}
