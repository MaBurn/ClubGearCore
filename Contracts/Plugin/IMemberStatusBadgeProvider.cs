namespace ClubGear.Plugin.Contracts;

public interface IMemberStatusBadgeProvider
{
    Task<IReadOnlyList<MemberStatusBadgeSlot>> GetBadgesAsync(
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default);
}

public sealed record MemberStatusBadgeSlot(
    string Key,
    string Label,
    string Tone = "secondary",
    int Order = 0);