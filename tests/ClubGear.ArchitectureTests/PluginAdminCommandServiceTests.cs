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

public sealed class PluginAdminCommandServiceTests
{
    [Fact]
    public async Task GetPanelsAsync_ReturnsAuthorizedPanelsAndCommands()
    {
        var registry = new PluginRegistry();
        var module = new Plugins.RuntimeAdminPluginModule();
        RegisterRuntime(registry, module);

        var service = new PluginAdminCommandService(
            registry,
            CreateRuntimeAdapter(),
            NullLogger<PluginAdminCommandService>.Instance);

        var result = await service.GetPanelsAsync(BuildUser(PermissionKeys.MembersManage));

        var modulePanels = Assert.Single(result);
        Assert.Equal(module.Manifest.ModuleId, modulePanels.ModuleId);

        var panel = Assert.Single(modulePanels.Panels);
        Assert.Equal("vehicle-fields", panel.Key);
        Assert.Single(panel.Commands!);
    }

    [Fact]
    public async Task GetPanelsAsync_HidesPanels_WhenUserLacksPanelPermission()
    {
        var registry = new PluginRegistry();
        var module = new Plugins.RuntimeAdminPluginModule();
        RegisterRuntime(registry, module);

        var service = new PluginAdminCommandService(
            registry,
            CreateRuntimeAdapter(),
            NullLogger<PluginAdminCommandService>.Instance);

        var result = await service.GetPanelsAsync(BuildUser());

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ReturnsSuccess_WhenUserHasCommandPermission()
    {
        var registry = new PluginRegistry();
        var module = new Plugins.RuntimeAdminPluginModule();
        RegisterRuntime(registry, module);

        var service = new PluginAdminCommandService(
            registry,
            CreateRuntimeAdapter(),
            NullLogger<PluginAdminCommandService>.Instance);

        var result = await service.ExecuteCommandAsync(
            module.Manifest.ModuleId,
            new PluginAdminCommandRequest("vehicle-fields", "reindex"),
            BuildUser(PermissionKeys.MembersManage));

        Assert.True(result.Success);
        Assert.Equal("executed", result.Status);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ReturnsForbidden_WhenUserLacksCommandPermission()
    {
        var registry = new PluginRegistry();
        var module = new Plugins.RuntimeAdminPluginModule();
        RegisterRuntime(registry, module);

        var service = new PluginAdminCommandService(
            registry,
            CreateRuntimeAdapter(),
            NullLogger<PluginAdminCommandService>.Instance);

        var result = await service.ExecuteCommandAsync(
            module.Manifest.ModuleId,
            new PluginAdminCommandRequest("vehicle-fields", "reindex"),
            BuildUser());

        Assert.False(result.Success);
        Assert.Equal("forbidden", result.Status);
    }

    private static PluginRuntimeAdapter CreateRuntimeAdapter()
    {
        return new PluginRuntimeAdapter(
            new ClaimsBackedPermissionFacade(),
            new NoOpAuditFacade(),
            new NoOpNotificationFacade(),
            new FakeMemberFeatureService(),
            NullLogger<PluginRuntimeAdapter>.Instance);
    }

    private static ClaimsPrincipal BuildUser(params string[] permissions)
    {
        var claims = permissions.Select(permission => new Claim("permission", permission)).ToList();
        claims.Add(new Claim(ClaimTypes.Name, "plugin-admin-command-tester"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static void RegisterRuntime(PluginRegistry registry, IPluginModule module)
    {
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
                && user.Claims.Any(claim => claim.Type == "permission" && string.Equals(claim.Value, permissionKey, StringComparison.OrdinalIgnoreCase)));
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
        public Task<NotificationResult> NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default)
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
