namespace ClubGear.Plugin.Contracts;

public interface IPluginPageProvider
{
    Task<PluginPageDefinition> GetPageDefinitionAsync(
        IPluginHostContext context,
        CancellationToken ct = default);

    Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> GetRowsAsync(
        IPluginHostContext context,
        string? filterValue,
        string? entityKey,
        CancellationToken ct = default);

    Task<PluginCommandResult> ExecuteCommandAsync(
        IPluginHostContext context,
        string commandKey,
        string? entityKey,
        IReadOnlyDictionary<string, string> arguments,
        CancellationToken ct = default);
}

public sealed record PluginPageDefinition(
    string PageKey,
    string Title,
    string EntityKeyColumn,
    IReadOnlyList<PluginPageColumn> Columns,
    IReadOnlyList<PluginPageCommand> Commands,
    string? ListPermission,
    string? FilterPlaceholder);

public sealed record PluginPageColumn(
    string Key,
    string Label);

public sealed record PluginPageCommand(
    string Key,
    string Label,
    string? Icon,
    string? RequiredPermission,
    IReadOnlyList<PluginFieldSchema>? Schema,
    bool RequiresEntityKey);
