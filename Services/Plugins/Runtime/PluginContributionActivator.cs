using System.Reflection;
using System.Security.Claims;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace ClubGear.Services.Plugins.Runtime;

/// <summary>CORE-18. See <see cref="IPluginContributionActivator"/>.</summary>
public sealed class PluginContributionActivator : IPluginContributionActivator
{
    private static readonly ClaimsPrincipal SystemPrincipal = new(
        new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "system.plugin-contribution-activator")],
            "System"));

    private readonly IPluginRegistryReader _registryReader;
    private readonly IPluginActivityTracker _activityTracker;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PluginContributionActivator> _logger;

    public PluginContributionActivator(
        IPluginRegistryReader registryReader,
        IPluginActivityTracker activityTracker,
        IServiceScopeFactory scopeFactory,
        ILogger<PluginContributionActivator> logger)
    {
        _registryReader = registryReader;
        _activityTracker = activityTracker;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public PluginContributionLease<T> Rent<T>(string moduleId, string contributionType, ClaimsPrincipal? user = null)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contributionType);

        var module = _registryReader.GetModule(moduleId);
        if (_registryReader.GetByModuleId(moduleId) is null || module is null)
        {
            return PluginContributionLease<T>.Empty(
                moduleId, contributionType, $"Plugin '{moduleId}' ist nicht aktiv.");
        }

        // Taken before anything is constructed, so deactivation cannot unload the module's
        // AssemblyLoadContext while this lease is alive (CORE-17).
        var activityToken = _activityTracker.TryAcquire(moduleId);
        if (activityToken is null)
        {
            return PluginContributionLease<T>.Empty(
                moduleId, contributionType, $"Plugin '{moduleId}' wird gerade deaktiviert.");
        }

        var contributionClrType = _registryReader.ResolveContributionType(moduleId, contributionType);
        if (contributionClrType is null || !typeof(T).IsAssignableFrom(contributionClrType))
        {
            activityToken.Dispose();
            return PluginContributionLease<T>.Empty(
                moduleId,
                contributionType,
                $"Typ '{contributionType}' aus Plugin '{moduleId}' wurde nicht gefunden oder "
                + $"implementiert '{typeof(T).Name}' nicht.");
        }

        // The lease deliberately does not re-run the isolation scan: that is a load-time gate
        // (design 1.2) over a property which cannot change after the module was loaded.
        IServiceScope? scope = null;
        try
        {
            scope = _scopeFactory.CreateAsyncScope();
            var adapter = scope.ServiceProvider.GetRequiredService<IPluginRuntimeAdapter>();
            var host = adapter.CreateBridge(module, user ?? SystemPrincipal).Host;

            var constructor = FindCanonicalConstructor(contributionClrType);
            if (constructor is null)
            {
                DisposeQuietly(scope, activityToken);
                return PluginContributionLease<T>.Empty(
                    moduleId,
                    contributionType,
                    $"Typ '{contributionType}' aus Plugin '{moduleId}' hat keinen oeffentlichen "
                    + "Konstruktor '(IPluginHostContext)'.");
            }

            var instance = (T)constructor.Invoke([host]);
            return PluginContributionLease<T>.Rented(moduleId, contributionType, instance, scope, activityToken);
        }
        catch (Exception ex)
        {
            var reason = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;

            _logger.LogError(
                reason,
                "Beitrag '{ContributionType}' aus Plugin '{ModuleId}' konnte nicht erzeugt werden.",
                contributionType,
                moduleId);

            DisposeQuietly(scope, activityToken);
            return PluginContributionLease<T>.Empty(moduleId, contributionType, reason.Message);
        }
    }

    /// <summary>
    /// CORE-18. The single canonical shape: one public constructor taking exactly one
    /// <see cref="IPluginHostContext"/>. <c>PluginLoader</c> rejects everything else at load time;
    /// this lookup is the runtime half of the same rule.
    /// </summary>
    internal static ConstructorInfo? FindCanonicalConstructor(Type contributionType)
        => contributionType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(candidate =>
            {
                var parameters = candidate.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(IPluginHostContext);
            });

    private static void DisposeQuietly(IServiceScope? scope, IDisposable activityToken)
    {
        try
        {
            scope?.Dispose();
        }
        finally
        {
            activityToken.Dispose();
        }
    }
}
