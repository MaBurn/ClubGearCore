namespace ClubGear.Plugin.Contracts;

public interface IAuditSinkProvider
{
    Task OnAuditEventAsync(PluginAuditEvent auditEvent, CancellationToken cancellationToken = default);
}
