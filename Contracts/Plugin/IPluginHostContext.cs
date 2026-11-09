namespace ClubGear.Plugin.Contracts;

public interface IPluginHostContext
{
    IPluginMetadataFacade Metadata { get; }

    IPluginMemberReader Members { get; }

    IPluginMemberActionFacade MemberActions { get; }

    IPluginDataStore Persistence { get; }

    IPluginPermissionFacade Permissions { get; }

    /// <summary>
    /// Contract 1.16.0. Who is acting in this call: the member linked to the signed-in user.
    /// The host binds it from the authenticated principal when it builds the context; a plugin
    /// cannot set it, and must never take an acting identity from command arguments instead.
    /// </summary>
    /// <remarks>
    /// The default implementation answers <c>null</c>, so plugin test doubles written against
    /// 1.15.0 keep compiling.
    /// </remarks>
    IPluginActorFacade Actor => NoPluginActor.Instance;
}

/// <summary>Contract 1.16.0. See <see cref="IPluginHostContext.Actor"/>.</summary>
public interface IPluginActorFacade
{
    /// <summary>
    /// The id of the member linked to the signed-in user, usable with
    /// <see cref="IPluginMemberReader.GetByIdAsync"/>. <c>null</c> when nobody is signed in, when
    /// the user has no linked member, and in host-initiated calls such as background jobs.
    /// </summary>
    Task<int?> GetMemberIdAsync(CancellationToken cancellationToken = default);
}

internal sealed class NoPluginActor : IPluginActorFacade
{
    public static readonly NoPluginActor Instance = new();

    public Task<int?> GetMemberIdAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<int?>(null);
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