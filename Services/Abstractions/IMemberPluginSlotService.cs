using System.Security.Claims;
using ClubGear.Models;
using ClubGear.Models.MemberActions;
using ClubGear.Plugin.Contracts;
using MemberPluginActionRequestModel = ClubGear.Models.MemberActions.PluginMemberActionRequest;

namespace ClubGear.Services.Abstractions;

public interface IMemberPluginSlotService
{
    Task<MemberPluginSlotSnapshot> GetSlotsAsync(
        Member member,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);

    Task<PluginMemberActionResult> ExecuteActionAsync(
        MemberPluginActionRequestModel request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}

public sealed record MemberPluginSlotSnapshot(
    IReadOnlyList<MemberPluginStatusBadgeView> StatusBadges,
    IReadOnlyList<MemberPluginDetailCardView> DetailCards,
    IReadOnlyList<MemberPluginEditTabView> EditTabs,
    IReadOnlyList<MemberPluginActionView> Actions)
{
    public static MemberPluginSlotSnapshot Empty { get; } = new(
        Array.Empty<MemberPluginStatusBadgeView>(),
        Array.Empty<MemberPluginDetailCardView>(),
        Array.Empty<MemberPluginEditTabView>(),
        Array.Empty<MemberPluginActionView>());
}

public sealed record MemberPluginStatusBadgeView(
    string ModuleId,
    string PluginDisplayName,
    MemberStatusBadgeSlot Badge,
    int SortOrder);

public sealed record MemberPluginDetailCardView(
    string ModuleId,
    string PluginDisplayName,
    MemberDetailCardSlot Card,
    int SortOrder);

public sealed record MemberPluginEditTabView(
    string ModuleId,
    string PluginDisplayName,
    MemberEditTabSlot Tab,
    int SortOrder)
{
    public string? GroupKey { get; init; }
    public string? GroupTitle { get; init; }
}

public sealed record MemberPluginActionView(
    string ModuleId,
    string PluginDisplayName,
    MemberActionSlot Action,
    int SortOrder);