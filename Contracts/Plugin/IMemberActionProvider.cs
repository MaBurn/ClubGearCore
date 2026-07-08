namespace ClubGear.Plugin.Contracts;

public interface IMemberActionProvider
{
    Task<IReadOnlyList<MemberActionSlot>> GetActionsAsync(
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default);

    Task<PluginMemberActionResult> ExecuteAsync(
        PluginMemberActionRequest request,
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default);
}

public sealed record MemberActionSlot(
    string Key,
    string Label,
    string PermissionKey,
    string Style = "outline-secondary",
    int Order = 0,
    string? ConfirmMessage = null,
    IReadOnlyList<PluginFieldSchema>? ArgumentSchema = null);