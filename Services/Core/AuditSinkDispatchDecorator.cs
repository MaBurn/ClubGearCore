using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.AuditSink;

namespace ClubGear.Services.Core;

public sealed class AuditSinkDispatchDecorator : IAuditLogService
{
    private readonly IAuditLogService _inner;
    private readonly IPluginAuditSinkService _sinkService;
    private readonly ILogger<AuditSinkDispatchDecorator> _logger;

    public AuditSinkDispatchDecorator(
        IAuditLogService inner,
        IPluginAuditSinkService sinkService,
        ILogger<AuditSinkDispatchDecorator> logger)
    {
        _inner = inner;
        _sinkService = sinkService;
        _logger = logger;
    }

    public async Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
    {
        await _inner.LogAsync(record, cancellationToken);

        var auditEvent = new PluginAuditEvent(
            record.Action,
            record.Actor,
            record.Source,
            record.TargetType,
            record.TargetId,
            record.OccurredAtUtc ?? DateTime.UtcNow);

        try
        {
            await _sinkService.DispatchAsync(auditEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Dispatchen von Audit-Events an Plugins.");
        }
    }

    public Task LogChangeAsync(
        string action,
        object? before,
        object? after,
        string? actor = null,
        string? source = null,
        string? targetType = null,
        string? targetId = null,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var record = new AuditLogRecord(
            Action: action,
            Actor: actor,
            Source: source,
            TargetType: targetType,
            TargetId: targetId,
            Before: before,
            After: after,
            Metadata: metadata,
            OccurredAtUtc: DateTime.UtcNow);

        return LogAsync(record, cancellationToken);
    }
}
