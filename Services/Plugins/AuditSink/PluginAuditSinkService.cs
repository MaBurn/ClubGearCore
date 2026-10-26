using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Plugins.AuditSink;

public sealed class PluginAuditSinkService : IPluginAuditSinkService
{
    private readonly IPluginRegistryReader _registryReader;
    private readonly IPluginContributionActivator _contributionActivator;
    private readonly ILogger<PluginAuditSinkService> _logger;

    public PluginAuditSinkService(
        IPluginRegistryReader registryReader,
        IPluginContributionActivator contributionActivator,
        ILogger<PluginAuditSinkService> logger)
    {
        _registryReader = registryReader;
        _contributionActivator = contributionActivator;
        _logger = logger;
    }

    public async Task DispatchAsync(PluginAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        foreach (var runtime in _registryReader.GetRegisteredPlugins())
        {
            foreach (var contribution in runtime.AuditSinks)
            {
                await using var lease = _contributionActivator.Rent<IAuditSinkProvider>(
                    runtime.ModuleId, contribution.ProviderType);

                if (lease.Instance is not { } provider)
                {
                    // CORE-18. A sink that cannot be rented is still skipped — one broken plugin
                    // must not swallow the audit event for the others — but no longer silently.
                    _logger.LogWarning(
                        "Audit-Sink '{ProviderType}' (Plugin '{ModuleId}') wurde uebersprungen: {Reason}",
                        contribution.ProviderType,
                        runtime.ModuleId,
                        lease.Error);
                    continue;
                }

                try
                {
                    await provider.OnAuditEventAsync(auditEvent, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Audit-Sink '{ProviderType}' (Plugin '{ModuleId}') hat einen Fehler produziert.",
                        contribution.ProviderType,
                        runtime.ModuleId);
                }
            }
        }
    }
}
