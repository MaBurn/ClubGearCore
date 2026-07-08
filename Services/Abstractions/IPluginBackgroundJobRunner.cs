namespace ClubGear.Services.Abstractions;

public interface IPluginBackgroundJobRunner
{
    Task StartJobsForModuleAsync(string moduleId, CancellationToken cancellationToken = default);

    Task StopJobsForModuleAsync(string moduleId);

    IReadOnlyList<PluginJobStatus> GetJobStatuses(string moduleId);
}

public sealed record PluginJobStatus(
    string ModuleId,
    string JobKey,
    string JobType,
    PluginJobRunState State,
    DateTimeOffset? LastRunUtc,
    string? LastError);

public enum PluginJobRunState
{
    Idle,
    Running,
    Completed,
    Faulted,
    Stopped
}
