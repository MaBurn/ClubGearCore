using System.Collections.Concurrent;
using System.Security.Claims;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClubGear.Services.Plugins.Runtime;

internal sealed class PluginBackgroundJobRunner : IPluginBackgroundJobRunner
{
    private static readonly ClaimsPrincipal SystemPrincipal = new ClaimsPrincipal(
        new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "system.plugin-job-runner")],
            "System"));

    private readonly IPluginRuntimeRegistry _runtimeRegistry;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CancellationToken _appStopping;

    // Keyed by "moduleId::jobKey"
    private readonly ConcurrentDictionary<string, PluginJobEntry> _jobs = new(StringComparer.OrdinalIgnoreCase);

    // Keyed by moduleId
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _moduleCts = new(StringComparer.OrdinalIgnoreCase);

    public PluginBackgroundJobRunner(
        IPluginRuntimeRegistry runtimeRegistry,
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime hostApplicationLifetime)
    {
        ArgumentNullException.ThrowIfNull(runtimeRegistry);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(hostApplicationLifetime);

        _runtimeRegistry = runtimeRegistry;
        _scopeFactory = scopeFactory;
        _appStopping = hostApplicationLifetime.ApplicationStopping;
    }

    public Task StartJobsForModuleAsync(string moduleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var runtime = _runtimeRegistry.GetByModuleId(moduleId);
        if (runtime is null || runtime.BackgroundJobs.Count == 0)
        {
            return Task.CompletedTask;
        }

        // Create or replace per-module CTS linked to the application stopping token.
        var moduleCts = CancellationTokenSource.CreateLinkedTokenSource(_appStopping, cancellationToken);
        _moduleCts[moduleId] = moduleCts;

        foreach (var contribution in runtime.BackgroundJobs)
        {
            var entryKey = BuildKey(moduleId, contribution.Key);
            var entry = new PluginJobEntry(moduleId, contribution.Key, contribution.JobType);
            _jobs[entryKey] = entry;

            entry.Task = Task.Run(
                () => RunJobAsync(entry, moduleCts.Token),
                CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task StopJobsForModuleAsync(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        // Signal cancellation.
        if (_moduleCts.TryRemove(moduleId, out var cts))
        {
            await cts.CancelAsync();
            cts.Dispose();
        }

        // Collect all tasks for this module.
        var keysToRemove = _jobs.Keys
            .Where(k => k.StartsWith(moduleId + "::", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var pendingTasks = new List<Task>(keysToRemove.Length);
        foreach (var key in keysToRemove)
        {
            if (_jobs.TryRemove(key, out var entry) && entry.Task is { } t)
            {
                pendingTasks.Add(t);
            }
        }

        if (pendingTasks.Count > 0)
        {
            // Await with a 5-second timeout.
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await Task.WhenAll(pendingTasks).WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // Timeout expired — jobs did not finish in time. Proceed anyway.
            }
            catch (Exception)
            {
                // Individual job faults are captured inside RunJobAsync; swallow here.
            }
        }

        foreach (var entry in pendingTasks.Select(_ => default(object)))
        {
            // entries already removed above
        }

        // Mark remaining entries (if any were added concurrently) as Stopped.
        foreach (var key in _jobs.Keys
                     .Where(k => k.StartsWith(moduleId + "::", StringComparison.OrdinalIgnoreCase))
                     .ToArray())
        {
            if (_jobs.TryRemove(key, out var entry))
            {
                entry.State = PluginJobRunState.Stopped;
            }
        }
    }

    public IReadOnlyList<PluginJobStatus> GetJobStatuses(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var prefix = moduleId + "::";
        return _jobs
            .Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Value.ToStatus())
            .ToArray();
    }

    private async Task RunJobAsync(PluginJobEntry entry, CancellationToken cancellationToken)
    {
        entry.State = PluginJobRunState.Running;

        try
        {
            var job = _runtimeRegistry.CreateMemberProvider<IPluginBackgroundJob>(entry.ModuleId, entry.JobType);

            if (job is null)
            {
                entry.State = PluginJobRunState.Faulted;
                entry.LastError = $"Could not resolve job type '{entry.JobType}' for module '{entry.ModuleId}'.";
                return;
            }

            // Build a minimal host context. We use a service scope so downstream
            // services can be resolved from DI if needed. The scope is disposed
            // after the job completes.
            await using var scope = _scopeFactory.CreateAsyncScope();
            var hostContext = new NullPluginHostContext();

            await job.ExecuteAsync(hostContext, cancellationToken);

            entry.LastRunUtc = DateTimeOffset.UtcNow;
            entry.State = PluginJobRunState.Completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            entry.State = PluginJobRunState.Stopped;
        }
        catch (Exception ex)
        {
            entry.LastRunUtc = DateTimeOffset.UtcNow;
            entry.LastError = ex.Message;
            entry.State = PluginJobRunState.Faulted;
        }
    }

    private static string BuildKey(string moduleId, string jobKey)
        => $"{moduleId}::{jobKey}";

    // ---------------------------------------------------------------------------
    // Inner types
    // ---------------------------------------------------------------------------

    private sealed class PluginJobEntry
    {
        public PluginJobEntry(string moduleId, string jobKey, string jobType)
        {
            ModuleId = moduleId;
            JobKey = jobKey;
            JobType = jobType;
            State = PluginJobRunState.Idle;
        }

        public string ModuleId { get; }
        public string JobKey { get; }
        public string JobType { get; }
        public PluginJobRunState State { get; set; }
        public DateTimeOffset? LastRunUtc { get; set; }
        public string? LastError { get; set; }
        public Task? Task { get; set; }

        public PluginJobStatus ToStatus()
            => new PluginJobStatus(ModuleId, JobKey, JobType, State, LastRunUtc, LastError);
    }

    /// <summary>
    /// Minimal no-op host context used when executing background jobs that
    /// do not need full host services. Background jobs that require DI services
    /// should accept them via the IServiceScopeFactory pattern and should not
    /// depend on IPluginHostContext members beyond the interface contract.
    /// </summary>
    private sealed class NullPluginHostContext : IPluginHostContext
    {
        public IPluginMetadataFacade Metadata => throw new NotSupportedException(
            "IPluginHostContext.Metadata is not available in the background-job host context.");

        public IPluginMemberReader Members => throw new NotSupportedException(
            "IPluginHostContext.Members is not available in the background-job host context.");

        public IPluginMemberActionFacade MemberActions => throw new NotSupportedException(
            "IPluginHostContext.MemberActions is not available in the background-job host context.");

        public IPluginDataStore Persistence => throw new NotSupportedException(
            "IPluginHostContext.Persistence is not available in the background-job host context.");

        public IPluginPermissionFacade Permissions => throw new NotSupportedException(
            "IPluginHostContext.Permissions is not available in the background-job host context.");
    }
}
