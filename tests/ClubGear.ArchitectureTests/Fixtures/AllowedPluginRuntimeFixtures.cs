using ClubGear.Plugin.Contracts;
using ClubGear.Services.Plugins.Runtime;
using System.Globalization;

namespace ClubGear.ArchitectureTests.Plugins;

public sealed class AllowedPluginCapability
{
    private readonly string _permissionKey;

    public AllowedPluginCapability(string permissionKey)
    {
        _permissionKey = permissionKey;
    }

    public bool WasAllowed { get; private set; }

    public async Task ExecuteAsync(IPluginRuntimeBridge runtime, CancellationToken cancellationToken)
    {
        WasAllowed = await runtime.HasPermissionAsync(_permissionKey, cancellationToken);
        if (!WasAllowed)
        {
            return;
        }

        await runtime.LogAsync("plugin.capability.executed", actor: "plugin", cancellationToken: cancellationToken);
        await runtime.NotifyAsync(
            new PluginRuntimeNotification("member-1", "Hello", "Plugin execution succeeded.", "InApp"),
            cancellationToken);
    }
}

public sealed class AllowedPluginEndpoint
{
    public Task<PluginEndpointResult> HandleAsync(IPluginRuntimeBridge runtime, CancellationToken cancellationToken)
        => Task.FromResult(new PluginEndpointResult(200, "ok"));
}

public sealed class HostContextCapability
{
    public PluginHostMetadata? Metadata { get; private set; }

    public IReadOnlyList<PluginMemberSummary> Members { get; private set; } = Array.Empty<PluginMemberSummary>();

    public PluginMemberDetail? Member { get; private set; }

    public async Task ExecuteAsync(IPluginRuntimeBridge runtime, CancellationToken cancellationToken)
    {
        Metadata = runtime.Host.Metadata.GetCurrent();
        Members = await runtime.Host.Members.GetListAsync(cancellationToken: cancellationToken);
        Member = await runtime.Host.Members.GetByIdAsync(1, cancellationToken);
    }
}

public sealed class RuntimeLoadedPluginModuleA : IPluginModule
{
    public RuntimeLoadedPluginModuleA()
    {
        Manifest = new PluginManifest(
            "plugin.runtime.a",
            "Runtime Plugin A",
            new Version(1, 0, 0),
            "Plugin Tests",
            "Proprietary",
            typeof(RuntimeLoadedPluginModuleA).FullName!,
            ">=1.0.0",
            ["members.read", "members.manage"],
            ["member.detail", "member.edit", "member.badge", "member.action"]);
    }

    public PluginManifest Manifest { get; }

    public void RegisterContributions(IPluginContributionSink sink)
    {
        sink.AddRoute(new PluginRouteContribution("/declared/runtime-a", "members.read"));
        sink.AddService(new PluginServiceContribution("members.service", typeof(RuntimeLoadedPluginModuleA).FullName!));
        sink.AddMemberProvider(new PluginMemberProviderContribution(PluginMemberSlotKind.DetailCard, typeof(RuntimeLoadedDetailCardProviderA).FullName!, 10));
        sink.AddMemberProvider(new PluginMemberProviderContribution(PluginMemberSlotKind.EditTab, typeof(RuntimeLoadedEditTabProviderA).FullName!, 20));
        sink.AddMemberProvider(new PluginMemberProviderContribution(PluginMemberSlotKind.StatusBadge, typeof(RuntimeLoadedStatusBadgeProviderA).FullName!, 0));
        sink.AddMemberProvider(new PluginMemberProviderContribution(PluginMemberSlotKind.Action, typeof(RuntimeLoadedActionProviderA).FullName!, 30));
        sink.AddBackgroundJob(new PluginBackgroundJobContribution("members.sync", typeof(RuntimeLoadedPluginModuleA).FullName!));
    }
}

public sealed class RuntimeLoadedPluginModuleB : IPluginModule
{
    public RuntimeLoadedPluginModuleB()
    {
        Manifest = new PluginManifest(
            "plugin.runtime.b",
            "Runtime Plugin B",
            new Version(1, 0, 0),
            "Plugin Tests",
            "Proprietary",
            typeof(RuntimeLoadedPluginModuleB).FullName!,
            ">=1.0.0",
            ["members.read", "members.manage"],
            ["member.detail", "member.edit", "member.badge", "member.action"]);
    }

    public PluginManifest Manifest { get; }

    public void RegisterContributions(IPluginContributionSink sink)
    {
        sink.AddRoute(new PluginRouteContribution("/declared/runtime-b", "members.read"));
        sink.AddService(new PluginServiceContribution("members.service.b", typeof(RuntimeLoadedPluginModuleB).FullName!));
        sink.AddMemberProvider(new PluginMemberProviderContribution(PluginMemberSlotKind.DetailCard, typeof(RuntimeLoadedDetailCardProviderB).FullName!, 20));
        sink.AddMemberProvider(new PluginMemberProviderContribution(PluginMemberSlotKind.EditTab, typeof(RuntimeLoadedEditTabProviderB).FullName!, 30));
        sink.AddMemberProvider(new PluginMemberProviderContribution(PluginMemberSlotKind.StatusBadge, typeof(RuntimeLoadedStatusBadgeProviderB).FullName!, 10));
        sink.AddMemberProvider(new PluginMemberProviderContribution(PluginMemberSlotKind.Action, typeof(RuntimeLoadedActionProviderB).FullName!, 40));
        sink.AddBackgroundJob(new PluginBackgroundJobContribution("members.sync.b", typeof(RuntimeLoadedPluginModuleB).FullName!));
    }
}

public sealed class RuntimeAdminPluginModule : IPluginModule
{
    public RuntimeAdminPluginModule()
    {
        Manifest = new PluginManifest(
            "plugin.runtime.admin",
            "Runtime Admin Plugin",
            new Version(1, 0, 0),
            "Plugin Tests",
            "Proprietary",
            typeof(RuntimeAdminPluginModule).FullName!,
            ">=1.0.0",
            ["members.read", "members.manage"],
            ["admin.functions"]);
    }

    public PluginManifest Manifest { get; }

    public void RegisterContributions(IPluginContributionSink sink)
    {
        sink.AddAdminPanelProvider(new PluginAdminPanelProviderContribution(typeof(RuntimeAdminPanelProvider).FullName!, 0));
    }
}

public sealed class RuntimeAdminPanelProvider : IAdminFunctionPanelProvider
{
    public Task<IReadOnlyList<PluginAdminPanel>> GetPanelsAsync(
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default)
    {
        var panel = new PluginAdminPanel(
            "vehicle-fields",
            "Feldverwaltung",
            "members.manage",
            Commands:
            [
                new PluginAdminCommandDescriptor(
                    "reindex",
                    "Neu indexieren",
                    "members.manage",
                    "outline-primary",
                    0,
                    "Soll der Index neu aufgebaut werden?")
            ]);

        return Task.FromResult<IReadOnlyList<PluginAdminPanel>>([panel]);
    }

    public Task<PluginCommandResult> ExecuteCommandAsync(
        PluginAdminCommandRequest request,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.CommandKey, "reindex", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new PluginCommandResult(false, "command-not-found", "Befehl nicht gefunden."));
        }

        return Task.FromResult(new PluginCommandResult(true, "executed", "Index wurde neu aufgebaut."));
    }
}

public sealed class RuntimeLoadedDetailCardProviderA : IMemberDetailCardProvider
{
    public Task<IReadOnlyList<MemberDetailCardSlot>> GetCardsAsync(
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MemberDetailCardSlot>>([new MemberDetailCardSlot("detail-a", "Plugin Karte A", $"Erweiterte Sicht fuer {member.FullName}", 5)]);
}

public sealed class RuntimeLoadedEditTabProviderA : IMemberEditTabProvider
{
    public Task<IReadOnlyList<MemberEditTabSlot>> GetTabsAsync(
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MemberEditTabSlot>>([new MemberEditTabSlot("edit-a", "Plugin Tab A", $"Bearbeitungsinhalt fuer {member.MemberNumber}", 5)]);
}

public sealed class RuntimeLoadedStatusBadgeProviderA : IMemberStatusBadgeProvider
{
    public Task<IReadOnlyList<MemberStatusBadgeSlot>> GetBadgesAsync(
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MemberStatusBadgeSlot>>([new MemberStatusBadgeSlot("badge-a", member.IsActive ? "Plugin Aktiv" : "Plugin Inaktiv", "success", 0)]);
}

public sealed class RuntimeLoadedActionProviderA : IMemberActionProvider
{
    public Task<IReadOnlyList<MemberActionSlot>> GetActionsAsync(
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MemberActionSlot>>([new MemberActionSlot("sync-a", "Synchronisieren A", "members.manage", "outline-primary", 5, "Plugin-Aktion jetzt ausfuehren?")]);

    public Task<PluginMemberActionResult> ExecuteAsync(
        PluginMemberActionRequest request,
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginMemberActionResult(true, "executed", $"Plugin A hat {member.FullName} verarbeitet."));
}

public sealed class RuntimeLoadedDetailCardProviderB : IMemberDetailCardProvider
{
    public Task<IReadOnlyList<MemberDetailCardSlot>> GetCardsAsync(
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MemberDetailCardSlot>>([new MemberDetailCardSlot("detail-b", "Plugin Karte B", $"Zusaetzliche Hinweise fuer {member.MemberNumber}", 10)]);
}

public sealed class RuntimeLoadedEditTabProviderB : IMemberEditTabProvider
{
    public Task<IReadOnlyList<MemberEditTabSlot>> GetTabsAsync(
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MemberEditTabSlot>>([new MemberEditTabSlot("edit-b", "Plugin Tab B", $"Ergaenzender Inhalt fuer {member.FullName}", 10)]);
}

public sealed class RuntimeLoadedStatusBadgeProviderB : IMemberStatusBadgeProvider
{
    public Task<IReadOnlyList<MemberStatusBadgeSlot>> GetBadgesAsync(
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MemberStatusBadgeSlot>>([new MemberStatusBadgeSlot("badge-b", "Plugin Zweitstatus", "info", 10)]);
}

public sealed class RuntimeLoadedActionProviderB : IMemberActionProvider
{
    public Task<IReadOnlyList<MemberActionSlot>> GetActionsAsync(
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MemberActionSlot>>([new MemberActionSlot("sync-b", "Synchronisieren B", "members.manage", "outline-secondary", 10)]);

    public Task<PluginMemberActionResult> ExecuteAsync(
        PluginMemberActionRequest request,
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginMemberActionResult(true, "executed", $"Plugin B hat {member.MemberNumber} verarbeitet."));
}

public sealed class MigratingPluginModule : IPluginModule
{
    public MigratingPluginModule()
    {
        Manifest = new PluginManifest(
            "plugin.runtime.migrating",
            "Runtime Plugin With Migration",
            new Version(1, 0, 0),
            "Plugin Tests",
            "Proprietary",
            typeof(MigratingPluginModule).FullName!,
            ">=1.0.0",
            ["members.read"],
            ["member.detail"]);
    }

    public PluginManifest Manifest { get; }

    public void RegisterContributions(IPluginContributionSink sink)
    {
        sink.AddMemberProvider(new PluginMemberProviderContribution(PluginMemberSlotKind.DetailCard, typeof(PersistedNotesDetailCardProvider).FullName!, 0));
    }

    public IReadOnlyList<IPluginMigration> GetMigrations()
        => [new CreateNotesTableMigration()];
}

public sealed class BadMigratingPluginModule : IPluginModule
{
    public BadMigratingPluginModule()
    {
        Manifest = new PluginManifest(
            "plugin.runtime.badmigration",
            "Runtime Plugin With Bad Migration",
            new Version(1, 0, 0),
            "Plugin Tests",
            "Proprietary",
            typeof(BadMigratingPluginModule).FullName!,
            ">=1.0.0",
            ["members.read"],
            ["member.detail"]);
    }

    public PluginManifest Manifest { get; }

    public IReadOnlyList<IPluginMigration> GetMigrations()
        => [new InvalidPrefixMigration()];
}

public sealed class CreateNotesTableMigration : IPluginMigration
{
    public string MigrationId => "001_create_notes";

    public async Task ApplyAsync(IPluginMigrationContext context, CancellationToken cancellationToken = default)
    {
        var tableName = context.GetTableName("notes");
        await context.ExecuteAsync(
            $"CREATE TABLE IF NOT EXISTS {tableName} (Id INTEGER NOT NULL PRIMARY KEY, MemberId INTEGER NOT NULL, Note TEXT NOT NULL);",
            cancellationToken: cancellationToken);
        await context.ExecuteAsync(
            $"INSERT INTO {tableName} (Id, MemberId, Note) VALUES (1, 1, @note);",
            new Dictionary<string, string?>
            {
                ["note"] = "Persistierte Plugin-Notiz"
            },
            cancellationToken);
    }
}

public sealed class InvalidPrefixMigration : IPluginMigration
{
    public string MigrationId => "001_create_wrong_table";

    public Task ApplyAsync(IPluginMigrationContext context, CancellationToken cancellationToken = default)
        => context.ExecuteAsync(
            "CREATE TABLE IF NOT EXISTS Members (Id INTEGER NOT NULL PRIMARY KEY, Note TEXT NOT NULL);",
            cancellationToken: cancellationToken);
}

internal sealed class RuntimeLoadedBackgroundJobA : IPluginBackgroundJob
{
    public Task ExecuteAsync(IPluginHostContext hostContext, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class FixtureSelfServiceProviderModule : IPluginModule
{
    public FixtureSelfServiceProviderModule()
    {
        Manifest = new PluginManifest(
            "plugin.fixture.selfservice",
            "Fixture SelfService Module",
            new Version(1, 0, 0),
            "Plugin Tests",
            "Proprietary",
            typeof(FixtureSelfServiceProviderModule).FullName!,
            ">=1.0.0",
            ["members.read"],
            ["member.selfservice"]);
    }

    public PluginManifest Manifest { get; }

    public void RegisterContributions(IPluginContributionSink sink)
    {
        sink.AddSelfServiceProfileSection(new PluginSelfServiceProfileProviderContribution(typeof(FixtureSelfServiceProvider).FullName!, 0));
    }
}

public sealed class FixtureSelfServiceProvider : ISelfServiceProfileSectionProvider
{
    public Task<SelfServiceProfileSection?> GetSectionAsync(
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default)
        => Task.FromResult<SelfServiceProfileSection?>(null);

    public Task<PluginMemberActionResult> ExecuteSelfServiceActionAsync(
        PluginMemberActionRequest request,
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginMemberActionResult(false, "noop", ""));
}

public sealed class PersistedNotesDetailCardProvider : IMemberDetailCardProvider
{
    public async Task<IReadOnlyList<MemberDetailCardSlot>> GetCardsAsync(
        PluginMemberDetail member,
        IPluginHostContext hostContext,
        CancellationToken cancellationToken = default)
    {
        var tableName = hostContext.Persistence.GetTableName("notes");
        var rows = await hostContext.Persistence.QueryAsync(
            $"SELECT Note FROM {tableName} WHERE MemberId = @memberId;",
            new Dictionary<string, string?>
            {
                ["memberId"] = member.Id.ToString(CultureInfo.InvariantCulture)
            },
            cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<MemberDetailCardSlot>();
        }

        return [new MemberDetailCardSlot("persisted-note", "Plugin-Notiz", rows[0].Values["Note"] ?? string.Empty)];
    }
}
