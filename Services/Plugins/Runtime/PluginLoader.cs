using System.Reflection;
using System.Runtime.Loader;
using ClubGear.Plugin.Contracts;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins;

namespace ClubGear.Services.Plugins.Runtime;

public sealed class PluginLoader
{
    private readonly IPluginPackageStore _packageStore;
    private readonly IContractCompatibilityService _contractCompatibility;
    private readonly ILogger<PluginLoader> _logger;

    // The compatibility service is optional so the many call sites that only need a loader can
    // keep using `new PluginLoader(store, logger)`; DI supplies the registered implementation and
    // a test can substitute one. The default is the real service, so the contract floor applies
    // everywhere unless a test deliberately opts out.
    public PluginLoader(
        IPluginPackageStore packageStore,
        ILogger<PluginLoader> logger,
        IContractCompatibilityService? contractCompatibility = null)
    {
        _packageStore = packageStore;
        _logger = logger;
        _contractCompatibility = contractCompatibility ?? new ContractCompatibilityService();
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
            var module = CreateModuleInstance(loadContext, extractionPath, record.Key, record.EntryPoint, out var error);
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

            // CORE-18. The install-time check in PluginInstallerService only ever sees a package
            // once. An already-installed plugin is never re-gated, so raising the contract floor
            // alone would let a stale plugin reach the constructor check and be reported as
            // "no (IPluginHostContext) constructor" — true, but the wrong diagnosis. Checking the
            // floor here makes the 1.13.0 break surface as a legible version rejection.
            var compatibility = _contractCompatibility.Validate(module.Manifest.RequiredContractVersion);
            if (!compatibility.IsCompatible)
            {
                loadContext.Unload();
                return PluginLoadResult.CreateFailure(
                    $"Plugin '{record.Key}' ist nicht mit dieser Host-Version kompatibel: "
                    + $"Es verlangt Contract >= {module.Manifest.RequiredContractVersion}, der Host unterstuetzt "
                    + $"{ContractVersion.MinimumSupported} bis {ContractVersion.Current}. "
                    + $"{compatibility.Reason}");
            }

            var collector = new PluginContributionCollector();
            module.RegisterContributions(collector);

            // CORE-13. An unknown capability key is a load-time rejection, not a silent no-op:
            // a plugin whose provider is never reachable would otherwise look installed and
            // healthy while doing nothing.
            var unknownCapability = collector.Services
                .FirstOrDefault(service => !PluginCapabilityKeys.IsKnown(service.Key));

            if (unknownCapability is not null)
            {
                loadContext.Unload();
                return PluginLoadResult.CreateFailure(
                    $"Plugin '{record.Key}' deklariert die unbekannte Capability '{unknownCapability.Key}'. "
                    + $"Erlaubt sind: {string.Join(", ", PluginCapabilityKeys.All.OrderBy(key => key, StringComparer.Ordinal))}.");
            }

            // CORE-16. Every type the plugin declares as a contribution is gated before it can be
            // registered: it has to exist in the module's own assembly, and it has to pass the same
            // isolation scan the entry point already passed. A contribution the host cannot resolve
            // is a broken plugin, not an empty extension point, so it is rejected rather than
            // silently dropped.
            var contributionFailure = FindContributionFailure(record.Key, module, collector);
            if (contributionFailure is not null)
            {
                loadContext.Unload();
                return PluginLoadResult.CreateFailure(contributionFailure);
            }

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
        string pluginKey,
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

            // CORE-16. The entry point is scanned before it is constructed: RegisterContributions
            // runs inside the constructor's reach, so scanning afterwards would already have let
            // plugin code execute. The module entry point keeps its parameterless constructor
            // (see docs/plugin-contract.md) — this scan is the compensating control.
            if (PluginIsolationScanner.TryFindViolation(entryPointType, out var entryPointViolation))
            {
                error = $"Plugin '{pluginKey}' wurde abgelehnt: Der Entry Point '{entryPoint}' greift direkt "
                    + $"auf den Core-Namensraum zu ('{entryPointViolation}'). Plugins duerfen nur ueber "
                    + "ClubGear.Plugin.Contracts mit dem Host sprechen.";
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

    /// <summary>
    /// CORE-16. Resolves every declared contribution type from the module's own assembly and runs
    /// the isolation scan over it. Returns the rejection message, or <c>null</c> when all declared
    /// types are present and clean.
    /// </summary>
    private static string? FindContributionFailure(
        string pluginKey,
        IPluginModule module,
        PluginContributionCollector collector)
    {
        var moduleAssembly = module.GetType().Assembly;

        foreach (var (kind, typeName) in EnumerateDeclaredContributionTypes(collector))
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return $"Plugin '{pluginKey}' deklariert {kind} ohne Typnamen.";
            }

            // Deliberately the module's own assembly only: a contribution type that resolves from
            // somewhere else would leave the plugin boundary before it is even registered.
            var contributionType = moduleAssembly.GetType(typeName, throwOnError: false, ignoreCase: false);
            if (contributionType is null)
            {
                return $"Plugin '{pluginKey}' deklariert {kind} '{typeName}', der im Plugin-Assembly "
                    + $"'{moduleAssembly.GetName().Name}' nicht gefunden wurde.";
            }

            if (!contributionType.IsVisible || contributionType.IsAbstract)
            {
                return $"Plugin '{pluginKey}' deklariert {kind} '{contributionType.FullName}', der nicht "
                    + "oeffentlich und instanziierbar ist. Beitragstypen muessen public und nicht abstract sein.";
            }

            // CORE-18. Exactly one canonical public constructor T(IPluginHostContext). No
            // parameterless fallback, no other arity. This is the shape whose removal moved
            // ContractVersion.MinimumSupported to 1.13.0.
            if (PluginContributionActivator.FindCanonicalConstructor(contributionType) is null)
            {
                return $"Plugin '{pluginKey}' wurde abgelehnt: {kind} '{contributionType.FullName}' hat keinen "
                    + "oeffentlichen Konstruktor '(IPluginHostContext)'. Seit Contract 1.13.0 werden Beitraege "
                    + "vom Host ueber genau diesen Konstruktor erzeugt.";
            }

            if (PluginIsolationScanner.TryFindViolation(contributionType, out var violation))
            {
                return $"Plugin '{pluginKey}' wurde abgelehnt: {kind} '{contributionType.FullName}' greift "
                    + $"direkt auf den Core-Namensraum zu ('{violation}'). Plugins duerfen nur ueber "
                    + "ClubGear.Plugin.Contracts mit dem Host sprechen.";
            }
        }

        return null;
    }

    private static IEnumerable<(string Kind, string TypeName)> EnumerateDeclaredContributionTypes(
        PluginContributionCollector collector)
    {
        foreach (var contribution in collector.Services)
        {
            yield return ("den Service-Typ", contribution.ServiceType);
        }

        foreach (var contribution in collector.MemberProviders)
        {
            yield return ("den Member-Provider-Typ", contribution.ProviderType);
        }

        foreach (var contribution in collector.BackgroundJobs)
        {
            yield return ("den Hintergrundjob-Typ", contribution.JobType);
        }

        foreach (var contribution in collector.AuditSinks)
        {
            yield return ("den Audit-Sink-Typ", contribution.ProviderType);
        }

        foreach (var contribution in collector.IdentityProviders)
        {
            yield return ("den Identity-Provider-Typ", contribution.ProviderType);
        }

        foreach (var contribution in collector.SelfServiceProfileProviders)
        {
            yield return ("den Self-Service-Provider-Typ", contribution.ProviderType);
        }
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