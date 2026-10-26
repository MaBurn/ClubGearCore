using System.Security.Claims;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Plugins.Runtime;

namespace ClubGear.ArchitectureTests.Plugins;

/// <summary>
/// CORE-18. The smallest adapter that can hand a contribution its <see cref="IPluginHostContext"/>.
/// Used where a test only needs construction to succeed and never calls back into the host.
/// </summary>
public sealed class StubPluginRuntimeAdapter : IPluginRuntimeAdapter
{
    public IPluginRuntimeBridge CreateBridge(IPluginModule pluginModule, ClaimsPrincipal user)
        => new StubBridge(pluginModule.Manifest.ModuleId);

    public Task<TResult> InvokeAsync<TResult>(
        IPluginModule pluginModule,
        ClaimsPrincipal user,
        Func<IPluginRuntimeBridge, CancellationToken, Task<TResult>> capability,
        string? requiredPermissionKey = null,
        Delegate? isolatedDelegate = null,
        CancellationToken cancellationToken = default)
        => capability(CreateBridge(pluginModule, user), cancellationToken);

    public Task RunAsync(
        IPluginModule pluginModule,
        ClaimsPrincipal user,
        Func<IPluginRuntimeBridge, CancellationToken, Task> capability,
        CancellationToken cancellationToken = default)
        => capability(CreateBridge(pluginModule, user), cancellationToken);

    public void EnsureIsolated(Delegate pluginDelegate)
    {
    }

    private sealed class StubBridge : IPluginRuntimeBridge
    {
        public StubBridge(string moduleId) => ModuleId = moduleId;

        public string ModuleId { get; }

        public IPluginHostContext Host { get; } = new StubHostContext();

        public Task<bool> HasPermissionAsync(string permissionKey, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task LogAsync(
            string action,
            string? actor = null,
            string? targetType = null,
            string? targetId = null,
            object? metadata = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PluginRuntimeNotificationResult> NotifyAsync(
            PluginRuntimeNotification notification,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The stub adapter does not deliver notifications.");
    }

    private sealed class StubHostContext : IPluginHostContext
    {
        public IPluginMetadataFacade Metadata => throw new NotSupportedException();

        public IPluginMemberReader Members => throw new NotSupportedException();

        public IPluginMemberActionFacade MemberActions => throw new NotSupportedException();

        public IPluginDataStore Persistence => throw new NotSupportedException();

        public IPluginPermissionFacade Permissions => throw new NotSupportedException();
    }
}
