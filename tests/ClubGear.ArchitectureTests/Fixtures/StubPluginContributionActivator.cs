using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using ClubGear.Services.Abstractions;

namespace ClubGear.ArchitectureTests.Plugins;

/// <summary>
/// CORE-18 test double for <see cref="IPluginContributionActivator"/>. Hands out leases over
/// instances the test supplies, so a unit test can exercise a call site without a real
/// AssemblyLoadContext, a real DI scope or a real plugin package.
/// </summary>
public sealed class StubPluginContributionActivator : IPluginContributionActivator
{
    private readonly Func<string, string, object?> _factory;
    private int _rentCallCount;
    private int _disposedLeaseCount;

    public StubPluginContributionActivator(Func<string, string, object?> factory)
    {
        _factory = factory;
    }

    /// <summary>How often <see cref="Rent{T}"/> was asked for a contribution.</summary>
    public int RentCallCount => _rentCallCount;

    /// <summary>How many of the leases handed out were disposed again.</summary>
    public int DisposedLeaseCount => _disposedLeaseCount;

    public ClaimsPrincipal? LastUser { get; private set; }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The counter is owned by the lease that is returned; disposing the lease disposes it.")]
    public PluginContributionLease<T> Rent<T>(string moduleId, string contributionType, ClaimsPrincipal? user = null)
        where T : class
    {
        Interlocked.Increment(ref _rentCallCount);
        LastUser = user;

        if (_factory(moduleId, contributionType) is not T instance)
        {
            return PluginContributionLease<T>.Empty(
                moduleId,
                contributionType,
                $"Typ '{contributionType}' aus Plugin '{moduleId}' wurde nicht gefunden oder "
                + $"implementiert '{typeof(T).Name}' nicht.");
        }

        return PluginContributionLease<T>.Rented(
            moduleId,
            contributionType,
            instance,
            activityToken: new DisposeCounter(this));
    }

    private sealed class DisposeCounter : IDisposable
    {
        private readonly StubPluginContributionActivator _owner;
        private bool _disposed;

        public DisposeCounter(StubPluginContributionActivator owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Interlocked.Increment(ref _owner._disposedLeaseCount);
        }
    }
}
