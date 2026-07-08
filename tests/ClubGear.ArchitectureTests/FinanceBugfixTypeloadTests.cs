using ClubGear.ArchitectureTests.Fixtures;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Plugins.Installation;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class FinanceBugfixTypeloadTests
{
    [Fact]
    public async Task CreateMemberProvider_ReturnsProvider_WhenContractAssemblyIsCurrentVersion()
    {
        // Arrange: build a plugin package containing FixtureSelfServiceProviderModule
        var packageStore = new FileSystemPluginPackageStore(
            Path.Combine(Path.GetTempPath(), "clubgear-finance-bugfix-typeload-tests", Guid.NewGuid().ToString("N")));

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            packageStore,
            "plugin.fixture.selfservice",
            "Fixture SelfService Module",
            typeof(Plugins.FixtureSelfServiceProviderModule).FullName!);

        var loader = new PluginLoader(packageStore, NullLogger<PluginLoader>.Instance);

        // Act: load and register
        var loadResult = await loader.LoadAsync(record);

        Assert.True(loadResult.Success, loadResult.Error);
        Assert.NotNull(loadResult.LoadedPlugin);

        var registry = new PluginRegistry();
        registry.Register(
            loadResult.LoadedPlugin!.Runtime,
            loadResult.LoadedPlugin.Module,
            loadResult.LoadedPlugin.LoadContext);

        // Act: instantiate the provider via the registry (the path guarded by the TypeLoadException catch)
        var provider = registry.CreateMemberProvider<ISelfServiceProfileSectionProvider>(
            "plugin.fixture.selfservice",
            typeof(Plugins.FixtureSelfServiceProvider).FullName!);

        // Assert: provider must be non-null and implement the contract interface
        Assert.NotNull(provider);
        Assert.IsAssignableFrom<ISelfServiceProfileSectionProvider>(provider);
    }
}
