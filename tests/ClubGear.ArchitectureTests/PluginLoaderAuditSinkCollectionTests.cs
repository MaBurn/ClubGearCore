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
        Assert.Equal(typeof(ContributedAuditSinkProvider).FullName, runtime.AuditSinks[0].ProviderType);
    }

    /// <summary>
    /// An audit sink provider that lives in the packaged plugin assembly. Since CORE-16 the loader
    /// resolves every declared contribution type from the module's own assembly, so a placeholder
    /// type name would be a load-time rejection.
    /// </summary>
    public sealed class ContributedAuditSinkProvider
    {
        // CORE-18. Contributions are host-constructed as T(IPluginHostContext).
        public ContributedAuditSinkProvider(IPluginHostContext hostContext)
        {
            HostContext = hostContext;
        }

        public IPluginHostContext HostContext { get; }
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
                ">=1.13.0",
                ["members.read"],
                ["audit.sink"]);
        }

        public PluginManifest Manifest { get; }

        public void RegisterContributions(IPluginContributionSink sink)
        {
            sink.AddAuditSink(new PluginAuditSinkContribution(typeof(ContributedAuditSinkProvider).FullName!));
        }
    }
}
