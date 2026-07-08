using System.Security.Claims;
using ClubGear.Models;
using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Abstractions;

public sealed record SelfServicePluginSectionView(
    string ModuleId,
    string PluginDisplayName,
    SelfServiceProfileSection Section,
    int SortOrder);

public sealed record SelfServiceSectionActionRequest(
    string ModuleId,
    string ActionKey,
    IReadOnlyDictionary<string, string> Arguments);

public interface ISelfServiceSectionService
{
    Task<IReadOnlyList<SelfServicePluginSectionView>> GetSelfServiceSectionsAsync(
        Member member,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<PluginMemberActionResult> ExecuteSelfServiceActionAsync(
        SelfServiceSectionActionRequest request,
        Member member,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
