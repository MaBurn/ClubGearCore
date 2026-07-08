namespace ClubGear.Plugin.Contracts;

public interface IPluginHostContext
{
    IPluginMetadataFacade Metadata { get; }

    IPluginMemberReader Members { get; }

    IPluginMemberActionFacade MemberActions { get; }

    IPluginDataStore Persistence { get; }

    IPluginPermissionFacade Permissions { get; }
}

public interface IPluginPermissionFacade
{
    Task<bool> HasPermissionAsync(string permissionKey, CancellationToken cancellationToken = default);
}

public interface IPluginMetadataFacade
{
    PluginHostMetadata GetCurrent();
}

public interface IPluginMemberReader
{
    Task<IReadOnlyList<PluginMemberSummary>> GetListAsync(string? search = null, CancellationToken cancellationToken = default);

    Task<PluginMemberDetail?> GetByIdAsync(int memberId, CancellationToken cancellationToken = default);
}

public interface IPluginMemberActionFacade
{
    Task<PluginMemberActionResult> ExecuteAsync(PluginMemberActionRequest request, CancellationToken cancellationToken = default);
}

public sealed record PluginHostMetadata(
    string ModuleId,
    string DisplayName,
    string License,
    string RequiredCoreVersion,
    IReadOnlyList<string> DeclaredPermissions,
    IReadOnlyList<string> ExtensionPoints);

public sealed record PluginMemberActionRequest(
    int MemberId,
    string ActionKey,
    IReadOnlyDictionary<string, string>? Arguments = null);

public sealed record PluginMemberActionResult(
    bool Success,
    string Status,
    string? Message = null,
    IReadOnlyList<PluginFieldError>? FieldErrors = null);