using System.Security.Claims;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Runtime;

namespace ClubGear.Services.Core;

public sealed class PluginPageService : IPluginPageService
{
    private readonly IPluginRegistryReader _pluginRegistryReader;
    private readonly IPluginContributionActivator _contributionActivator;
    private readonly IPluginRuntimeAdapter _pluginRuntimeAdapter;
    private readonly ILogger<PluginPageService> _logger;

    public PluginPageService(
        IPluginRegistryReader pluginRegistryReader,
        IPluginContributionActivator contributionActivator,
        IPluginRuntimeAdapter pluginRuntimeAdapter,
        ILogger<PluginPageService> logger)
    {
        _pluginRegistryReader = pluginRegistryReader;
        _contributionActivator = contributionActivator;
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

        var resolved = ResolveProvider<PluginPageDefinition>(moduleId, pageKey, user);
        await using var lease = resolved.Lease;
        if (resolved.NotFound is not null)
        {
            return resolved.NotFound;
        }

        var provider = lease.Instance;
        var module = resolved.Module;

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

        var resolved = ResolveProvider<IReadOnlyList<IReadOnlyDictionary<string, string?>>>(moduleId, pageKey, user);
        await using var lease = resolved.Lease;
        if (resolved.NotFound is not null)
        {
            return resolved.NotFound;
        }

        var provider = lease.Instance;
        var module = resolved.Module;

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

    /// <summary>
    /// Upper bound on rows per request. A plugin may advertise any DefaultPageSize and a user
    /// may put anything in the query string; neither gets to ask the plugin for a million rows.
    /// </summary>
    public const int MaxPageSize = 200;

    public async Task<PluginPageResult<PluginPageRows>> GetRowsPageAsync(
        string moduleId,
        string pageKey,
        ClaimsPrincipal user,
        PluginPageQuery query,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(pageKey);
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(query);

        var resolved = ResolveProvider<PluginPageRows>(moduleId, pageKey, user);
        await using var lease = resolved.Lease;
        if (resolved.NotFound is not null)
        {
            return resolved.NotFound;
        }

        var provider = lease.Instance;
        var module = resolved.Module;

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
                    return PluginPageResult<PluginPageRows>.Forbidden();
                }
            }

            var effectiveQuery = Clamp(query, definition);

            var rows = await _pluginRuntimeAdapter.InvokeAsync(
                module!,
                user,
                (bridge, token) => provider!.GetRowsPageAsync(bridge.Host, effectiveQuery, token),
                isolatedDelegate: provider!.GetRowsPageAsync,
                cancellationToken: ct);

            return PluginPageResult<PluginPageRows>.Success(rows);
        }
        catch (PluginPermissionDeniedException)
        {
            return PluginPageResult<PluginPageRows>.Forbidden();
        }
        catch (UserFriendlyException ex)
        {
            _logger.LogWarning(ex, "GetRowsPageAsync fehlgeschlagen fuer Modul {ModuleId}, Seite {PageKey}.", moduleId, pageKey);
            return PluginPageResult<PluginPageRows>.Error(ex.Message);
        }
    }

    /// <summary>
    /// Normalises the request before it reaches the plugin: page size within bounds, page at
    /// least 1, and a sort key the definition actually marked sortable. A plugin should never
    /// have to defend against a hand-edited query string.
    /// </summary>
    internal static PluginPageQuery Clamp(PluginPageQuery query, PluginPageDefinition definition)
    {
        var requestedSize = query.PageSize ?? definition.DefaultPageSize;

        int? pageSize = requestedSize is > 0
            ? Math.Min(requestedSize.Value, MaxPageSize)
            : null;

        var sortKey = query.SortKey;
        if (!string.IsNullOrWhiteSpace(sortKey)
            && !definition.Columns.Any(c => c.Sortable
                && string.Equals(c.Key, sortKey, StringComparison.OrdinalIgnoreCase)))
        {
            sortKey = null;
        }

        return query with
        {
            PageSize = pageSize,
            Page = query.Page < 1 ? 1 : query.Page,
            SortKey = sortKey
        };
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

        var resolved = ResolveProvider<PluginCommandResult>(moduleId, pageKey, user);
        await using var lease = resolved.Lease;
        if (resolved.NotFound is not null)
        {
            return resolved.NotFound;
        }

        var provider = lease.Instance;
        var module = resolved.Module;

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

    private (PluginContributionLease<IPluginPageProvider> Lease, IPluginModule? Module, PluginPageResult<T>? NotFound)
        ResolveProvider<T>(string moduleId, string pageKey, ClaimsPrincipal user)
    {
        var module = _pluginRegistryReader.GetModule(moduleId);
        if (module is null)
        {
            return (PluginContributionLease<IPluginPageProvider>.Empty(moduleId, pageKey, "Plugin nicht aktiv."), null, PluginPageResult<T>.NotFound());
        }

        var contributions = GetPageProviderContributions(module);

        // A module serving one page key per provider is matched by key. A module that never
        // declared PageKey (pre-1.14.0 shape: exactly one provider for the whole module) keeps
        // resolving the same way it always did, regardless of which page key was requested.
        var contribution = contributions
            .Where(c => string.Equals(c.PageKey, pageKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Order)
            .ThenBy(c => c.ProviderType, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? contributions
                .Where(c => c.PageKey is null)
                .OrderBy(c => c.Order)
                .ThenBy(c => c.ProviderType, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

        if (contribution is null)
        {
            return (
                PluginContributionLease<IPluginPageProvider>.Empty(moduleId, pageKey, "Kein Seiten-Provider deklariert."),
                null,
                PluginPageResult<T>.NotFound());
        }

        var lease = _contributionActivator.Rent<IPluginPageProvider>(moduleId, contribution.ProviderType, user);
        if (lease.Instance is null)
        {
            // CORE-18. The page still reports "not found" so a broken plugin cannot take the page
            // down, but the reason no longer disappears.
            _logger.LogWarning(
                "Seiten-Provider '{ProviderType}' aus Modul {ModuleId} konnte nicht erzeugt werden: {Reason}",
                contribution.ProviderType,
                moduleId,
                lease.Error);
            return (lease, null, PluginPageResult<T>.NotFound());
        }

        return (lease, module, null);
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
