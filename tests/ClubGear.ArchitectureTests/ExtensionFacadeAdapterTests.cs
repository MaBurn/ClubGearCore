using System.Security.Claims;
using System.Text.Json;
using ClubGear.Models;
using ClubGear.Services;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Core;
using ClubGear.Services.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class ExtensionFacadeAdapterTests
{
    [Fact]
    public async Task ExtensionPermissionFacade_PassesThrough_ToPermissionService()
    {
        var expected = true;
        ClaimsPrincipal? capturedUser = null;
        string? capturedPermissionKey = null;

        var permissionService = new FakePermissionService
        {
            OnHasPermissionAsync = (user, permissionKey, _) =>
            {
                capturedUser = user;
                capturedPermissionKey = permissionKey;
                return Task.FromResult(expected);
            }
        };

        var sut = new ExtensionPermissionFacade(permissionService, NullLogger<ExtensionPermissionFacade>.Instance);
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        var actual = await sut.HasPermissionAsync(user, "members.read", ["members.read"]);

        Assert.True(actual);
        Assert.Same(user, capturedUser);
        Assert.Equal("members.read", capturedPermissionKey);
    }

    [Fact]
    public async Task ExtensionPermissionFacade_RejectsUndeclaredManifestPermission_WithoutCallingPermissionService()
    {
        var wasCalled = false;
        var permissionService = new FakePermissionService
        {
            OnHasPermissionAsync = (_, _, _) =>
            {
                wasCalled = true;
                return Task.FromResult(true);
            }
        };

        var sut = new ExtensionPermissionFacade(permissionService, NullLogger<ExtensionPermissionFacade>.Instance);

        var actual = await sut.HasPermissionAsync(new ClaimsPrincipal(new ClaimsIdentity()), "members.manage", ["members.read"]);

        Assert.False(actual);
        Assert.False(wasCalled);
    }

    [Theory]
    [InlineData(PermissionKeys.MembersManage)]
    [InlineData(PermissionKeys.SelfServiceProfileEdit)]
    public async Task ExtensionPermissionFacade_GrantsDerivedMemberWritePermission(string sourcePermission)
    {
        var permissionService = new FakePermissionService
        {
            OnHasPermissionAsync = (_, permissionKey, _) =>
                Task.FromResult(string.Equals(permissionKey, sourcePermission, StringComparison.OrdinalIgnoreCase))
        };

        var sut = new ExtensionPermissionFacade(permissionService, NullLogger<ExtensionPermissionFacade>.Instance);

        var actual = await sut.HasPermissionAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            "clubgear.plugin.carinfo.member.write",
            ["clubgear.plugin.carinfo.member.write"]);

        Assert.True(actual);
    }

    [Fact]
    public async Task ExtensionPermissionFacade_TranslatesUnexpectedErrors_ToUserFriendlyException()
    {
        var permissionService = new FakePermissionService
        {
            OnHasPermissionAsync = (_, _, _) => throw new InvalidOperationException("permission-boom")
        };

        var sut = new ExtensionPermissionFacade(permissionService, NullLogger<ExtensionPermissionFacade>.Instance);

        var ex = await Assert.ThrowsAsync<UserFriendlyException>(() => sut.HasPermissionAsync(new ClaimsPrincipal(), "members.read", ["members.read"]));

        Assert.Equal("Die Berechtigungspruefung der Erweiterung konnte nicht ausgefuehrt werden.", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void CorePermissionDefinitionProvider_IncludesDeclaredPluginPermissions_FromStatusStore()
    {
        var store = new FakePluginStatusStore
        {
            Records =
            [
                new PluginStatusRecord
                {
                    Key = "plugin.members.analytics",
                    DisplayName = "Members Analytics",
                    Version = "1.0.0",
                    Author = "Plugin Author",
                    License = "Proprietary",
                    EntryPoint = "Plugin.EntryPoint",
                    RequiredCoreVersion = ">=1.0.0",
                    InstallSource = "zip",
                    PackageHash = "ABC123",
                    PackagePath = "/tmp/plugin.zip",
                    PermissionsJson = JsonSerializer.Serialize(new[] { "Plugin_Test_View" }),
                    ExtensionPointsJson = "[]"
                }
            ]
        };

        var sut = new CorePermissionDefinitionProvider(store);

        var definitions = sut.GetPermissions().ToArray();

        Assert.Contains(definitions, definition => definition.Key == "Plugin_Test_View" && definition.Category == "General");
        Assert.Contains(definitions, definition => definition.Key == PermissionKeys.MembersRead);
    }

    [Fact]
    public async Task ExtensionAuditFacade_PassesThrough_ToAuditService()
    {
        var capturedRecord = default(AuditLogRecord);
        var auditService = new FakeAuditLogService
        {
            OnLogAsync = (record, _) =>
            {
                capturedRecord = record;
                return Task.CompletedTask;
            }
        };

        var sut = new ExtensionAuditFacade(auditService, NullLogger<ExtensionAuditFacade>.Instance);
        var record = new AuditLogRecord(Action: "member.updated", Actor: "plugin");

        await sut.LogAsync(record);

        Assert.Equal(record, capturedRecord);
    }

    [Fact]
    public async Task ExtensionAuditFacade_TranslatesUnexpectedErrors_ToUserFriendlyException()
    {
        var auditService = new FakeAuditLogService
        {
            OnLogAsync = (_, _) => throw new InvalidOperationException("audit-boom")
        };

        var sut = new ExtensionAuditFacade(auditService, NullLogger<ExtensionAuditFacade>.Instance);
        var record = new AuditLogRecord(Action: "member.updated", Actor: "plugin");

        var ex = await Assert.ThrowsAsync<UserFriendlyException>(() => sut.LogAsync(record));

        Assert.Equal("Das Audit fuer die Erweiterung konnte nicht geschrieben werden.", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public async Task ExtensionNotificationFacade_PassesThrough_ToNotificationService()
    {
        var expectedResult = new NotificationResult(true, "InApp", "user-1");
        var capturedMessage = default(NotificationMessage);

        var notificationService = new FakeNotificationService
        {
            OnNotifyAsync = (message, _) =>
            {
                capturedMessage = message;
                return Task.FromResult(expectedResult);
            }
        };

        var sut = new ExtensionNotificationFacade(notificationService, NullLogger<ExtensionNotificationFacade>.Instance);
        var message = new NotificationMessage("user-1", "Subject", "Body", "InApp");

        var actual = await sut.NotifyAsync(message);

        Assert.Equal(expectedResult, actual);
        Assert.Equal(message, capturedMessage);
    }

    [Fact]
    public async Task ExtensionNotificationFacade_TranslatesUnexpectedErrors_ToUserFriendlyException()
    {
        var notificationService = new FakeNotificationService
        {
            OnNotifyAsync = (_, _) => throw new InvalidOperationException("notification-boom")
        };

        var sut = new ExtensionNotificationFacade(notificationService, NullLogger<ExtensionNotificationFacade>.Instance);
        var message = new NotificationMessage("user-1", "Subject", "Body", "InApp");

        var ex = await Assert.ThrowsAsync<UserFriendlyException>(() => sut.NotifyAsync(message));

        Assert.Equal("Die Benachrichtigung der Erweiterung konnte nicht versendet werden.", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void AddClubGearCoreServices_RegistersAndResolves_ExtensionFacades()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddClubGearCoreServices();

        services.AddSingleton<IPermissionService>(new FakePermissionService());
        services.AddSingleton<IAuditLogService>(new FakeAuditLogService());
        services.AddSingleton<INotificationService>(new FakeNotificationService());

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IExtensionPermissionFacade>());
        Assert.NotNull(provider.GetService<IExtensionAuditFacade>());
        Assert.NotNull(provider.GetService<IExtensionNotificationFacade>());
    }

    private sealed class FakePermissionService : IPermissionService
    {
        public Func<ClaimsPrincipal, string, CancellationToken, Task<bool>> OnHasPermissionAsync { get; set; } = (_, _, _) => Task.FromResult(false);

        public Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string permissionKey,
            CancellationToken cancellationToken = default)
            => OnHasPermissionAsync(user, permissionKey, cancellationToken);
    }

    private sealed class FakePluginStatusStore : IPluginStatusStore
    {
        public IReadOnlyList<PluginStatusRecord> Records { get; set; } = Array.Empty<PluginStatusRecord>();

        public PluginStatusRecord? GetByKey(string key)
            => Records.SingleOrDefault(record => string.Equals(record.Key, key, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<PluginStatusRecord> List()
            => Records;

        public Task<PluginStatusRecord> UpsertAsync(PluginStatusRecord record, CancellationToken cancellationToken = default)
            => Task.FromResult(record);

        public Task DeleteAsync(string key, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeAuditLogService : IAuditLogService
    {
        public Func<AuditLogRecord, CancellationToken, Task> OnLogAsync { get; set; } = (_, _) => Task.CompletedTask;

        public Func<string, object?, object?, string?, string?, string?, string?, object?, CancellationToken, Task> OnLogChangeAsync { get; set; }
            = (_, _, _, _, _, _, _, _, _) => Task.CompletedTask;

        public Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
            => OnLogAsync(record, cancellationToken);

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
            => OnLogChangeAsync(action, before, after, actor, source, targetType, targetId, metadata, cancellationToken);
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public Func<NotificationMessage, CancellationToken, Task<NotificationResult>> OnNotifyAsync { get; set; }
            = (_, _) => Task.FromResult(new NotificationResult(true, "InApp", "system"));

        public Task<NotificationResult> NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default)
            => OnNotifyAsync(message, cancellationToken);
    }
}
