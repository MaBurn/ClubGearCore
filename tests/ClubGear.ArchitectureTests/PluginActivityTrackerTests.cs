using ClubGear.Services.Plugins.Runtime;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// CORE-17: the in-flight counter that deactivation drains before it unloads a plugin.
/// </summary>
public sealed class PluginActivityTrackerTests
{
    private const string ModuleId = "plugin.drain.tracker";

    [Fact]
    public void TryAcquire_CountsInFlight_AndReleasesOnDispose()
    {
        var tracker = new PluginActivityTracker();

        Assert.Equal(0, tracker.GetInFlight(ModuleId));

        var first = tracker.TryAcquire(ModuleId);
        var second = tracker.TryAcquire(ModuleId);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(2, tracker.GetInFlight(ModuleId));

        first!.Dispose();
        Assert.Equal(1, tracker.GetInFlight(ModuleId));

        // Disposing twice must not double-decrement.
        first.Dispose();
        Assert.Equal(1, tracker.GetInFlight(ModuleId));

        second!.Dispose();
        Assert.Equal(0, tracker.GetInFlight(ModuleId));
    }

    [Fact]
    public void Seal_RefusesNewTokens_AndUnsealAllowsThemAgain()
    {
        var tracker = new PluginActivityTracker();

        tracker.Seal(ModuleId);
        Assert.Null(tracker.TryAcquire(ModuleId));

        tracker.Unseal(ModuleId);
        using var token = tracker.TryAcquire(ModuleId);
        Assert.NotNull(token);
    }

    [Fact]
    public void Seal_DoesNotCancelTokensAlreadyHandedOut()
    {
        var tracker = new PluginActivityTracker();

        using var token = tracker.TryAcquire(ModuleId);
        tracker.Seal(ModuleId);

        Assert.NotNull(token);
        Assert.Equal(1, tracker.GetInFlight(ModuleId));
        Assert.Null(tracker.TryAcquire(ModuleId));
    }

    [Fact]
    public async Task WaitForDrainAsync_ReturnsImmediately_WhenNothingIsInFlight()
    {
        var tracker = new PluginActivityTracker();

        Assert.True(await tracker.WaitForDrainAsync("plugin.never.seen", TimeSpan.FromSeconds(30)));

        using (tracker.TryAcquire(ModuleId))
        {
            // in flight
        }

        Assert.True(await tracker.WaitForDrainAsync(ModuleId, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task WaitForDrainAsync_CompletesWhenTheLastTokenIsReleased()
    {
        // Deterministic: the wait is released by disposing the token, not by a sleep. The 30 s
        // budget is only there so the test cannot pass through a timeout.
        var tracker = new PluginActivityTracker();
        var token = tracker.TryAcquire(ModuleId);
        Assert.NotNull(token);

        tracker.Seal(ModuleId);
        var drain = tracker.WaitForDrainAsync(ModuleId, TimeSpan.FromSeconds(30));

        Assert.False(drain.IsCompleted);

        token!.Dispose();

        Assert.True(await drain);
        Assert.Equal(0, tracker.GetInFlight(ModuleId));
    }

    [Fact]
    public async Task WaitForDrainAsync_ReturnsFalse_WhenTheBudgetExpires()
    {
        // A small real budget: the point of the test is that the wait ends by itself.
        var tracker = new PluginActivityTracker();
        using var stuck = tracker.TryAcquire(ModuleId);
        tracker.Seal(ModuleId);

        Assert.False(await tracker.WaitForDrainAsync(ModuleId, TimeSpan.FromMilliseconds(50)));
        Assert.Equal(1, tracker.GetInFlight(ModuleId));
    }

    [Fact]
    public async Task WaitForDrainAsync_IsReusable_AfterTheModuleWasUnsealedAndUsedAgain()
    {
        var tracker = new PluginActivityTracker();

        var first = tracker.TryAcquire(ModuleId);
        tracker.Seal(ModuleId);
        first!.Dispose();
        Assert.True(await tracker.WaitForDrainAsync(ModuleId, TimeSpan.FromSeconds(30)));

        tracker.Unseal(ModuleId);
        using var second = tracker.TryAcquire(ModuleId);
        tracker.Seal(ModuleId);

        // The drain signal from the first round must not leak into the second one.
        Assert.False(await tracker.WaitForDrainAsync(ModuleId, TimeSpan.FromMilliseconds(50)));
    }
}
