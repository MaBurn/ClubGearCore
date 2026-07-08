namespace ClubGear.Services.Abstractions;

public interface IEventLogService
{
    Task LogErrorAsync(
        string category,
        string message,
        object? details = null,
        string? requestId = null,
        string? path = null,
        string? method = null,
        string? userName = null,
        CancellationToken cancellationToken = default);

    Task LogInfoAsync(
        string category,
        string message,
        object? details = null,
        CancellationToken cancellationToken = default);
}
