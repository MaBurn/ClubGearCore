using System.Runtime.Loader;
using System.Security.Claims;
using ClubGear.Models;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using ClubGear.Services.Core;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginPageServiceTests
{
    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private static PluginPageService CreateService(PluginRegistry registry)
        => new PluginPageService(
            registry,
            CreateRuntimeAdapter(),
            NullLogger<PluginPageService>.Instance);

    private static PluginRuntimeAdapter CreateRuntimeAdapter()
        => new PluginRuntimeAdapter(
            new ClaimsBackedPermissionFacade(),
            new NoOpAuditFacade(),
            new NoOpNotificationFacade(),
            new FakeMemberFeatureService(),
            NullLogger<PluginRuntimeAdapter>.Instance);

    private static ClaimsPrincipal BuildUser(params string[] permissions)
    {
        var claims = permissions
            .Select(p => new Claim("permission", p))
            .ToList();
        claims.Add(new Claim(ClaimTypes.Name, "page-service-tester"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static PluginRegistry BuildRegistry(IPluginModule module)
    {
        var registry = new PluginRegistry();
        registry.Register(
            new RegisteredPluginRuntime(
                module.Manifest.ModuleId,
                module.Manifest.DisplayName,
                module.Manifest.PluginVersion,
                $"test:{module.Manifest.ModuleId}",
                Array.Empty<PluginRouteContribution>(),
                Array.Empty<PluginServiceContribution>(),
                Array.Empty<PluginMemberProviderContribution>(),
                Array.Empty<PluginBackgroundJobContribution>(),
                Array.Empty<PluginNavEntry>(),
                Array.Empty<PluginAuditSinkContribution>(),
                Array.Empty<PluginIdentityProviderContribution>(),
                Array.Empty<PluginSelfServiceProfileProviderContribution>()),
            module,
            AssemblyLoadContext.GetLoadContext(module.GetType().Assembly)!);
        return registry;
    }

    // ----------------------------------------------------------------
    // Tests
    // ----------------------------------------------------------------

    [Fact]
    public async Task GetPageDefinitionAsync_ReturnsNotFound_WhenModuleIdUnknown()
    {
        var registry = new PluginRegistry(); // empty
        var service = CreateService(registry);

        var result = await service.GetPageDefinitionAsync(
            "unknown.module",
            "some.page",
            BuildUser("page.read"));

        Assert.True(result.IsNotFound);
        Assert.False(result.IsSuccess);
        Assert.False(result.IsForbidden);
    }

    [Fact]
    public async Task GetPageDefinitionAsync_ReturnsForbidden_WhenUserLacksListPermission()
    {
        var module = new PagePluginModule();
        var registry = BuildRegistry(module);
        var service = CreateService(registry);

        var result = await service.GetPageDefinitionAsync(
            module.Manifest.ModuleId,
            "test.page",
            BuildUser()); // no permissions

        Assert.True(result.IsForbidden);
        Assert.False(result.IsSuccess);
        Assert.False(result.IsNotFound);
    }

    [Fact]
    public async Task GetPageDefinitionAsync_ReturnsSuccess_WhenUserHasListPermission()
    {
        var module = new PagePluginModule();
        var registry = BuildRegistry(module);
        var service = CreateService(registry);

        var result = await service.GetPageDefinitionAsync(
            module.Manifest.ModuleId,
            "test.page",
            BuildUser(PagePluginModule.ListPermission));

        Assert.True(result.IsSuccess);
        Assert.False(result.IsForbidden);
        Assert.False(result.IsNotFound);
        Assert.NotNull(result.Value);
        Assert.Equal("test.page", result.Value!.PageKey);
        Assert.Equal("Test Page", result.Value.Title);
    }

    [Fact]
    public async Task GetRowsAsync_ReturnsRows_WhenUserAuthorized()
    {
        var module = new PagePluginModule();
        var registry = BuildRegistry(module);
        var service = CreateService(registry);

        var result = await service.GetRowsAsync(
            module.Manifest.ModuleId,
            "test.page",
            BuildUser(PagePluginModule.ListPermission),
            filterValue: null,
            entityKey: null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("1", result.Value[0].GetValueOrDefault("Id"));
        Assert.Equal("Alpha", result.Value[0].GetValueOrDefault("Name"));
    }

    [Fact]
    public async Task GetRowsAsync_ReturnsForbidden_WhenUserLacksPermission()
    {
        var module = new PagePluginModule();
        var registry = BuildRegistry(module);
        var service = CreateService(registry);

        var result = await service.GetRowsAsync(
            module.Manifest.ModuleId,
            "test.page",
            BuildUser(), // no permissions
            filterValue: null,
            entityKey: null);

        Assert.True(result.IsForbidden);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ReturnsSuccess_WhenUserHasCommandPermission()
    {
        var module = new PagePluginModule();
        var registry = BuildRegistry(module);
        var service = CreateService(registry);

        var result = await service.ExecuteCommandAsync(
            module.Manifest.ModuleId,
            "test.page",
            "create",
            entityKey: null,
            new Dictionary<string, string> { ["Name"] = "New Item" },
            BuildUser(PagePluginModule.ListPermission, PagePluginModule.ManagePermission));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value!.Success);
        Assert.Equal("created", result.Value.Status);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ReturnsForbidden_WhenUserLacksCommandPermission()
    {
        var module = new PagePluginModule();
        var registry = BuildRegistry(module);
        var service = CreateService(registry);

        var result = await service.ExecuteCommandAsync(
            module.Manifest.ModuleId,
            "test.page",
            "create",
            entityKey: null,
            new Dictionary<string, string> { ["Name"] = "New Item" },
            BuildUser(PagePluginModule.ListPermission)); // has list but NOT manage

        Assert.True(result.IsForbidden);
        Assert.False(result.IsSuccess);
    }

    // ----------------------------------------------------------------
    // Inner stubs
    // ----------------------------------------------------------------

    private sealed class PagePluginModule : IPluginModule
    {
        public const string ListPermission = "test.page.read";
        public const string ManagePermission = "test.page.manage";

        public PagePluginModule()
        {
            Manifest = new PluginManifest(
                "plugin.page.test",
                "Page Test Plugin",
                new Version(1, 0, 0),
                "Plugin Tests",
                "Proprietary",
                typeof(PagePluginModule).FullName!,
                ">=1.0.0",
                [ListPermission, ManagePermission],
                ["page.generic"]);
        }

        public PluginManifest Manifest { get; }

        public void RegisterContributions(IPluginContributionSink sink)
        {
            sink.AddPageProvider(new PluginPageProviderContribution(typeof(TestPageProvider).FullName!, 0));
        }
    }

    private sealed class TestPageProvider : IPluginPageProvider
    {
        private static readonly IReadOnlyList<PluginPageColumn> Columns =
        [
            new PluginPageColumn("Id", "ID"),
            new PluginPageColumn("Name", "Name")
        ];

        private static readonly IReadOnlyList<PluginPageCommand> Commands =
        [
            new PluginPageCommand("create", "Erstellen", "bi-plus", PagePluginModule.ManagePermission,
                [new PluginFieldSchema("Name", "Name", Required: true)], false),
        ];

        public Task<PluginPageDefinition> GetPageDefinitionAsync(
            IPluginHostContext context,
            CancellationToken ct = default)
        {
            var def = new PluginPageDefinition(
                "test.page",
                "Test Page",
                "Id",
                Columns,
                Commands,
                PagePluginModule.ListPermission,
                "Filter...");
            return Task.FromResult(def);
        }

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string?>>> GetRowsAsync(
            IPluginHostContext context,
            string? filterValue,
            string? entityKey,
            CancellationToken ct = default)
        {
            IReadOnlyList<IReadOnlyDictionary<string, string?>> rows =
            [
                new Dictionary<string, string?> { ["Id"] = "1", ["Name"] = "Alpha" },
                new Dictionary<string, string?> { ["Id"] = "2", ["Name"] = "Beta" },
            ];
            return Task.FromResult(rows);
        }

        public Task<PluginCommandResult> ExecuteCommandAsync(
            IPluginHostContext context,
            string commandKey,
            string? entityKey,
            IReadOnlyDictionary<string, string> arguments,
            CancellationToken ct = default)
        {
            if (string.Equals(commandKey, "create", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new PluginCommandResult(true, "created", "Item erstellt."));
            }

            return Task.FromResult(new PluginCommandResult(false, "unknown-command", "Unbekannter Befehl."));
        }
    }

    private sealed class ClaimsBackedPermissionFacade : IExtensionPermissionFacade
    {
        public Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string permissionKey,
            IReadOnlyCollection<string> declaredPermissions,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                declaredPermissions.Contains(permissionKey, StringComparer.OrdinalIgnoreCase)
                && user.Claims.Any(c =>
                    c.Type == "permission"
                    && string.Equals(c.Value, permissionKey, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class NoOpAuditFacade : IExtensionAuditFacade
    {
        public Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task LogChangeAsync(
            string action,
            object? before,
            object? after,
            string? actor = null,
            string? source = null,
            string? targetType = null,
            string? targetId = null,
            object? metadata = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpNotificationFacade : IExtensionNotificationFacade
    {
        public Task<NotificationResult> NotifyAsync(
            NotificationMessage message,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new NotificationResult(true, message.Channel, message.Recipient));
    }

    private sealed class FakeMemberFeatureService : IMemberFeatureService
    {
        public Task<IReadOnlyList<Member>> GetListAsync(string? search = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Member>>(Array.Empty<Member>());

        public Task<IReadOnlyList<Member>> GetInactiveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Member>>(Array.Empty<Member>());

        public Task<Member?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult<Member?>(null);

        public Task CreateAsync(Member member, string? actor, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemberMutationStatus> UpdateAsync(Member member, string? actor, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemberMutationStatus> VerifyAsync(int id, string? actor, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemberMutationStatus> DeleteAsync(int id, string? actor, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> BulkDeleteAsync(IReadOnlyCollection<int> ids, string? actor, bool hasManagePermission, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MembersImportResult> ImportCsvAsync(Stream csvStream, string? actor, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
