using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Abstractions;

public interface IPluginRegistryReader
{
    IReadOnlyList<RegisteredPluginRuntime> GetRegisteredPlugins();

    RegisteredPluginRuntime? GetByModuleId(string moduleId);

    IPluginModule? GetModule(string moduleId);

    /// <summary>
    /// CORE-18. A pure lookup: resolves a declared contribution type from the module's own
    /// assembly without running any plugin code. Construction is
    /// <see cref="IPluginContributionActivator"/>'s job, so nothing executes under the registry's
    /// lock any more and no construction path can bypass the activity tracker.
    /// </summary>
    Type? ResolveContributionType(string moduleId, string contributionType);
}

public sealed record RegisteredPluginRuntime(
    string ModuleId,
    string DisplayName,
    Version PluginVersion,
    string LoadContextName,
    IReadOnlyList<PluginRouteContribution> Routes,
    IReadOnlyList<PluginServiceContribution> Services,
    IReadOnlyList<PluginMemberProviderContribution> MemberProviders,
    IReadOnlyList<PluginBackgroundJobContribution> BackgroundJobs,
    IReadOnlyList<PluginNavEntry> NavEntries,
    IReadOnlyList<PluginAuditSinkContribution> AuditSinks,
    IReadOnlyList<PluginIdentityProviderContribution> IdentityProviders,
    IReadOnlyList<PluginSelfServiceProfileProviderContribution> SelfServiceProfileProviders);