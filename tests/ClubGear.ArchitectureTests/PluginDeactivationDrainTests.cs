using System.Runtime.Loader;
using System.Security.Claims;
using ClubGear.Models;
using ClubGear.Models.MemberFilters;
using ClubGear.Plugin.Contracts;
using ClubGear.Services;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// CORE-17: deactivation seals the module, drains in-flight plugin work within a bounded budget and
/// unloads regardless. A stuck plugin must never pin the host — it may only leave a note behind.
/// </summary>
public sealed class PluginDeactivationDrainTests
{
    private const string ModuleId = "plugin.drain.subject";

    // Kept deliberately small: these tests only need the budget to expire by itself.
    private static readonly TimeSpan ShortBudget = TimeSpan.FromMilliseconds(50);

    // -----------------------------------------------------------------------
    // Registry: seal -> drain -> unload
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UnregisterAsync_DrainsCleanly_WhenNoWorkIsInFlight()
    {
        var tracker = new PluginActivityTracker();
        var registry = BuildRegistry(tracker, TimeSpan.FromSeconds(30));
        await RegisterAsync(registry);

        var outcome = await registry.UnregisterAsync(ModuleId);

        Assert.True(outcome.Removed);
        Assert.True(outcome.Drained);
        Assert.Equal(0, outcome.InFlightAtUnload);
        Assert.Null(registry.GetByModuleId(ModuleId));
    }

    [Fact]
    public async Task UnregisterAsync_UnloadsAnyway_WhenInFlightWorkNeverFinishes()
    {
        // Design test plan 6. The token is never released, so the drain can only end on the budget.
        var tracker = new PluginActivityTracker();
        var registry = BuildRegistry(tracker, ShortBudget);
        await RegisterAsync(registry);

        using var stuck = tracker.TryAcquire(ModuleId);
        Assert.NotNull(stuck);

        var outcome = await registry.UnregisterAsync(ModuleId);

        Assert.True(outcome.Removed);
        Assert.False(outcome.Drained);
        Assert.Equal(1, outcome.InFlightAtUnload);

        // Unloaded regardless: the registration is gone even though work is still running.
        Assert.Null(registry.GetByModuleId(ModuleId));
    }

    [Fact]
    public async Task UnregisterAsync_SealsTheModule_SoNoNewWorkCanEnter()
    {
        var tracker = new PluginActivityTracker();
        var registry = BuildRegistry(tracker, ShortBudget);
        await RegisterAsync(registry);

        await registry.UnregisterAsync(ModuleId);

        Assert.Null(tracker.TryAcquire(ModuleId));
    }

    [Fact]
    public async Task UnregisterAsync_ReportsNotRemoved_WhenTheModuleWasNeverRegistered()
    {
        var registry = BuildRegistry(new PluginActivityTracker(), ShortBudget);

        var outcome = await registry.UnregisterAsync("plugin.not.registered");

        Assert.False(outcome.Removed);
        Assert.True(outcome.Drained);
    }

    [Fact]
    public async Task RegisterAsync_ReplacePath_DrainsTheDisplacedRegistration_AndUnsealsAfterwards()
    {
        var tracker = new PluginActivityTracker();
        var registry = BuildRegistry(tracker, ShortBudget);
        await RegisterAsync(registry, "Erste Fassung");

        using var stuck = tracker.TryAcquire(ModuleId);

        await RegisterAsync(registry, "Zweite Fassung");

        // The replace path went through the same seal/drain/unload helper and then unsealed,
        // so the new registration accepts work again.
        Assert.Equal("Zweite Fassung", registry.GetByModuleId(ModuleId)!.DisplayName);
        using var afterReplace = tracker.TryAcquire(ModuleId);
        Assert.NotNull(afterReplace);
    }

    // -----------------------------------------------------------------------
    // Adapter: invocations take part in the drain
    // -----------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_IsCountedAsInFlight_WhileThePluginCapabilityRuns()
    {
        // Deterministic: the capability blocks on a gate the test releases explicitly.
        var tracker = new PluginActivityTracker();
        var adapter = BuildAdapter(tracker);
        var module = new DrainTestModule(ModuleId);

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var invocation = adapter.InvokeAsync<int>(
            module,
            BuildUser(),
            async (_, _) =>
            {
                entered.SetResult();
                await release.Task;
                return 42;
            });

        await entered.Task;
        Assert.Equal(1, tracker.GetInFlight(ModuleId));

        release.SetResult();
        Assert.Equal(42, await invocation);
        Assert.Equal(0, tracker.GetInFlight(ModuleId));
    }

    [Fact]
    public async Task InvokeAsync_RejectsWithAUserFriendlyMessage_WhenTheModuleIsSealed()
    {
        var tracker = new PluginActivityTracker();
        var adapter = BuildAdapter(tracker);
        var module = new DrainTestModule(ModuleId);

        tracker.Seal(ModuleId);

        var error = await Assert.ThrowsAsync<UserFriendlyException>(() => adapter.InvokeAsync(
            module,
            BuildUser(),
            (_, _) => Task.FromResult(1)));

        Assert.Equal("Das Plugin wird gerade deaktiviert.", error.Message);
    }

    [Fact]
    public async Task RunAsync_RejectsWithAUserFriendlyMessage_WhenTheModuleIsSealed()
    {
        var tracker = new PluginActivityTracker();
        var adapter = BuildAdapter(tracker);
        var module = new DrainTestModule(ModuleId);

        tracker.Seal(ModuleId);

        var error = await Assert.ThrowsAsync<UserFriendlyException>(() => adapter.RunAsync(
            module,
            BuildUser(),
            (_, _) => Task.CompletedTask));

        Assert.Equal("Das Plugin wird gerade deaktiviert.", error.Message);
    }

    // -----------------------------------------------------------------------
    // Lifecycle: the overrun becomes an operator-visible note
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DeactivateAsync_Succeeds_AndWritesTheOverrunNoteToLastError()
    {
        // Design test plan 7.
        var store = new InMemoryPluginStatusStore(NewActiveRecord());
        var registry = new StubUnloadRegistry(new PluginUnloadOutcome(Removed: true, Drained: false, InFlightAtUnload: 3));
        var lifecycle = BuildLifecycleService(store, registry, TimeSpan.FromSeconds(5));

        var result = await lifecycle.DeactivateAsync(ModuleId);

        Assert.True(result.Success);
        Assert.Equal("deactivated", result.Status);
        Assert.Equal(
            "Deaktiviert, aber 3 laufende Plugin-Aufruf(e) wurden nach 5s nicht beendet.",
            store.GetByKey(ModuleId)!.LastError);
        Assert.False(store.GetByKey(ModuleId)!.IsActive);
    }

    [Fact]
    public async Task DeactivateAsync_ClearsLastError_WhenTheDrainWasClean()
    {
        var record = NewActiveRecord();
        record.LastError = "Alter Fehler";
        var store = new InMemoryPluginStatusStore(record);
        var registry = new StubUnloadRegistry(new PluginUnloadOutcome(Removed: true, Drained: true, InFlightAtUnload: 0));
        var lifecycle = BuildLifecycleService(store, registry, TimeSpan.FromSeconds(5));

        var result = await lifecycle.DeactivateAsync(ModuleId);

        Assert.True(result.Success);
        Assert.Null(store.GetByKey(ModuleId)!.LastError);
    }

    // -----------------------------------------------------------------------
    // Configuration surface
    // -----------------------------------------------------------------------

    [Fact]
    public void PluginRuntimeOptions_DefaultDrainBudget_IsFiveSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), new PluginRuntimeOptions().DrainTimeout);
        Assert.Equal(
            TimeSpan.FromSeconds(5),
            new PluginRuntimeOptions { Drain = new PluginDrainOptions { TimeoutSeconds = 0 } }.DrainTimeout);
        Assert.Equal(
            TimeSpan.FromSeconds(12),
            new PluginRuntimeOptions { Drain = new PluginDrainOptions { TimeoutSeconds = 12 } }.DrainTimeout);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static PluginRegistry BuildRegistry(IPluginActivityTracker tracker, TimeSpan drainTimeout)
        => new(
            tracker,
            Options.Create(new PluginRuntimeOptions { Drain = new PluginDrainOptions { TimeoutSeconds = drainTimeout.TotalSeconds } }),
            NullLogger<PluginRegistry>.Instance);

    private static Task RegisterAsync(PluginRegistry registry, string displayName = "Drain Subject")
        => registry.RegisterAsync(
            new RegisteredPluginRuntime(
                ModuleId,
                displayName,
                new Version(1, 0, 0),
                $"test:{ModuleId}",
                Array.Empty<PluginRouteContribution>(),
                Array.Empty<PluginServiceContribution>(),
                Array.Empty<PluginMemberProviderContribution>(),
                Array.Empty<PluginBackgroundJobContribution>(),
                Array.Empty<PluginNavEntry>(),
                Array.Empty<PluginAuditSinkContribution>(),
                Array.Empty<PluginIdentityProviderContribution>(),
                Array.Empty<PluginSelfServiceProfileProviderContribution>()),
            new DrainTestModule(ModuleId),
            // Non-collectible on purpose: Unload no-ops here, the drain must still be correct.
            AssemblyLoadContext.Default);

    private static PluginRuntimeAdapter BuildAdapter(IPluginActivityTracker tracker)
        => new(
            new AllowAllPermissionFacade(),
            new NoOpAuditFacade(),
            new NoOpNotificationFacade(),
            new NoOpMemberFeatureService(),
            NullLogger<PluginRuntimeAdapter>.Instance,
            activityTracker: tracker);

    private static PluginLifecycleService BuildLifecycleService(
        IPluginStatusStore store,
        IPluginRuntimeRegistry registry,
        TimeSpan drainTimeout)
        => new(
            store,
            registry,
            new PluginEndpointRegistrar(new NoOpRuntimeAdapter(), registry),
            // Deactivation touches neither the loader nor the migration runner.
            pluginLoader: null!,
            pluginMigrationRunner: null!,
            new NoOpJobRunner(),
            NullLogger<PluginLifecycleService>.Instance,
            Options.Create(new PluginRuntimeOptions { Drain = new PluginDrainOptions { TimeoutSeconds = drainTimeout.TotalSeconds } }));

    private static PluginStatusRecord NewActiveRecord()
        => new()
        {
            Id = 1,
            Key = ModuleId,
            DisplayName = "Drain Subject",
            Version = "1.0.0",
            EntryPoint = "Drain.Subject",
            IsActive = true
        };

    private static ClaimsPrincipal BuildUser()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "drain-tester")], "TestAuth"));

    // -----------------------------------------------------------------------
    // Fakes
    // -----------------------------------------------------------------------

    private sealed class DrainTestModule : IPluginModule
    {
        public DrainTestModule(string moduleId)
            => Manifest = new PluginManifest(
                moduleId,
                "Drain Subject",
                new Version(1, 0, 0),
                "ClubGear",
                "MIT",
                "Drain.Subject",
                ">=1.0.0",
                Array.Empty<string>(),
                Array.Empty<string>());

        public PluginManifest Manifest { get; }
    }

    private sealed class StubUnloadRegistry : IPluginRuntimeRegistry
    {
        private readonly PluginUnloadOutcome _outcome;

        public StubUnloadRegistry(PluginUnloadOutcome outcome) => _outcome = outcome;

        public IReadOnlyList<RegisteredPluginRuntime> GetRegisteredPlugins() => Array.Empty<RegisteredPluginRuntime>();

        public RegisteredPluginRuntime? GetByModuleId(string moduleId) => null;

        public IPluginModule? GetModule(string moduleId) => null;

        public Type? ResolveContributionType(string moduleId, string contributionType) => null;

        public Task RegisterAsync(
            RegisteredPluginRuntime runtime,
            IPluginModule module,
            AssemblyLoadContext loadContext,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void AddOrReplaceRoute(string moduleId, PluginRouteContribution route)
        {
        }

        public Task<PluginUnloadOutcome> UnregisterAsync(string moduleId, CancellationToken cancellationToken = default)
            => Task.FromResult(_outcome);
    }

    private sealed class InMemoryPluginStatusStore : IPluginStatusStore
    {
        private readonly Dictionary<string, PluginStatusRecord> _records = new(StringComparer.OrdinalIgnoreCase);

        public InMemoryPluginStatusStore(params PluginStatusRecord[] records)
        {
            foreach (var record in records)
            {
                _records[record.Key] = record;
            }
        }

        public PluginStatusRecord? GetByKey(string key)
            => _records.TryGetValue(key, out var record) ? record : null;

        public IReadOnlyList<PluginStatusRecord> List() => _records.Values.ToArray();

        public Task<PluginStatusRecord> UpsertAsync(PluginStatusRecord record, CancellationToken cancellationToken = default)
        {
            _records[record.Key] = record;
            return Task.FromResult(record);
        }

        public Task DeleteAsync(string key, CancellationToken ct = default)
        {
            _records.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpJobRunner : IPluginBackgroundJobRunner
    {
        public Task StartJobsForModuleAsync(string moduleId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopJobsForModuleAsync(string moduleId) => Task.CompletedTask;

        public IReadOnlyList<PluginJobStatus> GetJobStatuses(string moduleId) => Array.Empty<PluginJobStatus>();
    }

    private sealed class NoOpRuntimeAdapter : IPluginRuntimeAdapter
    {
        public IPluginRuntimeBridge CreateBridge(IPluginModule pluginModule, ClaimsPrincipal user)
            => throw new NotSupportedException();

        public Task RunAsync(
            IPluginModule pluginModule,
            ClaimsPrincipal user,
            Func<IPluginRuntimeBridge, CancellationToken, Task> capability,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<TResult> InvokeAsync<TResult>(
            IPluginModule pluginModule,
            ClaimsPrincipal user,
            Func<IPluginRuntimeBridge, CancellationToken, Task<TResult>> capability,
            string? requiredPermissionKey = null,
            Delegate? isolatedDelegate = null,
            CancellationToken cancellationToken = default) => Task.FromResult<TResult>(default!);

        public void EnsureIsolated(Delegate pluginDelegate)
        {
        }
    }

    private sealed class AllowAllPermissionFacade : IExtensionPermissionFacade
    {
        public Task<bool> HasPermissionAsync(
            ClaimsPrincipal user,
            string permissionKey,
            IReadOnlyCollection<string> declaredPermissions,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
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
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpNotificationFacade : IExtensionNotificationFacade
    {
        public Task<NotificationResult> NotifyAsync(NotificationMessage message, CancellationToken cancellationToken = default)
            => Task.FromResult(new NotificationResult(true, message.Channel, message.Recipient));
    }

    private sealed class NoOpMemberFeatureService : IMemberFeatureService
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
