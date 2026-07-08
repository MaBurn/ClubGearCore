using System.Security.Claims;
using ClubGear.Models;
using ClubGear.Models.MemberActions;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Runtime;
using MemberPluginActionRequestModel = ClubGear.Models.MemberActions.PluginMemberActionRequest;

namespace ClubGear.Services.Core;

public sealed class MemberPluginSlotService : IMemberPluginSlotService
{
    private readonly IPluginRegistryReader _pluginRegistryReader;
    private readonly IPluginRuntimeAdapter _pluginRuntimeAdapter;
    private readonly IMemberFeatureService _memberFeatureService;
    private readonly ILogger<MemberPluginSlotService> _logger;

    public MemberPluginSlotService(
        IPluginRegistryReader pluginRegistryReader,
        IPluginRuntimeAdapter pluginRuntimeAdapter,
        IMemberFeatureService memberFeatureService,
        ILogger<MemberPluginSlotService> logger)
    {
        _pluginRegistryReader = pluginRegistryReader;
        _pluginRuntimeAdapter = pluginRuntimeAdapter;
        _memberFeatureService = memberFeatureService;
        _logger = logger;
    }

    public async Task<MemberPluginSlotSnapshot> GetSlotsAsync(
        Member member,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(user);

        var memberDetail = MapMember(member);
        var badges = new List<MemberPluginStatusBadgeView>();
        var cards = new List<MemberPluginDetailCardView>();
        var tabs = new List<MemberPluginEditTabView>();
        var actions = new List<MemberPluginActionView>();

        foreach (var runtime in _pluginRegistryReader.GetRegisteredPlugins())
        {
            var module = _pluginRegistryReader.GetModule(runtime.ModuleId);
            if (module is null)
            {
                continue;
            }

            foreach (var contribution in runtime.MemberProviders.OrderBy(provider => provider.Order).ThenBy(provider => provider.ProviderType, StringComparer.OrdinalIgnoreCase))
            {
                switch (contribution.SlotKind)
                {
                    case PluginMemberSlotKind.DetailCard:
                        await CollectDetailCardsAsync(runtime, module, contribution, memberDetail, user, cards, cancellationToken);
                        break;
                    case PluginMemberSlotKind.EditTab:
                        await CollectEditTabsAsync(runtime, module, contribution, memberDetail, user, tabs, cancellationToken);
                        break;
                    case PluginMemberSlotKind.StatusBadge:
                        await CollectStatusBadgesAsync(runtime, module, contribution, memberDetail, user, badges, cancellationToken);
                        break;
                    case PluginMemberSlotKind.Action:
                        await CollectActionsAsync(runtime, module, contribution, memberDetail, user, actions, cancellationToken);
                        break;
                }
            }
        }

        return new MemberPluginSlotSnapshot(
            badges.OrderBy(view => view.SortOrder).ThenBy(view => view.ModuleId, StringComparer.OrdinalIgnoreCase).ToArray(),
            cards.OrderBy(view => view.SortOrder).ThenBy(view => view.ModuleId, StringComparer.OrdinalIgnoreCase).ToArray(),
            tabs.OrderBy(view => view.SortOrder).ThenBy(view => view.ModuleId, StringComparer.OrdinalIgnoreCase).ToArray(),
            actions.OrderBy(view => view.SortOrder).ThenBy(view => view.ModuleId, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public async Task<PluginMemberActionResult> ExecuteActionAsync(
        MemberPluginActionRequestModel request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(request.ModuleId) || string.IsNullOrWhiteSpace(request.ActionKey))
        {
            return new PluginMemberActionResult(false, "invalid", "Plugin-Aktion ist unvollstaendig.");
        }

        var runtime = _pluginRegistryReader.GetByModuleId(request.ModuleId);
        var module = _pluginRegistryReader.GetModule(request.ModuleId);
        if (runtime is null || module is null)
        {
            return new PluginMemberActionResult(false, "plugin-not-active", $"Plugin '{request.ModuleId}' ist nicht aktiv.");
        }

        var member = await _memberFeatureService.GetByIdAsync(request.MemberId, cancellationToken);
        if (member is null)
        {
            return new PluginMemberActionResult(false, "member-not-found", $"Mitglied '{request.MemberId}' wurde nicht gefunden.");
        }

        var memberDetail = MapMember(member);
        foreach (var contribution in runtime.MemberProviders.Where(provider => provider.SlotKind == PluginMemberSlotKind.Action)
                     .OrderBy(provider => provider.Order)
                     .ThenBy(provider => provider.ProviderType, StringComparer.OrdinalIgnoreCase))
        {
            var provider = _pluginRegistryReader.CreateMemberProvider<IMemberActionProvider>(request.ModuleId, contribution.ProviderType);
            if (provider is null)
            {
                continue;
            }

            try
            {
                var availableActions = await _pluginRuntimeAdapter.InvokeAsync(
                    module,
                    user,
                    (bridge, token) => provider.GetActionsAsync(memberDetail, bridge.Host, token),
                    isolatedDelegate: provider.GetActionsAsync,
                    cancellationToken: cancellationToken);

                var action = availableActions.SingleOrDefault(candidate => string.Equals(candidate.Key, request.ActionKey, StringComparison.OrdinalIgnoreCase));
                if (action is null)
                {
                    continue;
                }

                return await _pluginRuntimeAdapter.InvokeAsync(
                    module,
                    user,
                    (bridge, token) => provider.ExecuteAsync(
                        new ClubGear.Plugin.Contracts.PluginMemberActionRequest(request.MemberId, request.ActionKey, request.Arguments),
                        memberDetail,
                        bridge.Host,
                        token),
                    action.PermissionKey,
                    provider.ExecuteAsync,
                    cancellationToken);
            }
            catch (PluginPermissionDeniedException ex)
            {
                return new PluginMemberActionResult(false, "forbidden", ex.Message);
            }
            catch (UserFriendlyException ex)
            {
                _logger.LogWarning(ex, "Plugin-Aktion {ActionKey} fuer Modul {ModuleId} fehlgeschlagen.", request.ActionKey, request.ModuleId);
                return new PluginMemberActionResult(false, "plugin-error", ex.Message);
            }
        }

        return new PluginMemberActionResult(false, "action-not-found", $"Plugin-Aktion '{request.ActionKey}' wurde nicht gefunden.");
    }

    private async Task CollectDetailCardsAsync(
        RegisteredPluginRuntime runtime,
        IPluginModule module,
        PluginMemberProviderContribution contribution,
        PluginMemberDetail member,
        ClaimsPrincipal user,
        List<MemberPluginDetailCardView> cards,
        CancellationToken cancellationToken)
    {
        var provider = _pluginRegistryReader.CreateMemberProvider<IMemberDetailCardProvider>(runtime.ModuleId, contribution.ProviderType);
        if (provider is null)
        {
            return;
        }

        try
        {
            var resolved = await _pluginRuntimeAdapter.InvokeAsync(
                module,
                user,
                (bridge, token) => provider.GetCardsAsync(member, bridge.Host, token),
                isolatedDelegate: provider.GetCardsAsync,
                cancellationToken: cancellationToken);

            cards.AddRange(resolved.Select(card => new MemberPluginDetailCardView(
                runtime.ModuleId,
                runtime.DisplayName,
                card,
                contribution.Order + card.Order)));
        }
        catch (UserFriendlyException ex)
        {
            _logger.LogWarning(ex, "Plugin-Detailkarten fuer Modul {ModuleId} konnten nicht geladen werden.", runtime.ModuleId);
        }
    }

    private async Task CollectEditTabsAsync(
        RegisteredPluginRuntime runtime,
        IPluginModule module,
        PluginMemberProviderContribution contribution,
        PluginMemberDetail member,
        ClaimsPrincipal user,
        List<MemberPluginEditTabView> tabs,
        CancellationToken cancellationToken)
    {
        var provider = _pluginRegistryReader.CreateMemberProvider<IMemberEditTabProvider>(runtime.ModuleId, contribution.ProviderType);
        if (provider is null)
        {
            return;
        }

        try
        {
            var resolved = await _pluginRuntimeAdapter.InvokeAsync(
                module,
                user,
                (bridge, token) => provider.GetTabsAsync(member, bridge.Host, token),
                isolatedDelegate: provider.GetTabsAsync,
                cancellationToken: cancellationToken);

            tabs.AddRange(resolved.Select(tab => new MemberPluginEditTabView(
                runtime.ModuleId,
                runtime.DisplayName,
                tab,
                contribution.Order + tab.Order)
            {
                GroupKey = tab.GroupKey,
                GroupTitle = tab.GroupTitle,
            }));
        }
        catch (UserFriendlyException ex)
        {
            _logger.LogWarning(ex, "Plugin-Edit-Tabs fuer Modul {ModuleId} konnten nicht geladen werden.", runtime.ModuleId);
        }
    }

    private async Task CollectStatusBadgesAsync(
        RegisteredPluginRuntime runtime,
        IPluginModule module,
        PluginMemberProviderContribution contribution,
        PluginMemberDetail member,
        ClaimsPrincipal user,
        List<MemberPluginStatusBadgeView> badges,
        CancellationToken cancellationToken)
    {
        var provider = _pluginRegistryReader.CreateMemberProvider<IMemberStatusBadgeProvider>(runtime.ModuleId, contribution.ProviderType);
        if (provider is null)
        {
            return;
        }

        try
        {
            var resolved = await _pluginRuntimeAdapter.InvokeAsync(
                module,
                user,
                (bridge, token) => provider.GetBadgesAsync(member, bridge.Host, token),
                isolatedDelegate: provider.GetBadgesAsync,
                cancellationToken: cancellationToken);

            badges.AddRange(resolved.Select(badge => new MemberPluginStatusBadgeView(
                runtime.ModuleId,
                runtime.DisplayName,
                badge,
                contribution.Order + badge.Order)));
        }
        catch (UserFriendlyException ex)
        {
            _logger.LogWarning(ex, "Plugin-Statusbadges fuer Modul {ModuleId} konnten nicht geladen werden.", runtime.ModuleId);
        }
    }

    private async Task CollectActionsAsync(
        RegisteredPluginRuntime runtime,
        IPluginModule module,
        PluginMemberProviderContribution contribution,
        PluginMemberDetail member,
        ClaimsPrincipal user,
        List<MemberPluginActionView> actions,
        CancellationToken cancellationToken)
    {
        var provider = _pluginRegistryReader.CreateMemberProvider<IMemberActionProvider>(runtime.ModuleId, contribution.ProviderType);
        if (provider is null)
        {
            return;
        }

        try
        {
            var resolved = await _pluginRuntimeAdapter.InvokeAsync(
                module,
                user,
                (bridge, token) => provider.GetActionsAsync(member, bridge.Host, token),
                isolatedDelegate: provider.GetActionsAsync,
                cancellationToken: cancellationToken);

            foreach (var action in resolved.Where(action =>
                         module.Manifest.Permissions.Contains(action.PermissionKey, StringComparer.OrdinalIgnoreCase)))
            {
                var isAllowed = await _pluginRuntimeAdapter.InvokeAsync(
                    module,
                    user,
                    (bridge, token) => bridge.HasPermissionAsync(action.PermissionKey, token),
                    isolatedDelegate: provider.GetActionsAsync,
                    cancellationToken: cancellationToken);

                if (!isAllowed)
                {
                    continue;
                }

                actions.Add(new MemberPluginActionView(
                    runtime.ModuleId,
                    runtime.DisplayName,
                    action,
                    contribution.Order + action.Order));
            }
        }
        catch (UserFriendlyException ex)
        {
            _logger.LogWarning(ex, "Plugin-Aktionen fuer Modul {ModuleId} konnten nicht geladen werden.", runtime.ModuleId);
        }
    }

    private static PluginMemberDetail MapMember(Member member)
    {
        return new PluginMemberDetail(
            member.Id,
            member.MemberNumber ?? string.Empty,
            member.FullName,
            member.FirstName,
            member.LastName,
            member.Email,
            member.PhoneNumber,
            member.IsActive);
    }
}
