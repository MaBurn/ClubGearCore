using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Core;

public class ExtensionAuditFacade : IExtensionAuditFacade
{
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<ExtensionAuditFacade> _logger;

    public ExtensionAuditFacade(IAuditLogService auditLogService, ILogger<ExtensionAuditFacade> logger)
    {
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
    {
        try
        {
            await _auditLogService.LogAsync(record, cancellationToken);
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erweiterungs-Audit fuer Aktion {Action}", record.Action);
            throw new UserFriendlyException("Das Audit fuer die Erweiterung konnte nicht geschrieben werden.", ex);
        }
    }

    public async Task LogChangeAsync(
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
        try
        {
            await _auditLogService.LogChangeAsync(action, before, after, actor, source, targetType, targetId, metadata, cancellationToken);
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Erweiterungs-Audit-Change fuer Aktion {Action}", action);
            throw new UserFriendlyException("Die Aenderung konnte im Erweiterungs-Audit nicht protokolliert werden.", ex);
        }
    }
}