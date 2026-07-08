using ClubGear.ArchitectureTests.Fixtures;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Plugins.Installation;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginLoaderAuditSinkCollectionTests
{
    [Fact]
    public async Task Loader_CollectsAuditSinkContributions_FromRegisterContributions()
    {
        var packageStore = new FileSystemPluginPackageStore(
            Path.Combine(Path.GetTempPath(), "clubgear-plugin-audit-sink-tests", Guid.NewGuid().ToString("N")));

        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            packageStore,
            "plugin.audit.sink.test",
            "Audit Sink Test Plugin",
            typeof(AuditSinkContributingPluginModule).FullName!);

        var loader = new PluginLoader(packageStore, NullLogger<PluginLoader>.Instance);
        var loadResult = await loader.LoadAsync(record);

        Assert.True(loadResult.Success, loadResult.Error);
        Assert.NotNull(loadResult.LoadedPlugin);

        var runtime = loadResult.LoadedPlugin!.Runtime;
        Assert.Equal(1, runtime.AuditSinks.Count);
        Assert.Equal("MyProvider", runtime.AuditSinks[0].ProviderType);
    }

    /// <summary>
    /// A plugin module that registers a single audit sink contribution for testing.
    /// </summary>
    public sealed class AuditSinkContributingPluginModule : IPluginModule
    {
        public AuditSinkContributingPluginModule()
        {
            Manifest = new PluginManifest(
                "plugin.audit.sink.test",
                "Audit Sink Test Plugin",
                new Version(1, 0, 0),
                "Tests",
                "MIT",
                typeof(AuditSinkContributingPluginModule).FullName!,
                ">=1.0.0",
                ["members.read"],
                ["audit.sink"]);
        }

        public PluginManifest Manifest { get; }

        public void RegisterContributions(IPluginContributionSink sink)
        {
            sink.AddAuditSink(new PluginAuditSinkContribution("MyProvider"));
        }
    }
}
