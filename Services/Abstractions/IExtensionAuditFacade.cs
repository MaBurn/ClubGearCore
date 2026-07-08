namespace ClubGear.Services.Abstractions;

public interface IExtensionAuditFacade
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