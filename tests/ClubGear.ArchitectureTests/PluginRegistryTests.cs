using System.Security.Claims;
using ClubGear.ArchitectureTests.Fixtures;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Plugins.Installation;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginRegistryTests
{
    [Fact]
    public async Task RegistryReader_ExposesLoadedPluginContributions_AndRuntimeRoutes()
    {
        var packageStore = new FileSystemPluginPackageStore(
            Path.Combine(Path.GetTempPath(), "clubgear-plugin-registry-tests", Guid.NewGuid().ToString("N")));
        var record = await PluginTestPackageBuilder.CreateStoredPluginRecordAsync(
            packageStore,
            "plugin.runtime.a",
            "Runtime Plugin A",
            typeof(Plugins.RuntimeLoadedPluginModuleA).FullName!);

        var loader = new PluginLoader(packageStore, NullLogger<PluginLoader>.Instance);
        var loadResult = await loader.LoadAsync(record);

        Assert.True(loadResult.Success, loadResult.Error);
        Assert.NotNull(loadResult.LoadedPlugin);

        var registry = new PluginRegistry();
        registry.Register(
            loadResult.LoadedPlugin!.Runtime,
            loadResult.LoadedPlugin.Module,
            loadResult.LoadedPlugin.LoadContext);

        var registrar = new PluginEndpointRegistrar(new NoOpRuntimeAdapter(), registry);
        registrar.RegisterGet(
            loadResult.LoadedPlugin.Module,
            "/runtime/health",
            "members.read",
            new Plugins.AllowedPluginEndpoint().HandleAsync);

        var snapshot = registry.GetByModuleId("plugin.runtime.a");
        Assert.NotNull(snapshot);
        Assert.Contains(snapshot!.Routes, route => route.RoutePattern == "/declared/runtime-a");
        Assert.Contains(snapshot.Routes, route => route.RoutePattern == "/runtime/health");
        Assert.Contains(snapshot.Services, service => service.Key == "members.service");
        Assert.Contains(snapshot.MemberProviders, provider => provider.SlotKind == PluginMemberSlotKind.DetailCard);
        Assert.Contains(snapshot.BackgroundJobs, job => job.Key == "members.sync");
    }

    private sealed class NoOpRuntimeAdapter : IPluginRuntimeAdapter
    {
        public IPluginRuntimeBridge CreateBridge(IPluginModule pluginModule, ClaimsPrincipal user)
            => throw new NotSupportedException();

        public Task<TResult> InvokeAsync<TResult>(
            IPluginModule pluginModule,
            ClaimsPrincipal user,
            Func<IPluginRuntimeBridge, CancellationToken, Task<TResult>> capability,
            string? requiredPermissionKey = null,
            Delegate? isolatedDelegate = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RunAsync(
            IPluginModule pluginModule,
            ClaimsPrincipal user,
            Func<IPluginRuntimeBridge, CancellationToken, Task> capability,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void EnsureIsolated(Delegate pluginDelegate)
        {
        }
    }
}