using System.Collections.Concurrent;
using System.Security.Claims;
using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClubGear.Services.Plugins.Runtime;

internal sealed class PluginBackgroundJobRunner : IPluginBackgroundJobRunner
{
    private static readonly ClaimsPrincipal SystemPrincipal = new ClaimsPrincipal(
        new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "system.plugin-job-runner")],
            "System"));

    private readonly IPluginRuntimeRegistry _runtimeRegistry;
    private readonly IPluginContributionActivator _contributionActivator;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CancellationToken _appStopping;
    private readonly PluginRuntimeOptions _options;
    private readonly ILogger<PluginBackgroundJobRunner> _logger;

    // Keyed by "moduleId::jobKey"
    private readonly ConcurrentDictionary<string, PluginJobEntry> _jobs = new(StringComparer.OrdinalIgnoreCase);

    // Keyed by moduleId
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _moduleCts = new(StringComparer.OrdinalIgnoreCase);

    public PluginBackgroundJobRunner(
        IPluginRuntimeRegistry runtimeRegistry,
        IPluginContributionActivator contributionActivator,
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime hostApplicationLifetime,
        IOptions<PluginRuntimeOptions>? options = null,
        ILogger<PluginBackgroundJobRunner>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(runtimeRegistry);
        ArgumentNullException.ThrowIfNull(contributionActivator);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(hostApplicationLifetime);

        _runtimeRegistry = runtimeRegistry;
        _contributionActivator = contributionActivator;
        _scopeFactory = scopeFactory;
        _appStopping = hostApplicationLifetime.ApplicationStopping;
        _options = options?.Value ?? new PluginRuntimeOptions();
        _logger = logger ?? NullLogger<PluginBackgroundJobRunner>.Instance;
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

        var pending = new List<(string JobKey, Task Task)>(keysToRemove.Length);
        foreach (var key in keysToRemove)
        {
            if (_jobs.TryRemove(key, out var entry) && entry.Task is { } t)
            {
                pending.Add((entry.JobKey, t));
            }
        }

        if (pending.Count > 0)
        {
            // Await within the configured drain budget (Plugins:Drain:TimeoutSeconds).
            using var timeoutCts = new CancellationTokenSource(_options.DrainTimeout);
            try
            {
                await Task.WhenAll(pending.Select(p => p.Task)).WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                // Budget expired — jobs did not finish in time. Proceed anyway, but say so:
                // silently abandoning them used to make a hanging job invisible.
                var unfinished = pending
                    .Where(p => !p.Task.IsCompleted)
                    .Select(p => p.JobKey)
                    .ToArray();

                _logger.LogWarning(
                    "Plugin {PluginKey}: {UnfinishedCount} Hintergrundjob(s) wurden nach {DrainSeconds}s nicht beendet: {JobKeys}.",
                    moduleId,
                    unfinished.Length,
                    _options.DrainTimeout.TotalSeconds,
                    string.Join(", ", unfinished));
            }
            catch (Exception)
            {
                // Individual job faults are captured inside RunJobAsync; swallow here.
            }
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
            // CORE-18. The job instance comes from a lease, so the module stays counted as busy
            // for as long as the job runs. The per-module CTS from CORE-17 still runs first: it
            // asks the job to stop, and the lease then makes deactivation wait for it.
            await using var lease = _contributionActivator.Rent<IPluginBackgroundJob>(
                entry.ModuleId, entry.JobType, SystemPrincipal);

            if (lease.Instance is not { } job)
            {
                entry.State = PluginJobRunState.Faulted;
                entry.LastError = lease.Error
                    ?? $"Could not resolve job type '{entry.JobType}' for module '{entry.ModuleId}'.";
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
