using System.Security.Claims;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Runtime;

namespace ClubGear.Services.Core;

public sealed class PluginAdminCommandService : IPluginAdminCommandService
{
    private readonly IPluginRegistryReader _pluginRegistryReader;
    private readonly IPluginRuntimeAdapter _pluginRuntimeAdapter;
    private readonly ILogger<PluginAdminCommandService> _logger;

    public PluginAdminCommandService(
        IPluginRegistryReader pluginRegistryReader,
        IPluginRuntimeAdapter pluginRuntimeAdapter,
        ILogger<PluginAdminCommandService> logger)
    {
        _pluginRegistryReader = pluginRegistryReader;
        _pluginRuntimeAdapter = pluginRuntimeAdapter;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PluginAdminModulePanels>> GetPanelsAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var modulePanels = new List<PluginAdminModulePanels>();

        foreach (var runtime in _pluginRegistryReader.GetRegisteredPlugins()
                     .OrderBy(candidate => candidate.DisplayName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(candidate => candidate.ModuleId, StringComparer.OrdinalIgnoreCase))
        {
            var module = _pluginRegistryReader.GetModule(runtime.ModuleId);
            if (module is null)
            {
                continue;
            }

            var bridge = _pluginRuntimeAdapter.CreateBridge(module, user);
            var visiblePanels = new List<PluginAdminPanel>();

            foreach (var contribution in GetAdminPanelProviderContributions(module)
                         .OrderBy(provider => provider.Order)
                         .ThenBy(provider => provider.ProviderType, StringComparer.OrdinalIgnoreCase))
            {
                var provider = _pluginRegistryReader.CreateMemberProvider<IAdminFunctionPanelProvider>(runtime.ModuleId, contribution.ProviderType);
                if (provider is null)
                {
                    continue;
                }

                try
                {
                    var panels = await _pluginRuntimeAdapter.InvokeAsync(
                        module,
                        user,
                        (runtimeBridge, token) => provider.GetPanelsAsync(runtimeBridge.Host, token),
                        isolatedDelegate: provider.GetPanelsAsync,
                        cancellationToken: cancellationToken);

                    foreach (var panel in panels.OrderBy(candidate => candidate.Order).ThenBy(candidate => candidate.Title, StringComparer.OrdinalIgnoreCase))
                    {
                        if (!await bridge.HasPermissionAsync(panel.PermissionKey, cancellationToken))
                        {
                            continue;
                        }

                        var commands = panel.Commands is null
                            ? null
                            : await FilterVisibleCommandsAsync(bridge, panel.Commands, cancellationToken);

                        visiblePanels.Add(panel with
                        {
                            Commands = commands
                        });
                    }
                }
                catch (PluginPermissionDeniedException)
                {
                    continue;
                }
                catch (UserFriendlyException ex)
                {
                    _logger.LogWarning(ex, "Plugin-Admin-Panels fuer Modul {ModuleId} konnten nicht geladen werden.", runtime.ModuleId);
                }
            }

            if (visiblePanels.Count == 0)
            {
                continue;
            }

            modulePanels.Add(new PluginAdminModulePanels(runtime.ModuleId, runtime.DisplayName, visiblePanels));
        }

        return modulePanels;
    }

    public async Task<PluginCommandResult> ExecuteCommandAsync(
        string moduleId,
        PluginAdminCommandRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(request.PanelKey) || string.IsNullOrWhiteSpace(request.CommandKey))
        {
            return new PluginCommandResult(false, "invalid", "Plugin-Admin-Befehl ist unvollstaendig.");
        }

        var runtime = _pluginRegistryReader.GetByModuleId(moduleId);
        var module = _pluginRegistryReader.GetModule(moduleId);
        if (runtime is null || module is null)
        {
            return new PluginCommandResult(false, "plugin-not-active", $"Plugin '{moduleId}' ist nicht aktiv.");
        }

        foreach (var contribution in GetAdminPanelProviderContributions(module)
                     .OrderBy(provider => provider.Order)
                     .ThenBy(provider => provider.ProviderType, StringComparer.OrdinalIgnoreCase))
        {
            var provider = _pluginRegistryReader.CreateMemberProvider<IAdminFunctionPanelProvider>(runtime.ModuleId, contribution.ProviderType);
            if (provider is null)
            {
                continue;
            }

            try
            {
                var panels = await _pluginRuntimeAdapter.InvokeAsync(
                    module,
                    user,
                    (bridge, token) => provider.GetPanelsAsync(bridge.Host, token),
                    isolatedDelegate: provider.GetPanelsAsync,
                    cancellationToken: cancellationToken);

                var panel = panels.SingleOrDefault(candidate =>
                    string.Equals(candidate.Key, request.PanelKey, StringComparison.OrdinalIgnoreCase));
                if (panel is null)
                {
                    continue;
                }

                var command = panel.Commands?.SingleOrDefault(candidate =>
                    string.Equals(candidate.Key, request.CommandKey, StringComparison.OrdinalIgnoreCase));
                if (command is null)
                {
                    return new PluginCommandResult(false, "command-not-found", $"Plugin-Befehl '{request.CommandKey}' wurde nicht gefunden.");
                }

                return await _pluginRuntimeAdapter.InvokeAsync(
                    module,
                    user,
                    (bridge, token) => provider.ExecuteCommandAsync(request, bridge.Host, token),
                    command.PermissionKey,
                    provider.ExecuteCommandAsync,
                    cancellationToken);
            }
            catch (PluginPermissionDeniedException ex)
            {
                return new PluginCommandResult(false, "forbidden", ex.Message);
            }
            catch (UserFriendlyException ex)
            {
                _logger.LogWarning(ex, "Plugin-Admin-Befehl {CommandKey} fuer Modul {ModuleId} fehlgeschlagen.", request.CommandKey, moduleId);
                return new PluginCommandResult(false, "plugin-error", ex.Message);
            }
        }

        return new PluginCommandResult(false, "panel-not-found", $"Plugin-Panel '{request.PanelKey}' wurde nicht gefunden.");
    }

    private static async Task<IReadOnlyList<PluginAdminCommandDescriptor>> FilterVisibleCommandsAsync(
        IPluginRuntimeBridge bridge,
        IReadOnlyList<PluginAdminCommandDescriptor> commands,
        CancellationToken cancellationToken)
    {
        var visibleCommands = new List<PluginAdminCommandDescriptor>();

        foreach (var command in commands.OrderBy(candidate => candidate.Order).ThenBy(candidate => candidate.Label, StringComparer.OrdinalIgnoreCase))
        {
            if (!await bridge.HasPermissionAsync(command.PermissionKey, cancellationToken))
            {
                continue;
            }

            visibleCommands.Add(command);
        }

        return visibleCommands;
    }

    private static IReadOnlyList<PluginAdminPanelProviderContribution> GetAdminPanelProviderContributions(IPluginModule module)
    {
        var sink = new ContributionSink();
        module.RegisterContributions(sink);
        return sink.AdminPanelProviders;
    }

    private sealed class ContributionSink : IPluginContributionSink
    {
        private readonly List<PluginAdminPanelProviderContribution> _adminPanelProviders = new();

        public IReadOnlyList<PluginAdminPanelProviderContribution> AdminPanelProviders => _adminPanelProviders;

        public void AddRoute(PluginRouteContribution contribution)
        {
        }

        public void AddService(PluginServiceContribution contribution)
        {
        }

        public void AddMemberProvider(PluginMemberProviderContribution contribution)
        {
        }

        public void AddBackgroundJob(PluginBackgroundJobContribution contribution)
        {
        }

        public void AddAdminPanelProvider(PluginAdminPanelProviderContribution contribution)
        {
            ArgumentNullException.ThrowIfNull(contribution);
            _adminPanelProviders.Add(contribution);
        }
    }
}