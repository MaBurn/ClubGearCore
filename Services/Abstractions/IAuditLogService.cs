namespace ClubGear.Services.Abstractions;

public interface IAuditLogService
{
    Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default);
    Task LogChangeAsync(
        string action,
        object? before,
        object? after,
        string? actor = null,
        string? source = null,
        string? targetType = null,
        string? targetId = null,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}

public sealed record AuditLogRecord(
    string Action,
    string? Actor = null,
    string? Source = null,
    string? TargetType = null,
    string? TargetId = null,
    object? Before = null,
    object? After = null,
    object? Metadata = null,
    DateTime? OccurredAtUtc = null);
