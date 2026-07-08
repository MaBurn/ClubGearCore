namespace ClubGear.Plugin.Contracts;

public interface ISelfServiceProfileSectionProvider
{
    Task<SelfServiceProfileSection?> GetSectionAsync(
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default);

    Task<PluginMemberActionResult> ExecuteSelfServiceActionAsync(
        PluginMemberActionRequest request,
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default);
}

public sealed record SelfServiceProfileSection(
    string Key,
    string Title,
    string HtmlBody,
    int Order = 0);
