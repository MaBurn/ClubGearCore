using System.Reflection;
using System.Runtime.Loader;
using ClubGear.Controllers;
using ClubGear.Data;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Authorization;
using ClubGear.Services.Core;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>CORE-14: the administrator selects the provider of a single-active capability.</summary>
public sealed class PluginProviderSelectionTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Filename=:memory:");
    private readonly PluginRegistry _runtimeRegistry = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly ServiceProvider _services;

    public PluginProviderSelectionTests()
    {
        _connection.Open();
        var collection = new ServiceCollection();
        collection.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        collection.AddScoped<ISystemConfigService, SystemConfigService>();
        collection.AddSingleton<IAuditLogService>(_audit);
        _services = collection.BuildServiceProvider();

        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Default_IsTheCoreDefault_AndACollisionIsReported()
    {
        Register("alpha", PluginCapabilityKeys.StorageBlob);
        Register("beta", PluginCapabilityKeys.StorageBlob);
        var sut = CreateSut();

        Assert.Null(sut.GetSelectedModuleId(PluginCapabilityKeys.StorageBlob));
        var status = Assert.Single(sut.GetStatuses(), s => s.CapabilityKey == PluginCapabilityKeys.StorageBlob);
        Assert.Equal(new[] { "alpha", "beta" }, status.ClaimantModuleIds);
        Assert.True(status.HasUnresolvedCollision);
    }

    [Fact]
    public void Statuses_ListOnlySingleActiveCapabilities()
    {
        var keys = CreateSut().GetStatuses().Select(s => s.CapabilityKey).ToArray();

        Assert.Contains(PluginCapabilityKeys.StorageBlob, keys);
        Assert.DoesNotContain(PluginCapabilityKeys.NotificationChannel, keys);
    }

    [Fact]
    public async Task Select_PersistsAndAudits_AndSurvivesANewInstance()
    {
        Register("alpha", PluginCapabilityKeys.StorageBlob);

        var result = await CreateSut().SelectAsync(PluginCapabilityKeys.StorageBlob, "ALPHA", "admin");

        Assert.True(result.Success, result.Message);
        // A fresh instance has no cache, so this reads what was persisted.
        Assert.Equal("alpha", CreateSut().GetSelectedModuleId(PluginCapabilityKeys.StorageBlob));
        var entry = Assert.Single(_audit.Changes);
        Assert.Equal(("plugin.provider.selected", "admin", PluginCapabilityKeys.StorageBlob), entry);
    }

    [Fact]
    public async Task Select_Null_RestoresTheCoreDefault()
    {
        Register("alpha", PluginCapabilityKeys.StorageBlob);
        var sut = CreateSut();
        await sut.SelectAsync(PluginCapabilityKeys.StorageBlob, "alpha", "admin");

        var result = await sut.SelectAsync(PluginCapabilityKeys.StorageBlob, null, "admin");

        Assert.True(result.Success);
        Assert.Null(sut.GetSelectedModuleId(PluginCapabilityKeys.StorageBlob));
        Assert.Null(CreateSut().GetSelectedModuleId(PluginCapabilityKeys.StorageBlob));
    }

    [Theory]
    [InlineData(PluginCapabilityKeys.NotificationChannel, "alpha")] // fan-out: no selection
    [InlineData("storage.unknown", "alpha")]                         // not on the allowlist
    [InlineData(PluginCapabilityKeys.StorageBlob, "mallory")]         // does not claim the key
    [InlineData(PluginCapabilityKeys.DocumentStore, "alpha")]         // active, but claims another key
    public async Task Select_RejectsAnythingButAnActiveClaimant(string capabilityKey, string moduleId)
    {
        Register("alpha", PluginCapabilityKeys.StorageBlob, PluginCapabilityKeys.NotificationChannel);
        var sut = CreateSut();

        var result = await sut.SelectAsync(capabilityKey, moduleId, "admin");

        Assert.False(result.Success);
        Assert.Empty(_audit.Changes);
        Assert.Null(CreateSut().GetSelectedModuleId(PluginCapabilityKeys.StorageBlob));
    }

    [Fact]
    public async Task Status_FlagsASelectedPluginThatIsNoLongerActive()
    {
        Register("alpha", PluginCapabilityKeys.StorageBlob);
        await CreateSut().SelectAsync(PluginCapabilityKeys.StorageBlob, "alpha", "admin");
        await _runtimeRegistry.UnregisterAsync("alpha");

        var status = Assert.Single(CreateSut().GetStatuses(), s => s.CapabilityKey == PluginCapabilityKeys.StorageBlob);

        Assert.Equal("alpha", status.SelectedModuleId);
        Assert.True(status.IsSelectedProviderMissing);
    }

    [Fact]
    public void SelectionEndpoint_RequiresAdminAccess_AndAntiforgery()
    {
        var type = typeof(PluginProviderSelectionController);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Contains(
            type.GetCustomAttributes<PermissionAuthorizeAttribute>(),
            attribute => attribute.Arguments?.Contains(PermissionKeys.AdminAccess) == true);

        var select = type.GetMethod(nameof(PluginProviderSelectionController.Select))!;
        Assert.NotNull(select.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(select.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    private PluginProviderSelectionService CreateSut()
        => new(_services.GetRequiredService<IServiceScopeFactory>(), _runtimeRegistry);

    private void Register(string moduleId, params string[] capabilityKeys)
    {
        var runtime = new RegisteredPluginRuntime(
            moduleId,
            moduleId,
            new Version(1, 0, 0),
            $"test:{moduleId}",
            Array.Empty<PluginRouteContribution>(),
            capabilityKeys.Select(key => new PluginServiceContribution(key, "Acme.Provider")).ToArray(),
            Array.Empty<PluginMemberProviderContribution>(),
            Array.Empty<PluginBackgroundJobContribution>(),
            Array.Empty<PluginNavEntry>(),
            Array.Empty<PluginAuditSinkContribution>(),
            Array.Empty<PluginIdentityProviderContribution>(),
            Array.Empty<PluginSelfServiceProfileProviderContribution>());

        _runtimeRegistry.RegisterAsync(runtime, new NoOpModule(moduleId), AssemblyLoadContext.Default).GetAwaiter().GetResult();
    }

    private sealed class RecordingAuditLog : IAuditLogService
    {
        public List<(string Action, string? Actor, string? TargetId)> Changes { get; } = new();

        public Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LogChangeAsync(string action, object? before, object? after, string? actor = null, string? source = null,
            string? targetType = null, string? targetId = null, object? metadata = null, CancellationToken cancellationToken = default)
        {
            Changes.Add((action, actor, targetId));
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpModule : IPluginModule
    {
        public NoOpModule(string moduleId)
            => Manifest = new PluginManifest(moduleId, moduleId, new Version(1, 0, 0), "tests", "proprietary",
                typeof(NoOpModule).FullName!, ">=1.13.0", Array.Empty<string>(), Array.Empty<string>());

        public PluginManifest Manifest { get; }

        public void RegisterContributions(IPluginContributionSink sink)
        {
        }
    }
}
