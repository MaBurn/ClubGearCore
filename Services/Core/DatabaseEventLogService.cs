using System.Text.Json;
using ClubGear.Data;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace ClubGear.Services.Core;

public class DatabaseEventLogService : IEventLogService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ILogger<DatabaseEventLogService> _logger;

    // Uses its own DbContext instance rather than the ambient scoped one: this service is called
    // from GlobalExceptionMiddleware while the request's scoped DbContext may hold pending,
    // unvalidated changes from the operation that just failed. Sharing that context would flush
    // those changes as a side effect of logging the error.
    public DatabaseEventLogService(IDbContextFactory<ApplicationDbContext> dbContextFactory, ILogger<DatabaseEventLogService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public Task LogErrorAsync(
        string category,
        string message,
        object? details = null,
        string? requestId = null,
        string? path = null,
        string? method = null,
        string? userName = null,
        CancellationToken cancellationToken = default)
    {
        return WriteAsync(
            level: "Error",
            category: category,
            message: message,
            details: details,
            requestId: requestId,
            path: path,
            method: method,
            userName: userName,
            cancellationToken: cancellationToken);
    }

    public Task LogInfoAsync(
        string category,
        string message,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        return WriteAsync(
            level: "Info",
            category: category,
            message: message,
            details: details,
            requestId: null,
            path: null,
            method: null,
            userName: null,
            cancellationToken: cancellationToken);
    }

    private async Task WriteAsync(
        string level,
        string category,
        string message,
        object? details,
        string? requestId,
        string? path,
        string? method,
        string? userName,
        CancellationToken cancellationToken)
    {
        try
        {
            var entry = new SystemEventLog
            {
                OccurredAtUtc = DateTime.UtcNow,
                Level = level,
                Category = category,
                Message = message,
                RequestId = requestId,
                Path = path,
                Method = method,
                UserName = userName,
                DetailsJson = SerializeSafe(details)
            };

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            dbContext.SystemEventLogs.Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fehler beim Schreiben von SystemEventLog ({Category})", category);
        }
    }

    private static string? SerializeSafe(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Serialize(value);
        }
        catch
        {
            return value.ToString();
        }
    }
}
