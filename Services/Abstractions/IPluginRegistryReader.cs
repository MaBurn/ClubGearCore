using ClubGear.Plugin.Contracts;

namespace ClubGear.Services.Abstractions;

public interface IPluginRegistryReader
{
    IReadOnlyList<RegisteredPluginRuntime> GetRegisteredPlugins();

    RegisteredPluginRuntime? GetByModuleId(string moduleId);

    IPluginModule? GetModule(string moduleId);

    TProvider? CreateMemberProvider<TProvider>(string moduleId, string providerType)
        where TProvider : class;
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