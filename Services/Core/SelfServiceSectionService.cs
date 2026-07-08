using System.Security.Claims;
using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Runtime;

namespace ClubGear.Services.Core;

public sealed class SelfServiceSectionService : ISelfServiceSectionService
{
    private readonly IPluginRegistryReader _pluginRegistryReader;
    private readonly IPluginRuntimeAdapter _pluginRuntimeAdapter;
    private readonly ILogger<SelfServiceSectionService> _logger;

    public SelfServiceSectionService(
        IPluginRegistryReader pluginRegistryReader,
        IPluginRuntimeAdapter pluginRuntimeAdapter,
        ILogger<SelfServiceSectionService> logger)
    {
        _pluginRegistryReader = pluginRegistryReader;
        _pluginRuntimeAdapter = pluginRuntimeAdapter;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SelfServicePluginSectionView>> GetSelfServiceSectionsAsync(
        Member member,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(user);

        var memberDetail = MapMember(member);
        var sections = new List<SelfServicePluginSectionView>();

        foreach (var runtime in _pluginRegistryReader.GetRegisteredPlugins())
        {
            var module = _pluginRegistryReader.GetModule(runtime.ModuleId);
            if (module is null)
            {
                continue;
            }

            foreach (var contribution in runtime.SelfServiceProfileProviders.OrderBy(p => p.Order))
            {
                ISelfServiceProfileSectionProvider? provider;
                try
                {
                    provider = _pluginRegistryReader.CreateMemberProvider<ISelfServiceProfileSectionProvider>(
                        runtime.ModuleId, contribution.ProviderType);
                }
                catch (TypeLoadException ex)
                {
                    _logger.LogError(ex, "Provider {ProviderType} fuer Modul {ModuleId} konnte nicht instanziert werden. Moegliche Ursache: veraltete Contracts-Assembly im Host-Prozess.", contribution.ProviderType, runtime.ModuleId);
                    continue;
                }

                if (provider is null)
                {
                    continue;
                }

                try
                {
                    var section = await _pluginRuntimeAdapter.InvokeAsync(
                        module,
                        user,
                        (bridge, token) => provider.GetSectionAsync(memberDetail, bridge.Host, token),
                        requiredPermissionKey: null,
                        isolatedDelegate: provider.GetSectionAsync,
                        cancellationToken: cancellationToken);

                    if (section is not null)
                    {
                        sections.Add(new SelfServicePluginSectionView(
                            runtime.ModuleId,
                            runtime.DisplayName,
                            section,
                            contribution.Order + section.Order));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Self-Service-Sektion fuer Modul {ModuleId} konnte nicht geladen werden.", runtime.ModuleId);
                }
            }
        }

        return sections
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.ModuleId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<PluginMemberActionResult> ExecuteSelfServiceActionAsync(
        SelfServiceSectionActionRequest request,
        Member member,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(user);

        var runtime = _pluginRegistryReader.GetByModuleId(request.ModuleId);
        var module = _pluginRegistryReader.GetModule(request.ModuleId);
        if (runtime is null || module is null)
        {
            return new PluginMemberActionResult(false, "plugin-not-active", "Plugin nicht gefunden.");
        }

        var memberDetail = MapMember(member);

        foreach (var contribution in runtime.SelfServiceProfileProviders.OrderBy(p => p.Order))
        {
            ISelfServiceProfileSectionProvider? provider;
            try
            {
                provider = _pluginRegistryReader.CreateMemberProvider<ISelfServiceProfileSectionProvider>(
                    runtime.ModuleId, contribution.ProviderType);
            }
            catch (TypeLoadException ex)
            {
                _logger.LogError(ex, "Provider {ProviderType} fuer Modul {ModuleId} (Aktion {ActionKey}) konnte nicht instanziert werden. Moegliche Ursache: veraltete Contracts-Assembly im Host-Prozess.", contribution.ProviderType, runtime.ModuleId, request.ActionKey);
                return new PluginMemberActionResult(false, "plugin-type-load-error", "Plugin konnte nicht geladen werden.");
            }

            if (provider is null)
            {
                continue;
            }

            try
            {
                var contractRequest = new PluginMemberActionRequest(member.Id, request.ActionKey, request.Arguments);

                var result = await _pluginRuntimeAdapter.InvokeAsync(
                    module,
                    user,
                    (bridge, token) => provider.ExecuteSelfServiceActionAsync(contractRequest, memberDetail, bridge.Host, token),
                    requiredPermissionKey: null,
                    isolatedDelegate: provider.ExecuteSelfServiceActionAsync,
                    cancellationToken: cancellationToken);

                if (result is not null)
                {
                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Self-Service-Aktion {ActionKey} fuer Modul {ModuleId} fehlgeschlagen.", request.ActionKey, request.ModuleId);
            }
        }

        return new PluginMemberActionResult(false, "action-not-found", "Aktion nicht gefunden.");
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
