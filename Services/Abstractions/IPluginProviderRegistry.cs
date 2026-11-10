using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Abstractions;

/// <summary>
/// CORE-13. The one seam through which a plugin-supplied service reaches a consumer, whether that
/// consumer is the core or another plugin.
/// </summary>
/// <remarks>
/// This gives <see cref="PluginServiceContribution"/> its first consumer. The loader has always
/// collected those contributions (<c>PluginLoader.PluginContributionCollector.AddService</c>) and
/// carried them on <see cref="RegisteredPluginRuntime.Services"/>, but nothing ever read them, so
/// declaring a service was a no-op.
/// <para>
/// Providers are resolved on demand rather than registered in the DI container, because plugins
/// are activated and deactivated at runtime while the container is built once at startup. This is
/// the same approach the shipped audit-sink and identity-provider seams already take.
/// </para>
/// </remarks>
public interface IPluginProviderRegistry
{
    /// <summary>
    /// The administrator-selected provider for a <see cref="PluginCapabilityArity.SingleActive"/>
    /// capability, or an empty lease when the caller should fall back to the core default.
    /// </summary>
    /// <remarks>
    /// CORE-14: without a selection the core default serves the capability, however many plugins
    /// claim it, so this never throws on a collision and never picks arbitrarily. A selected plugin
    /// that is not active also yields an empty lease. Selection lives in
    /// <see cref="IPluginProviderSelectionService"/>.
    /// </remarks>
    /// <remarks>
    /// CORE-18: the caller owns a lease, not a bare instance. Disposing it releases the host scope
    /// the provider was constructed in and lets a pending deactivation finish draining.
    /// </remarks>
    PluginContributionLease<TProvider> Resolve<TProvider>(string capabilityKey) where TProvider : class;

    /// <summary>
    /// Every provider serving a <see cref="PluginCapabilityArity.FanOut"/> capability, ordered by
    /// module id so the sequence is stable across restarts.
    /// </summary>
    /// <remarks>CORE-18: every element carries its own lease and must be disposed.</remarks>
    IReadOnlyList<PluginProvider<TProvider>> ResolveAll<TProvider>(string capabilityKey) where TProvider : class;

    /// <summary>
    /// What is registered for a capability, without instantiating anything.
    /// </summary>
    IReadOnlyList<PluginProviderRegistration> GetRegistrations(string capabilityKey);
}

/// <summary>A resolved provider instance together with the plugin it came from.</summary>
/// <remarks>
/// The module id travels with the instance so a failure can name the plugin responsible, the way
/// <c>PluginAuditSinkService</c> already does when a sink throws.
/// </remarks>
public sealed class PluginProvider<TProvider> : IDisposable, IAsyncDisposable
    where TProvider : class
{
    private readonly PluginContributionLease<TProvider> _lease;

    public PluginProvider(PluginContributionLease<TProvider> lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (lease.Instance is null)
        {
            throw new ArgumentException("A PluginProvider is only ever built from a live lease.", nameof(lease));
        }

        _lease = lease;
    }

    /// <summary>The underlying lease, so a caller that selects one provider can hand it on.</summary>
    public PluginContributionLease<TProvider> Lease => _lease;

    public string ModuleId => _lease.ModuleId;

    public string ProviderType => _lease.ContributionType;

    public TProvider Instance => _lease.Instance!;

    public void Dispose() => _lease.Dispose();

    public ValueTask DisposeAsync() => _lease.DisposeAsync();
}

/// <summary>A declared provider that has not been instantiated.</summary>
public sealed record PluginProviderRegistration(string ModuleId, string CapabilityKey, string ProviderType);
