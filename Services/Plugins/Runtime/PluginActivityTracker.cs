using System.Collections.Concurrent;
using ClubGear.Services.Abstractions;

namespace ClubGear.Services.Plugins.Runtime;

/// <summary>
/// Singleton in-flight counter per plugin module (CORE-17).
/// </summary>
public sealed class PluginActivityTracker : IPluginActivityTracker
{
    private readonly ConcurrentDictionary<string, ModuleActivity> _modules = new(StringComparer.OrdinalIgnoreCase);

    public IDisposable? TryAcquire(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var activity = GetOrAdd(moduleId);
        lock (activity.Sync)
        {
            if (activity.IsSealed)
            {
                return null;
            }

            if (activity.InFlight == 0 && activity.Drained.Task.IsCompleted)
            {
                activity.Drained = CreateSignal();
            }

            activity.InFlight++;
        }

        return new ActivityToken(this, moduleId);
    }

    public void Seal(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var activity = GetOrAdd(moduleId);
        lock (activity.Sync)
        {
            activity.IsSealed = true;
        }
    }

    public void Unseal(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        var activity = GetOrAdd(moduleId);
        lock (activity.Sync)
        {
            activity.IsSealed = false;
        }
    }

    public int GetInFlight(string moduleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (!_modules.TryGetValue(moduleId, out var activity))
        {
            return 0;
        }

        lock (activity.Sync)
        {
            return activity.InFlight;
        }
    }

    public async Task<bool> WaitForDrainAsync(string moduleId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);

        if (!_modules.TryGetValue(moduleId, out var activity))
        {
            return true;
        }

        Task drained;
        lock (activity.Sync)
        {
            if (activity.InFlight == 0)
            {
                return true;
            }

            drained = activity.Drained.Task;
        }

        if (timeout <= TimeSpan.Zero)
        {
            return false;
        }

        try
        {
            await drained.WaitAsync(timeout, cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void Release(string moduleId)
    {
        if (!_modules.TryGetValue(moduleId, out var activity))
        {
            return;
        }

        lock (activity.Sync)
        {
            if (activity.InFlight > 0)
            {
                activity.InFlight--;
            }

            if (activity.InFlight == 0)
            {
                activity.Drained.TrySetResult();
            }
        }
    }

    private ModuleActivity GetOrAdd(string moduleId)
        => _modules.GetOrAdd(moduleId, _ => new ModuleActivity(CreateSignal()));

    private static TaskCompletionSource CreateSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class ModuleActivity
    {
        public ModuleActivity(TaskCompletionSource drained) => Drained = drained;

        public object Sync { get; } = new();

        public int InFlight { get; set; }

        public bool IsSealed { get; set; }

        public TaskCompletionSource Drained { get; set; }
    }

    private sealed class ActivityToken : IDisposable
    {
        private readonly PluginActivityTracker _tracker;
        private readonly string _moduleId;
        private int _released;

        public ActivityToken(PluginActivityTracker tracker, string moduleId)
        {
            _tracker = tracker;
            _moduleId = moduleId;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
            {
                return;
            }

            _tracker.Release(_moduleId);
        }
    }
}
