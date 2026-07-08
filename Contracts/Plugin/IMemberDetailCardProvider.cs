namespace ClubGear.Plugin.Contracts;

public interface IMemberDetailCardProvider
{
    Task<IReadOnlyList<MemberDetailCardSlot>> GetCardsAsync(
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default);
}

public sealed record MemberDetailCardSlot(
    string Key,
    string Title,
    string Body,
    int Order = 0);