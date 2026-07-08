using System.Security.Claims;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Runtime;

namespace ClubGear.Services.Core;

public sealed class PluginPageService : IPluginPageService
{
    private readonly IPluginRegistryReader _pluginRegistryReader;
    private readonly IPluginRuntimeAdapter _pluginRuntimeAdapter;
    private readonly ILogger<PluginPageService> _logger;

    public PluginPageService(
        IPluginRegistryReader pluginRegistryReader,
        IPluginRuntimeAdapter pluginRuntimeAdapter,
        ILogger<PluginPageService> logger)
    {
        _pluginRegistryReader = pluginRegistryReader;
        _pluginRuntimeAdapter = pluginRuntimeAdapter;
        _logger = logger;
    }

    public async Task<PluginPageResult<PluginPageDefinition>> GetPageDefinitionAsync(
        string moduleId,
        string pageKey,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pageKey);
        ArgumentNullException.ThrowIfNull(user);

        var (provider, module, notFoundResult) = ResolveProvider<PluginPageDefinition>(moduleId, pageKey);
        if (notFoundResult is not null)
        {
            return notFoundResult;
        }

        try
        {
            var definition = await _pluginRuntimeAdapter.InvokeAsync(
                module!,
                user,
                (bridge, token) => provider!.GetPageDefinitionAsync(bridge.Host, token),
                isolatedDelegate: provider!.GetPageDefinitionAsync,
                cancellationToken: ct);

            if (definition.ListPermission is not null)
            {
                var bridge = _pluginRuntimeAdapter.CreateBridge(module!, user);
                if (!await bridge.HasPermissionAsync(definition.ListPermission, ct))
                {
                    return PluginPageResult<PluginPageDefinition>.Forbidden();
                }
            }

            return PluginPageResult<PluginPageDefinition>.Success(definition);
        }
        catch (PluginPermissionDeniedException)
        {
            return PluginPageResult<PluginPageDefinition>.Forbidden();
        }
        catch (UserFriendlyException ex)
        {
            _logger.LogWarning(ex, "GetPageDefinitionAsync fehlgeschlagen fuer Modul {ModuleId}, Seite {PageKey}.", moduleId, pageKey);
            return PluginPageResult<PluginPageDefinition>.Error(ex.Message);
        }
    }

    public async Task<PluginPageResult<IReadOnlyList<IReadOnlyDictionary<string, string?>>>> GetRowsAsync(
        string moduleId,
        string pageKey,
        ClaimsPrincipal user,
        string? filterValue,
        string? entityKey,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pageKey);
        ArgumentNullException.ThrowIfNull(user);

        var (provider, module, notFoundResult) = ResolveProvider<IReadOnlyList<IReadOnlyDictionary<string, string?>>>(moduleId, pageKey);
        if (notFoundResult is not null)
        {
            return notFoundResult;
        }

        try
        {
            var definition = await _pluginRuntimeAdapter.InvokeAsync(
                module!,
                user,
                (bridge, token) => provider!.GetPageDefinitionAsync(bridge.Host, token),
                isolatedDelegate: provider!.GetPageDefinitionAsync,
                cancellationToken: ct);

            if (definition.ListPermission is not null)
            {
                var bridge = _pluginRuntimeAdapter.CreateBridge(module!, user);
                if (!await bridge.HasPermissionAsync(definition.ListPermission, ct))
                {
                    return PluginPageResult<IReadOnlyList<IReadOnlyDictionary<string, string?>>>.Forbidden();
                }
            }

            var rows = await _pluginRuntimeAdapter.InvokeAsync(
                module!,
                user,
                (bridge, token) => provider!.GetRowsAsync(bridge.Host, filterValue, entityKey, token),
                isolatedDelegate: provider!.GetRowsAsync,
                cancellationToken: ct);

            return PluginPageResult<IReadOnlyList<IReadOnlyDictionary<string, string?>>>.Success(rows);
        }
        catch (PluginPermissionDeniedException)
        {
            return PluginPageResult<IReadOnlyList<IReadOnlyDictionary<string, string?>>>.Forbidden();
        }
        catch (UserFriendlyException ex)
        {
            _logger.LogWarning(ex, "GetRowsAsync fehlgeschlagen fuer Modul {ModuleId}, Seite {PageKey}.", moduleId, pageKey);
            return PluginPageResult<IReadOnlyList<IReadOnlyDictionary<string, string?>>>.Error(ex.Message);
        }
    }

    public async Task<PluginPageResult<PluginCommandResult>> ExecuteCommandAsync(
        string moduleId,
        string pageKey,
        string commandKey,
        string? entityKey,
        IReadOnlyDictionary<string, string> arguments,
        ClaimsPrincipal user,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pageKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandKey);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(user);

        var (provider, module, notFoundResult) = ResolveProvider<PluginCommandResult>(moduleId, pageKey);
        if (notFoundResult is not null)
        {
            return notFoundResult;
        }

        try
        {
            var definition = await _pluginRuntimeAdapter.InvokeAsync(
                module!,
                user,
                (bridge, token) => provider!.GetPageDefinitionAsync(bridge.Host, token),
                isolatedDelegate: provider!.GetPageDefinitionAsync,
                cancellationToken: ct);

            var command = definition.Commands.FirstOrDefault(c =>
                string.Equals(c.Key, commandKey, StringComparison.OrdinalIgnoreCase));

            if (command?.RequiredPermission is not null)
            {
                var bridge = _pluginRuntimeAdapter.CreateBridge(module!, user);
                if (!await bridge.HasPermissionAsync(command.RequiredPermission, ct))
                {
                    return PluginPageResult<PluginCommandResult>.Forbidden();
                }
            }

            var result = await _pluginRuntimeAdapter.InvokeAsync(
                module!,
                user,
                (bridge, token) => provider!.ExecuteCommandAsync(bridge.Host, commandKey, entityKey, arguments, token),
                isolatedDelegate: provider!.ExecuteCommandAsync,
                cancellationToken: ct);

            return PluginPageResult<PluginCommandResult>.Success(result);
        }
        catch (PluginPermissionDeniedException)
        {
            return PluginPageResult<PluginCommandResult>.Forbidden();
        }
        catch (UserFriendlyException ex)
        {
            _logger.LogWarning(ex, "ExecuteCommandAsync fehlgeschlagen fuer Modul {ModuleId}, Seite {PageKey}, Befehl {CommandKey}.", moduleId, pageKey, commandKey);
            return PluginPageResult<PluginCommandResult>.Error(ex.Message);
        }
    }

    private (IPluginPageProvider? Provider, IPluginModule? Module, PluginPageResult<T>? NotFound)
        ResolveProvider<T>(string moduleId, string pageKey)
    {
        var module = _pluginRegistryReader.GetModule(moduleId);
        if (module is null)
        {
            return (null, null, PluginPageResult<T>.NotFound());
        }

        var contribution = GetPageProviderContributions(module)
            .OrderBy(c => c.Order)
            .ThenBy(c => c.ProviderType, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (contribution is null)
        {
            return (null, null, PluginPageResult<T>.NotFound());
        }

        var provider = _pluginRegistryReader.CreateMemberProvider<IPluginPageProvider>(
            moduleId, contribution.ProviderType);

        if (provider is null)
        {
            return (null, null, PluginPageResult<T>.NotFound());
        }

        return (provider, module, null);
    }

    private static IReadOnlyList<PluginPageProviderContribution> GetPageProviderContributions(IPluginModule module)
    {
        var sink = new ContributionSink();
        module.RegisterContributions(sink);
        return sink.PageProviders;
    }

    private sealed class ContributionSink : IPluginContributionSink
    {
        private readonly List<PluginPageProviderContribution> _pageProviders = new();

        public IReadOnlyList<PluginPageProviderContribution> PageProviders => _pageProviders;

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
        }

        public void AddNavEntries(IReadOnlyList<PluginNavEntry> entries)
        {
        }

        public void AddPageProvider(PluginPageProviderContribution contribution)
        {
            ArgumentNullException.ThrowIfNull(contribution);
            _pageProviders.Add(contribution);
        }
    }
}
