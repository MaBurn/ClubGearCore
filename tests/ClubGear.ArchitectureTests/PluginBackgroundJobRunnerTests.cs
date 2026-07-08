using System.Runtime.Loader;
using ClubGear.ArchitectureTests.Plugins;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Plugins.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class PluginBackgroundJobRunnerTests
{
    // ---------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------

    private static RegisteredPluginRuntime MakeRuntime(
        string moduleId,
        IReadOnlyList<PluginBackgroundJobContribution> jobs)
        => new RegisteredPluginRuntime(
            moduleId,
            moduleId,
            new Version(1, 0, 0),
            $"test:{moduleId}",
            Array.Empty<PluginRouteContribution>(),
            Array.Empty<PluginServiceContribution>(),
            Array.Empty<PluginMemberProviderContribution>(),
            jobs,
            Array.Empty<PluginNavEntry>(),
            Array.Empty<PluginAuditSinkContribution>(),
            Array.Empty<PluginIdentityProviderContribution>(),
            Array.Empty<PluginSelfServiceProfileProviderContribution>());

    private static PluginBackgroundJobRunner BuildRunner(
        IPluginRuntimeRegistry registry,
        IHostApplicationLifetime? lifetime = null)
    {
        var services = new ServiceCollection();
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        lifetime ??= new StubHostApplicationLifetime();
        return new PluginBackgroundJobRunner(registry, scopeFactory, lifetime);
    }

    // ---------------------------------------------------------------
    // test 1: GetJobStatuses returns empty list when no jobs started
    // ---------------------------------------------------------------

    [Fact]
    public void GetJobStatuses_ReturnsEmpty_WhenNoJobsStarted()
    {
        var registry = new StubPluginRuntimeRegistry(
            Array.Empty<RegisteredPluginRuntime>(),
            (moduleId, providerType) => null);
        var runner = BuildRunner(registry);

        var statuses = runner.GetJobStatuses("some.module");

        Assert.Empty(statuses);
    }

    // ---------------------------------------------------------------
    // test 2: StartJobsForModuleAsync does nothing when no background jobs registered
    // ---------------------------------------------------------------

    [Fact]
    public async Task StartJobsForModuleAsync_DoesNothing_WhenNoBackgroundJobsRegistered()
    {
        var runtime = MakeRuntime("plugin.a", Array.Empty<PluginBackgroundJobContribution>());
        var registry = new StubPluginRuntimeRegistry([runtime], (_, _) => null);
        var runner = BuildRunner(registry);

        await runner.StartJobsForModuleAsync("plugin.a");

        Assert.Empty(runner.GetJobStatuses("plugin.a"));
    }

    // ---------------------------------------------------------------
    // test 3: Idle → Running → Completed state transition (happy path)
    // ---------------------------------------------------------------

    [Fact]
    public async Task StartJobsForModuleAsync_TransitionsToCompleted_ForSuccessfulJob()
    {
        var jobTypeFullName = typeof(RuntimeLoadedBackgroundJobA).FullName!;
        var runtime = MakeRuntime("plugin.a", [new PluginBackgroundJobContribution("sync", jobTypeFullName)]);
        var completingJob = new CompletingBackgroundJob();

        var registry = new StubPluginRuntimeRegistry(
            [runtime],
            (moduleId, providerType) => string.Equals(providerType, jobTypeFullName, StringComparison.Ordinal)
                ? completingJob
                : null);

        var runner = BuildRunner(registry);

        await runner.StartJobsForModuleAsync("plugin.a");

        // Allow the fire-and-forget task to complete.
        await Task.Delay(200);

        var statuses = runner.GetJobStatuses("plugin.a");
        Assert.Single(statuses);
        var status = statuses[0];
        Assert.Equal("plugin.a", status.ModuleId);
        Assert.Equal("sync", status.JobKey);
        Assert.Equal(jobTypeFullName, status.JobType);
        Assert.Equal(PluginJobRunState.Completed, status.State);
        Assert.NotNull(status.LastRunUtc);
        Assert.Null(status.LastError);
    }

    // ---------------------------------------------------------------
    // test 4: Idle → Running → Faulted when job throws
    // ---------------------------------------------------------------

    [Fact]
    public async Task StartJobsForModuleAsync_TransitionsToFaulted_WhenJobThrows()
    {
        var jobTypeFullName = typeof(RuntimeLoadedBackgroundJobA).FullName!;
        var runtime = MakeRuntime("plugin.a", [new PluginBackgroundJobContribution("faulty", jobTypeFullName)]);
        var throwingJob = new ThrowingBackgroundJob("simulated-failure");

        var registry = new StubPluginRuntimeRegistry(
            [runtime],
            (moduleId, providerType) => throwingJob);

        var runner = BuildRunner(registry);

        await runner.StartJobsForModuleAsync("plugin.a");

        // Allow the fire-and-forget task to complete.
        await Task.Delay(200);

        var statuses = runner.GetJobStatuses("plugin.a");
        Assert.Single(statuses);
        var status = statuses[0];
        Assert.Equal(PluginJobRunState.Faulted, status.State);
        Assert.Equal("simulated-failure", status.LastError);
        Assert.NotNull(status.LastRunUtc);
    }

    // ---------------------------------------------------------------
    // test 5: GetJobStatuses returns correct snapshot for module
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetJobStatuses_ReturnsCorrectSnapshotForModule()
    {
        var jobTypeFullName = typeof(RuntimeLoadedBackgroundJobA).FullName!;
        var runtimeA = MakeRuntime("plugin.a", [
            new PluginBackgroundJobContribution("job1", jobTypeFullName),
            new PluginBackgroundJobContribution("job2", jobTypeFullName)
        ]);
        var runtimeB = MakeRuntime("plugin.b", [
            new PluginBackgroundJobContribution("job1", jobTypeFullName)
        ]);

        var completingJob = new CompletingBackgroundJob();

        var registry = new StubPluginRuntimeRegistry(
            [runtimeA, runtimeB],
            (_, _) => completingJob);

        var runner = BuildRunner(registry);

        await runner.StartJobsForModuleAsync("plugin.a");
        await runner.StartJobsForModuleAsync("plugin.b");

        await Task.Delay(200);

        var statusesA = runner.GetJobStatuses("plugin.a");
        var statusesB = runner.GetJobStatuses("plugin.b");

        Assert.Equal(2, statusesA.Count);
        Assert.Single(statusesB);
        Assert.All(statusesA, s => Assert.Equal("plugin.a", s.ModuleId));
        Assert.All(statusesB, s => Assert.Equal("plugin.b", s.ModuleId));
    }

    // ---------------------------------------------------------------
    // test 6: StopJobsForModuleAsync cancels running jobs within timeout
    // ---------------------------------------------------------------

    [Fact]
    public async Task StopJobsForModuleAsync_CancelsRunningJobs_WithinTimeout()
    {
        var jobTypeFullName = typeof(RuntimeLoadedBackgroundJobA).FullName!;
        var runtime = MakeRuntime("plugin.a", [new PluginBackgroundJobContribution("long-job", jobTypeFullName)]);

        // A job that waits for cancellation and completes cleanly once cancelled.
        var cancellableJob = new CancellableBackgroundJob();

        var registry = new StubPluginRuntimeRegistry(
            [runtime],
            (_, _) => cancellableJob);

        var runner = BuildRunner(registry);

        await runner.StartJobsForModuleAsync("plugin.a");

        // Give the job time to start and begin waiting.
        await Task.Delay(50);

        // StopJobsForModuleAsync must complete well within the test timeout.
        var stopTask = runner.StopJobsForModuleAsync("plugin.a");
        var completed = await Task.WhenAny(stopTask, Task.Delay(6000)) == stopTask;

        Assert.True(completed, "StopJobsForModuleAsync did not complete within the expected time.");

        // After stopping, the module's jobs should be removed from state.
        var statuses = runner.GetJobStatuses("plugin.a");
        Assert.Empty(statuses);
    }

    // ---------------------------------------------------------------
    // test 7: StopJobsForModuleAsync removes jobs from both dictionaries
    // ---------------------------------------------------------------

    [Fact]
    public async Task StopJobsForModuleAsync_RemovesJobEntries_FromState()
    {
        var jobTypeFullName = typeof(RuntimeLoadedBackgroundJobA).FullName!;
        var runtime = MakeRuntime("plugin.a", [new PluginBackgroundJobContribution("sync", jobTypeFullName)]);
        var completingJob = new CompletingBackgroundJob();

        var registry = new StubPluginRuntimeRegistry(
            [runtime],
            (_, _) => completingJob);

        var runner = BuildRunner(registry);

        await runner.StartJobsForModuleAsync("plugin.a");
        await Task.Delay(200); // Let job finish.
        await runner.StopJobsForModuleAsync("plugin.a");

        Assert.Empty(runner.GetJobStatuses("plugin.a"));
    }

    // ---------------------------------------------------------------
    // stubs / fakes
    // ---------------------------------------------------------------

    private sealed class StubPluginRuntimeRegistry : IPluginRuntimeRegistry
    {
        private readonly IReadOnlyList<RegisteredPluginRuntime> _runtimes;
        private readonly Func<string, string, IPluginBackgroundJob?> _providerFactory;

        public StubPluginRuntimeRegistry(
            IReadOnlyList<RegisteredPluginRuntime> runtimes,
            Func<string, string, IPluginBackgroundJob?> providerFactory)
        {
            _runtimes = runtimes;
            _providerFactory = providerFactory;
        }

        public IReadOnlyList<RegisteredPluginRuntime> GetRegisteredPlugins() => _runtimes;

        public RegisteredPluginRuntime? GetByModuleId(string moduleId)
            => _runtimes.FirstOrDefault(r => string.Equals(r.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase));

        public IPluginModule? GetModule(string moduleId) => null;

        public TProvider? CreateMemberProvider<TProvider>(string moduleId, string providerType)
            where TProvider : class
        {
            var job = _providerFactory(moduleId, providerType);
            return job as TProvider;
        }

        public void Register(RegisteredPluginRuntime runtime, IPluginModule module, AssemblyLoadContext loadContext) { }

        public void AddOrReplaceRoute(string moduleId, PluginRouteContribution route) { }

        public bool Unregister(string moduleId) => true;
    }

    private sealed class StubHostApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _cts = new();

        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => _cts.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() => _cts.Cancel();
    }

    /// <summary>A job that completes immediately without error.</summary>
    private sealed class CompletingBackgroundJob : IPluginBackgroundJob
    {
        public Task ExecuteAsync(IPluginHostContext hostContext, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    /// <summary>A job that throws a specific exception message.</summary>
    private sealed class ThrowingBackgroundJob : IPluginBackgroundJob
    {
        private readonly string _message;

        public ThrowingBackgroundJob(string message) => _message = message;

        public Task ExecuteAsync(IPluginHostContext hostContext, CancellationToken cancellationToken)
            => throw new InvalidOperationException(_message);
    }

    /// <summary>A job that delays indefinitely until cancellation is signalled.</summary>
    private sealed class CancellableBackgroundJob : IPluginBackgroundJob
    {
        public async Task ExecuteAsync(IPluginHostContext hostContext, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
}
