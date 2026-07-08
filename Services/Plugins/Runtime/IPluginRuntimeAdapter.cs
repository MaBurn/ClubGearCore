using System.Security.Claims;
using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Plugins.Runtime;

public interface IPluginRuntimeAdapter
{
    IPluginRuntimeBridge CreateBridge(IPluginModule pluginModule, ClaimsPrincipal user);

    Task<TResult> InvokeAsync<TResult>(
        IPluginModule pluginModule,
        ClaimsPrincipal user,
        Func<IPluginRuntimeBridge, CancellationToken, Task<TResult>> capability,
        string? requiredPermissionKey = null,
        Delegate? isolatedDelegate = null,
        CancellationToken cancellationToken = default);

    Task RunAsync(
        IPluginModule pluginModule,
        ClaimsPrincipal user,
        Func<IPluginRuntimeBridge, CancellationToken, Task> capability,
        CancellationToken cancellationToken = default);

    void EnsureIsolated(Delegate pluginDelegate);
}

public interface IPluginRuntimeBridge
{
    string ModuleId { get; }

    IPluginHostContext Host { get; }

    Task<bool> HasPermissionAsync(string permissionKey, CancellationToken cancellationToken = default);

    Task LogAsync(
        string action,
        string? actor = null,
        string? targetType = null,
        string? targetId = null,
        object? metadata = null,
        CancellationToken cancellationToken = default);

    Task<PluginRuntimeNotificationResult> NotifyAsync(
        PluginRuntimeNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record PluginRuntimeNotification(
    string Recipient,
    string Subject,
    string Body,
    string Channel,
    string? CorrelationId = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record PluginRuntimeNotificationResult(
    bool Success,
    string Channel,
    string Recipient,
    string? Error = null);