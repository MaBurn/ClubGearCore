namespace ClubGear.Plugin.Contracts;

public interface IAdminFunctionPanelProvider
{
    Task<IReadOnlyList<PluginAdminPanel>> GetPanelsAsync(
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default);

    Task<PluginCommandResult> ExecuteCommandAsync(
        PluginAdminCommandRequest request,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default);
}

public sealed record PluginAdminModulePanels(
    string ModuleId,
    string DisplayName,
    IReadOnlyList<PluginAdminPanel> Panels);

public sealed record PluginAdminPanel(
    string Key,
    string Title,
    string PermissionKey,
    int Order = 0,
    string? Description = null,
    IReadOnlyList<PluginAdminCommandDescriptor>? Commands = null,
    IReadOnlyList<PluginAdminPanelItem>? Items = null);

public sealed record PluginAdminPanelItem(
    string Key,
    string Title,
    string State = "active",
    int Order = 0,
    string? Description = null,
    IReadOnlyDictionary<string, string>? Values = null);

public sealed record PluginAdminCommandDescriptor(
    string Key,
    string Label,
    string PermissionKey,
    string Style = "outline-secondary",
    int Order = 0,
    string? ConfirmMessage = null,
    IReadOnlyList<PluginFieldSchema>? ArgumentSchema = null);

public sealed record PluginAdminCommandRequest(
    string PanelKey,
    string CommandKey,
    IReadOnlyDictionary<string, string>? Arguments = null);

public sealed record PluginCommandResult(
    bool Success,
    string Status,
    string? Message = null,
    IReadOnlyList<PluginFieldError>? FieldErrors = null);