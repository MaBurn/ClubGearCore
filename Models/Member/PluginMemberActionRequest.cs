namespace ClubGear.Models.MemberActions;

public sealed record PluginMemberActionRequest(
    string ModuleId,
    string ActionKey,
    int MemberId,
    IReadOnlyDictionary<string, string>? Arguments = null);