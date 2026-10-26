using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace ClubGear.Services.Abstractions;

/// <summary>
/// CORE-18. The one seam through which a plugin contribution is constructed. Every contribution is
/// built through a host-controlled scope and handed out as a lease, so that "plugin code is live
/// for this module" is a fact the host knows rather than approximates.
/// </summary>
/// <remarks>
/// Contributions have exactly one canonical public constructor,
/// <c>T(ClubGear.Plugin.Contracts.IPluginHostContext)</c>. There is no parameterless fallback and no
/// other accepted arity; the shape is enforced at load time by <c>PluginLoader</c>, which is why
/// <c>ContractVersion.MinimumSupported</c> moved to 1.13.0.
/// <para>
/// The module entry point (<c>IPluginModule</c>) is the one deliberate exception and keeps its
/// parameterless constructor: <c>IPluginHostContext</c> is built from the module's manifest, which
/// is only reachable from an already-constructed module. It is isolation-scanned before it is
/// instantiated instead. See <c>docs/plugin-contract.md</c>.
/// </para>
/// </remarks>
public interface IPluginContributionActivator
{
    /// <summary>
    /// Constructs the contribution and keeps the module counted as busy until the lease is
    /// disposed. Never throws for a plugin-authoring or lifecycle problem: an empty lease carrying
    /// <see cref="PluginContributionLease{T}.Error"/> is returned instead.
    /// </summary>
    PluginContributionLease<T> Rent<T>(string moduleId, string contributionType, ClaimsPrincipal? user = null)
        where T : class;
}

/// <summary>
/// A constructed plugin contribution together with the host scope it lives in and the activity
/// token that keeps deactivation waiting for it (CORE-17).
/// </summary>
/// <remarks>
/// Implements both <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/> on purpose: most
/// call sites are asynchronous and use <c>await using</c>, but the Razor view at
/// <c>Views/Admin/ExternalLogin/Configure.cshtml</c> is synchronous and uses <c>using</c>.
/// </remarks>
public sealed class PluginContributionLease<T> : IDisposable, IAsyncDisposable
    where T : class
{
    private readonly IServiceScope? _scope;
    private readonly IDisposable? _activityToken;
    private bool _disposed;

    private PluginContributionLease(
        string moduleId,
        string contributionType,
        T? instance,
        string? error,
        IServiceScope? scope,
        IDisposable? activityToken)
    {
        ModuleId = moduleId;
        ContributionType = contributionType;
        Instance = instance;
        Error = error;
        _scope = scope;
        _activityToken = activityToken;
    }

    /// <summary>The contribution, or <c>null</c> when it could not be rented. See <see cref="Error"/>.</summary>
    public T? Instance { get; }

    public string ModuleId { get; }

    public string ContributionType { get; }

    /// <summary>Why <see cref="Instance"/> is <c>null</c>, or <c>null</c> when the lease is live.</summary>
    public string? Error { get; }

    public static PluginContributionLease<T> Rented(
        string moduleId,
        string contributionType,
        T instance,
        IServiceScope? scope = null,
        IDisposable? activityToken = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return new PluginContributionLease<T>(moduleId, contributionType, instance, null, scope, activityToken);
    }

    public static PluginContributionLease<T> Empty(string moduleId, string contributionType, string error)
        => new(moduleId, contributionType, null, error, null, null);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Scope first, token second: the token is what deactivation waits on, so releasing it
        // before the scope is gone would let the AssemblyLoadContext unload underneath us.
        try
        {
            _scope?.Dispose();
        }
        finally
        {
            _activityToken?.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            if (_scope is IAsyncDisposable asyncScope)
            {
                await asyncScope.DisposeAsync();
            }
            else
            {
                _scope?.Dispose();
            }
        }
        finally
        {
            _activityToken?.Dispose();
        }
    }
}
