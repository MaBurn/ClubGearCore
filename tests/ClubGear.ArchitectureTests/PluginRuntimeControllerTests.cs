using System.Runtime.Loader;
using System.Security.Claims;
using ClubGear.Controllers.Api;
using ClubGear.Plugin.Contracts;
using ClubGear.Services;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginRuntimeControllerTests
{
    [Fact]
    public async Task InvokeRoute_ReturnsPluginResponse_WhenRouteIsRegistered()
    {
        var registry = new PluginRegistry();
        var adapter = new FakeRuntimeAdapter(allowPermission: true);
        var registrar = new PluginEndpointRegistrar(adapter, registry);
        var module = new TestPluginModule("plugin.runtime.http");

        registry.Register(
            new RegisteredPluginRuntime(
                module.Manifest.ModuleId,
                module.Manifest.DisplayName,
                module.Manifest.PluginVersion,
                "plugin:test",
                Array.Empty<PluginRouteContribution>(),
                Array.Empty<PluginServiceContribution>(),
                Array.Empty<PluginMemberProviderContribution>(),
                Array.Empty<PluginBackgroundJobContribution>(),
                Array.Empty<PluginNavEntry>(),
                Array.Empty<PluginAuditSinkContribution>(),
                Array.Empty<PluginIdentityProviderContribution>(),
                Array.Empty<PluginSelfServiceProfileProviderContribution>()),
            module,
            new AssemblyLoadContext("plugin.runtime.http", isCollectible: true));

        registrar.RegisterGet(module, "/health", "members.read", (_, _) => Task.FromResult(new PluginEndpointResult(200, "ok", "text/plain")));

        var controller = CreateController(registry, registrar);

        var response = await controller.InvokeRoute("plugin.runtime.http", "health");

        var content = Assert.IsType<ContentResult>(response);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal("ok", content.Content);
        Assert.Equal("text/plain", content.ContentType);
    }

    [Fact]
    public async Task InvokeRoute_ReturnsNotFound_WhenPluginIsNotRegistered()
    {
        var controller = CreateController(new PluginRegistry(), new PluginEndpointRegistrar(new FakeRuntimeAdapter(allowPermission: true)));

        var response = await controller.InvokeRoute("missing.plugin", "health");

        var notFound = Assert.IsType<NotFoundObjectResult>(response);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task InvokeRoute_ReturnsForbidden_WhenPermissionIsDenied()
    {
        var registry = new PluginRegistry();
        var adapter = new FakeRuntimeAdapter(allowPermission: false);
        var registrar = new PluginEndpointRegistrar(adapter, registry);
        var module = new TestPluginModule("plugin.runtime.http");

        registry.Register(
            new RegisteredPluginRuntime(
                module.Manifest.ModuleId,
                module.Manifest.DisplayName,
                module.Manifest.PluginVersion,
                "plugin:test",
                Array.Empty<PluginRouteContribution>(),
                Array.Empty<PluginServiceContribution>(),
                Array.Empty<PluginMemberProviderContribution>(),
                Array.Empty<PluginBackgroundJobContribution>(),
                Array.Empty<PluginNavEntry>(),
                Array.Empty<PluginAuditSinkContribution>(),
                Array.Empty<PluginIdentityProviderContribution>(),
                Array.Empty<PluginSelfServiceProfileProviderContribution>()),
            module,
            new AssemblyLoadContext("plugin.runtime.http", isCollectible: true));

        registrar.RegisterGet(module, "/secure", "members.manage", (_, _) => Task.FromResult(new PluginEndpointResult(200, "ok", "text/plain")));

        var controller = CreateController(registry, registrar);

        var response = await controller.InvokeRoute("plugin.runtime.http", "secure");

        var content = Assert.IsType<ContentResult>(response);
        Assert.Equal(StatusCodes.Status403Forbidden, content.StatusCode);
        Assert.Equal("Forbidden", content.Content);
    }

    private static PluginRuntimeController CreateController(IPluginRegistryReader registry, PluginEndpointRegistrar registrar)
    {
        return new PluginRuntimeController(registry, registrar)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "tester")], "Test"))
                }
            }
        };
    }

    private sealed class TestPluginModule : IPluginModule
    {
        public TestPluginModule(string moduleId)
        {
            Manifest = new PluginManifest(
                moduleId,
                "Runtime HTTP Plugin",
                new Version(1, 0, 0),
                "Plugin Tests",
                "MIT",
                "Plugin.EntryPoint",
                ">=1.0.0",
                ["members.read", "members.manage"],
                ["runtime.route"]);
        }

        public PluginManifest Manifest { get; }
    }

    private sealed class FakeRuntimeAdapter : IPluginRuntimeAdapter
    {
        private readonly bool _allowPermission;

        public FakeRuntimeAdapter(bool allowPermission)
        {
            _allowPermission = allowPermission;
        }

        public IPluginRuntimeBridge CreateBridge(IPluginModule pluginModule, ClaimsPrincipal user)
            => new FakeBridge(pluginModule.Manifest.ModuleId);

        public Task RunAsync(
            IPluginModule pluginModule,
            ClaimsPrincipal user,
            Func<IPluginRuntimeBridge, CancellationToken, Task> capability,
            CancellationToken cancellationToken = default)
            => capability(CreateBridge(pluginModule, user), cancellationToken);

        public Task<TResult> InvokeAsync<TResult>(
            IPluginModule pluginModule,
            ClaimsPrincipal user,
            Func<IPluginRuntimeBridge, CancellationToken, Task<TResult>> capability,
            string? requiredPermissionKey = null,
            Delegate? isolatedDelegate = null,
            CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(requiredPermissionKey) && !_allowPermission)
            {
                throw new PluginPermissionDeniedException(pluginModule.Manifest.ModuleId, requiredPermissionKey);
            }

            return capability(CreateBridge(pluginModule, user), cancellationToken);
        }

        public void EnsureIsolated(Delegate pluginDelegate)
        {
        }
    }

    private sealed class FakeBridge : IPluginRuntimeBridge
    {
        public FakeBridge(string moduleId)
        {
            ModuleId = moduleId;
            Host = new FakeHostContext(moduleId);
        }

        public string ModuleId { get; }

        public IPluginHostContext Host { get; }

        public Task<bool> HasPermissionAsync(string permissionKey, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task LogAsync(
            string action,
            string? actor = null,
            string? targetType = null,
            string? targetId = null,
            object? metadata = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<PluginRuntimeNotificationResult> NotifyAsync(
            PluginRuntimeNotification notification,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginRuntimeNotificationResult(true, notification.Channel, notification.Recipient));
    }

    private sealed class FakeHostContext : IPluginHostContext
    {
        public FakeHostContext(string moduleId)
        {
            Metadata = new FakeMetadata(moduleId);
            Members = new FakeMemberReader();
            MemberActions = new FakeMemberActionFacade();
            Persistence = new FakeDataStore(moduleId);
        }

        public IPluginMetadataFacade Metadata { get; }

        public IPluginMemberReader Members { get; }

        public IPluginMemberActionFacade MemberActions { get; }

        public IPluginDataStore Persistence { get; }

        public IPluginPermissionFacade Permissions => throw new NotSupportedException("Not needed in tests.");
    }

    private sealed class FakeMetadata : IPluginMetadataFacade
    {
        private readonly string _moduleId;

        public FakeMetadata(string moduleId)
        {
            _moduleId = moduleId;
        }

        public PluginHostMetadata GetCurrent()
            => new(_moduleId, "Runtime HTTP Plugin", "MIT", ">=1.0.0", Array.Empty<string>(), Array.Empty<string>());
    }

    private sealed class FakeMemberReader : IPluginMemberReader
    {
        public Task<IReadOnlyList<PluginMemberSummary>> GetListAsync(string? search = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PluginMemberSummary>>(Array.Empty<PluginMemberSummary>());

        public Task<PluginMemberDetail?> GetByIdAsync(int memberId, CancellationToken cancellationToken = default)
            => Task.FromResult<PluginMemberDetail?>(null);
    }

    private sealed class FakeMemberActionFacade : IPluginMemberActionFacade
    {
        public Task<PluginMemberActionResult> ExecuteAsync(PluginMemberActionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new PluginMemberActionResult(false, "not-supported"));
    }

    private sealed class FakeDataStore : IPluginDataStore
    {
        public FakeDataStore(string moduleId)
        {
            ModuleId = moduleId;
        }

        public string ModuleId { get; }

        public string TablePrefix => "plg_test_";

        public string GetTableName(string localName)
            => TablePrefix + localName;

        public Task ExecuteAsync(string sql, IReadOnlyDictionary<string, string?>? parameters = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<PluginDataRow>> QueryAsync(string sql, IReadOnlyDictionary<string, string?>? parameters = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PluginDataRow>>(Array.Empty<PluginDataRow>());
    }
}
