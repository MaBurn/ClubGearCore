using System.Security.Claims;
using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Abstractions;

public interface IPluginPageService
{
    Task<PluginPageResult<PluginPageDefinition>> GetPageDefinitionAsync(
        string moduleId,
        string pageKey,
        ClaimsPrincipal user,
        CancellationToken ct = default);

    Task<PluginPageResult<IReadOnlyList<IReadOnlyDictionary<string, string?>>>> GetRowsAsync(
        string moduleId,
        string pageKey,
        ClaimsPrincipal user,
        string? filterValue,
        string? entityKey,
        CancellationToken ct = default);

    Task<PluginPageResult<PluginCommandResult>> ExecuteCommandAsync(
        string moduleId,
        string pageKey,
        string commandKey,
        string? entityKey,
        IReadOnlyDictionary<string, string> arguments,
        ClaimsPrincipal user,
        CancellationToken ct = default);
}

public sealed record PluginPageResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public bool IsNotFound { get; init; }
    public bool IsForbidden { get; init; }
    public string? ErrorMessage { get; init; }

    public static PluginPageResult<T> Success(T value) => new()
    {
        IsSuccess = true,
        Value = value
    };

    public static PluginPageResult<T> NotFound() => new()
    {
        IsNotFound = true
    };

    public static PluginPageResult<T> Forbidden() => new()
    {
        IsForbidden = true
    };

    public static PluginPageResult<T> Error(string message) => new()
    {
        ErrorMessage = message
    };
}
