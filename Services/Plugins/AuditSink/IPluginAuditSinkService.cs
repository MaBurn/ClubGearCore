using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Plugins.AuditSink;

public interface IPluginAuditSinkService
{
    Task DispatchAsync(PluginAuditEvent auditEvent, CancellationToken cancellationToken = default);
}
