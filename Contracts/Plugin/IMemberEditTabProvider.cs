namespace ClubGear.Plugin.Contracts;

public interface IMemberEditTabProvider
{
    Task<IReadOnlyList<MemberEditTabSlot>> GetTabsAsync(
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default);
}

public sealed record MemberEditTabSlot(
    string Key,
    string Title,
    string Content,
    int Order = 0)
{
    public string? GroupKey { get; init; }
    public string? GroupTitle { get; init; }
}