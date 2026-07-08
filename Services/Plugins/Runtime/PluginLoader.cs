using System.Reflection;
using System.Runtime.Loader;
using ClubGear.Plugin.Contracts;
using ClubGear.Models;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Plugins.Runtime;

public sealed class PluginLoader
{
    private readonly IPluginPackageStore _packageStore;
    private readonly ILogger<PluginLoader> _logger;

    public PluginLoader(IPluginPackageStore packageStore, ILogger<PluginLoader> logger)
    {
        _packageStore = packageStore;
        _logger = logger;
    }

    public async Task<PluginLoadResult> LoadAsync(PluginStatusRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.PackagePath))
        {
            return PluginLoadResult.CreateFailure($"Plugin '{record.Key}' hat keinen gespeicherten Paketpfad.");
        }

        if (string.IsNullOrWhiteSpace(record.EntryPoint))
        {
            return PluginLoadResult.CreateFailure($"Plugin '{record.Key}' hat keinen Entry Point.");
        }

        try
        {
            var extractionPath = await _packageStore.EnsureExtractedAsync(record.Key, record.PackageHash, record.PackagePath, cancellationToken);
            var loadContext = new PluginAssemblyLoadContext(record.Key, extractionPath);
            var module = CreateModuleInstance(loadContext, extractionPath, record.EntryPoint, out var error);
            if (module is null)
            {
                loadContext.Unload();
                return PluginLoadResult.CreateFailure(error ?? $"Plugin '{record.Key}' konnte nicht geladen werden.");
            }

            if (!string.Equals(module.Manifest.Key, record.Key, StringComparison.OrdinalIgnoreCase))
            {
                loadContext.Unload();
                return PluginLoadResult.CreateFailure(
                    $"Manifest-Schluessel '{module.Manifest.Key}' passt nicht zum installierten Plugin '{record.Key}'.");
            }

            var collector = new PluginContributionCollector();
            module.RegisterContributions(collector);

            var runtime = new RegisteredPluginRuntime(
                module.Manifest.ModuleId,
                module.Manifest.DisplayName,
                module.Manifest.PluginVersion,
                loadContext.Name ?? $"plugin:{record.Key}",
                collector.Routes,
                collector.Services,
                collector.MemberProviders,
                collector.BackgroundJobs,
                collector.NavEntries,
                collector.AuditSinks,
                collector.IdentityProviders,
                collector.SelfServiceProfileProviders);

            return PluginLoadResult.CreateSuccess(new LoadedPluginRuntime(module, loadContext, runtime));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plugin {PluginKey} konnte nicht geladen werden.", record.Key);
            return PluginLoadResult.CreateFailure(ex.Message);
        }
    }

    private static IPluginModule? CreateModuleInstance(
        PluginAssemblyLoadContext loadContext,
        string extractionPath,
        string entryPoint,
        out string? error)
    {
        foreach (var assemblyPath in Directory.EnumerateFiles(extractionPath, "*.dll", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            Assembly assembly;
            try
            {
                assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            }
            catch (BadImageFormatException)
            {
                continue;
            }
            catch (FileLoadException)
            {
                continue;
            }

            var entryPointType = assembly.GetType(entryPoint, throwOnError: false, ignoreCase: false);
            if (entryPointType is null)
            {
                continue;
            }

            if (!typeof(IPluginModule).IsAssignableFrom(entryPointType))
            {
                error = $"Entry Point '{entryPoint}' implementiert IPluginModule nicht.";
                return null;
            }

            if (Activator.CreateInstance(entryPointType) is not IPluginModule module)
            {
                error = $"Entry Point '{entryPoint}' konnte nicht instanziiert werden.";
                return null;
            }

            error = null;
            return module;
        }

        error = $"Entry Point '{entryPoint}' wurde im Plugin-Paket nicht gefunden.";
        return null;
    }

    private sealed class PluginContributionCollector : IPluginContributionSink
    {
        private readonly List<PluginRouteContribution> _routes = new();
        private readonly List<PluginServiceContribution> _services = new();
        private readonly List<PluginMemberProviderContribution> _memberProviders = new();
        private readonly List<PluginBackgroundJobContribution> _backgroundJobs = new();
        private readonly List<PluginNavEntry> _navEntries = new();
        private readonly List<PluginAuditSinkContribution> _auditSinks = new();
        private readonly List<PluginIdentityProviderContribution> _identityProviders = new();
        private readonly List<PluginSelfServiceProfileProviderContribution> _selfServiceProfileProviders = new();

        public IReadOnlyList<PluginRouteContribution> Routes => _routes.ToArray();

        public IReadOnlyList<PluginServiceContribution> Services => _services.ToArray();

        public IReadOnlyList<PluginMemberProviderContribution> MemberProviders => _memberProviders.ToArray();

        public IReadOnlyList<PluginBackgroundJobContribution> BackgroundJobs => _backgroundJobs.ToArray();

        public IReadOnlyList<PluginNavEntry> NavEntries => _navEntries.ToArray();

        public IReadOnlyList<PluginAuditSinkContribution> AuditSinks => _auditSinks.ToArray();

        public IReadOnlyList<PluginIdentityProviderContribution> IdentityProviders => _identityProviders.ToArray();

        public IReadOnlyList<PluginSelfServiceProfileProviderContribution> SelfServiceProfileProviders => _selfServiceProfileProviders.ToArray();

        public void AddRoute(PluginRouteContribution contribution)
        {
            ArgumentNullException.ThrowIfNull(contribution);
            _routes.Add(contribution);
        }

        public void AddService(PluginServiceContribution contribution)
        {
            ArgumentNullException.ThrowIfNull(contribution);
            _services.Add(contribution);
        }

        public void AddMemberProvider(PluginMemberProviderContribution contribution)
        {
            ArgumentNullException.ThrowIfNull(contribution);
            _memberProviders.Add(contribution);
        }

        public void AddBackgroundJob(PluginBackgroundJobContribution contribution)
        {
            ArgumentNullException.ThrowIfNull(contribution);
            _backgroundJobs.Add(contribution);
        }

        public void AddNavEntries(IReadOnlyList<PluginNavEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
            _navEntries.AddRange(entries);
        }

        public void AddAuditSink(PluginAuditSinkContribution contribution)
        {
            ArgumentNullException.ThrowIfNull(contribution);
            _auditSinks.Add(contribution);
        }

        public void AddIdentityProvider(PluginIdentityProviderContribution contribution)
        {
            ArgumentNullException.ThrowIfNull(contribution);
            _identityProviders.Add(contribution);
        }

        public void AddSelfServiceProfileSection(PluginSelfServiceProfileProviderContribution contribution)
        {
            ArgumentNullException.ThrowIfNull(contribution);
            _selfServiceProfileProviders.Add(contribution);
        }
    }

    private sealed class PluginAssemblyLoadContext : AssemblyLoadContext
    {
        private static readonly string ContractsAssemblyName = typeof(IPluginModule).Assembly.GetName().Name!;
        private readonly Dictionary<string, string> _assemblyPaths;

        public PluginAssemblyLoadContext(string pluginKey, string extractionPath)
            : base($"plugin:{pluginKey}:{Guid.NewGuid():N}", isCollectible: true)
        {
            _assemblyPaths = Directory.EnumerateFiles(extractionPath, "*.dll", SearchOption.AllDirectories)
                .ToDictionary(
                    path => Path.GetFileNameWithoutExtension(path),
                    path => path,
                    StringComparer.OrdinalIgnoreCase);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (string.Equals(assemblyName.Name, ContractsAssemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return Default.LoadFromAssemblyName(assemblyName);
            }

            return assemblyName.Name is not null && _assemblyPaths.TryGetValue(assemblyName.Name, out var assemblyPath)
                ? LoadFromAssemblyPath(assemblyPath)
                : null;
        }
    }
}

public sealed record LoadedPluginRuntime(
    IPluginModule Module,
    AssemblyLoadContext LoadContext,
    RegisteredPluginRuntime Runtime);

public sealed record PluginLoadResult(bool Success, string? Error, LoadedPluginRuntime? LoadedPlugin)
{
    public static PluginLoadResult CreateSuccess(LoadedPluginRuntime loadedPlugin)
        => new(true, null, loadedPlugin);

    public static PluginLoadResult CreateFailure(string error)
        => new(false, error, null);
}