using System.Text.Json;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Core;

public class DatabaseAuditLogService : IAuditLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DatabaseAuditLogService> _logger;

    public DatabaseAuditLogService(ApplicationDbContext dbContext, ILogger<DatabaseAuditLogService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = new AuditLogEntry
            {
                OccurredAtUtc = record.OccurredAtUtc ?? DateTime.UtcNow,
                Action = record.Action,
                Actor = record.Actor,
                Source = record.Source,
                TargetType = record.TargetType,
                TargetId = record.TargetId,
                BeforeJson = SerializeSafe(record.Before),
                AfterJson = SerializeSafe(record.After),
                MetadataJson = SerializeSafe(record.Metadata)
            };

            _dbContext.AuditLogs.Add(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Schreiben des Audit-Logs fuer Aktion {Action}", record.Action);
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

    private static string? SerializeSafe(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Serialize(value, JsonOptions);
        }
        catch
        {
            return value.ToString();
        }
    }
}
