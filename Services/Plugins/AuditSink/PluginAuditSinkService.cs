using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Plugins.AuditSink;

public sealed class PluginAuditSinkService : IPluginAuditSinkService
{
    private readonly IPluginRegistryReader _registryReader;
    private readonly ILogger<PluginAuditSinkService> _logger;

    public PluginAuditSinkService(
        IPluginRegistryReader registryReader,
        ILogger<PluginAuditSinkService> logger)
    {
        _registryReader = registryReader;
        _logger = logger;
    }

    public async Task DispatchAsync(PluginAuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        foreach (var runtime in _registryReader.GetRegisteredPlugins())
        {
            foreach (var contribution in runtime.AuditSinks)
            {
                var provider = _registryReader.CreateMemberProvider<IAuditSinkProvider>(
                    runtime.ModuleId, contribution.ProviderType);

                if (provider is null)
                {
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
