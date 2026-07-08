namespace ClubGear.Plugin.Contracts;

public interface IPluginContributionSink
{
    void AddRoute(PluginRouteContribution contribution);

    void AddService(PluginServiceContribution contribution);

    void AddMemberProvider(PluginMemberProviderContribution contribution);

    void AddBackgroundJob(PluginBackgroundJobContribution contribution);

    void AddAdminPanelProvider(PluginAdminPanelProviderContribution contribution)
    {
        ArgumentNullException.ThrowIfNull(contribution);
    }

    void AddNavEntries(IReadOnlyList<PluginNavEntry> entries) { }

    void AddPageProvider(PluginPageProviderContribution contribution) { }

    void AddAuditSink(PluginAuditSinkContribution contribution) { }

    void AddIdentityProvider(PluginIdentityProviderContribution contribution) { }

    void AddSelfServiceProfileSection(PluginSelfServiceProfileProviderContribution contribution) { }
}

public sealed record PluginRouteContribution(string RoutePattern, string PermissionKey);

public sealed record PluginServiceContribution(string Key, string ServiceType);

public enum PluginMemberSlotKind
{
    DetailCard,
    EditTab,
    StatusBadge,
    Action
}

public sealed record PluginMemberProviderContribution(
    PluginMemberSlotKind SlotKind,
    string ProviderType,
    int Order = 0);

public sealed record PluginBackgroundJobContribution(string Key, string JobType);

public sealed record PluginAdminPanelProviderContribution(
    string ProviderType,
    int Order = 0);

public sealed record PluginPageProviderContribution(string ProviderType, int Order = 0);

public sealed record PluginAuditSinkContribution(string ProviderType, int Order = 0);

public sealed record PluginIdentityProviderContribution(string ProviderKey, string ProviderType);

public sealed record PluginSelfServiceProfileProviderContribution(string ProviderType, int Order = 0);