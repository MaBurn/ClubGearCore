using ClubGear.ArchitectureTests.Fixtures;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Plugins.Installation;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginLoaderIdentityProviderCollectionTests
{
    [Fact]
    public async Task Loader_CollectsIdentityProviderContributions_FromRegisterContributions()
    {
        var packageStore = new FileSystemPluginPackageStore(
            Path.Combine(Path.GetTempPath(), "clubgear-plugin-idp-tests", Guid.NewGuid().ToString("N")));

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            packageStore,
            "plugin.identity.provider.test",
            "Identity Provider Test Plugin",
            typeof(IdentityProviderContributingPluginModule).FullName!);

        var loader = new PluginLoader(packageStore, NullLogger<PluginLoader>.Instance);
        var loadResult = await loader.LoadAsync(record);

        Assert.True(loadResult.Success, loadResult.Error);
        Assert.NotNull(loadResult.LoadedPlugin);

        var runtime = loadResult.LoadedPlugin!.Runtime;
        Assert.Equal(1, runtime.IdentityProviders.Count);
        Assert.Equal("my.idp", runtime.IdentityProviders[0].ProviderKey);
        Assert.Equal("MyIdentityProviderPlugin", runtime.IdentityProviders[0].ProviderType);
    }

    /// <summary>
    /// A plugin module that registers a single identity provider contribution for testing.
    /// </summary>
    public sealed class IdentityProviderContributingPluginModule : IPluginModule
    {
        public IdentityProviderContributingPluginModule()
        {
            Manifest = new PluginManifest(
                "plugin.identity.provider.test",
                "Identity Provider Test Plugin",
                new Version(1, 0, 0),
                "Tests",
                "MIT",
                typeof(IdentityProviderContributingPluginModule).FullName!,
                ">=1.0.0",
                ["members.read"],
                ["identity.provider"]);
        }

        public PluginManifest Manifest { get; }

        public void RegisterContributions(IPluginContributionSink sink)
        {
            sink.AddIdentityProvider(new PluginIdentityProviderContribution("my.idp", "MyIdentityProviderPlugin"));
        }
    }
}
