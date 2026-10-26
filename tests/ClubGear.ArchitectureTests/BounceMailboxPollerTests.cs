using System.Collections.Concurrent;
using ClubGear.Models;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;
using Xunit;

namespace ClubGear.ArchitectureTests;

/// <summary>
/// Slice 4, checkbox 4.3 DoD: one poll cycle routes every bounce notice to
/// <see cref="IMemberEmailInvalidationService"/>, a throwing fetch is logged and the loop survives
/// to the next tick, cancellation exits promptly without an unhandled exception, and a configured
/// interval below one minute is clamped.
/// </summary>
public sealed class BounceMailboxPollerTests
{
    // ── one cycle: fetch → invalidate ───────────────────────────────────────

    [Fact]
    public async Task PollOnceAsync_RoutesEveryNoticeToTheInvalidationService()
    {
        using var harness = new PollerHarness();
        harness.Mailbox.Enqueue(
            new BounceNotice("erika@example.org", "550 5.1.1 unknown mailbox"),
            new BounceNotice("max@example.org", null));

        await harness.Poller.PollOnceAsync(CancellationToken.None);

        Assert.Equal(
            new[] { "erika@example.org", "max@example.org" },
            harness.Invalidation.Calls.Select(c => c.Address).ToArray());
        Assert.Equal("550 5.1.1 unknown mailbox", harness.Invalidation.Calls[0].Reason);
        Assert.Null(harness.Invalidation.Calls[1].Reason);
    }

    [Fact]
    public async Task PollOnceAsync_WithoutNotices_DoesNotTouchTheInvalidationService()
    {
        using var harness = new PollerHarness();
        harness.Mailbox.Enqueue();

        await harness.Poller.PollOnceAsync(CancellationToken.None);

        Assert.Empty(harness.Invalidation.Calls);
    }

    [Fact]
    public async Task PollOnceAsync_SkipsNoticesWithoutARecipient()
    {
        using var harness = new PollerHarness();
        harness.Mailbox.Enqueue(
            new BounceNotice("   ", "kein Empfaenger"),
            new BounceNotice("max@example.org", null));

        await harness.Poller.PollOnceAsync(CancellationToken.None);

        var call = Assert.Single(harness.Invalidation.Calls);
        Assert.Equal("max@example.org", call.Address);
    }

    // ── the loop ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenAFetchThrows_LogsItAndKeepsPollingOnTheNextTick()
    {
        using var harness = new PollerHarness();
        harness.Mailbox.EnqueueFailure(new InvalidOperationException("IMAP nicht erreichbar"));
        harness.Mailbox.Enqueue(new BounceNotice("erika@example.org", "550 5.1.1 unknown mailbox"));

        await harness.Poller.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(
                await harness.Invalidation.WaitForCallsAsync(1),
                "Der Poller hat nach dem Fehler keinen weiteren Durchlauf ausgefuehrt.");
        }
        finally
        {
            await harness.Poller.StopAsync(CancellationToken.None);
        }

        Assert.Equal("erika@example.org", harness.Invalidation.Calls[0].Address);
        Assert.Contains(harness.Logger.Entries, e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
        Assert.True(harness.Mailbox.FetchCount >= 2);
    }

    [Fact]
    public async Task ExecuteAsync_OnShutdown_StopsPromptlyWithoutAnUnhandledException()
    {
        using var harness = new PollerHarness();

        await harness.Poller.StartAsync(CancellationToken.None);
        Assert.True(await harness.Mailbox.WaitForFetchAsync(1), "Der Poller hat keinen ersten Durchlauf ausgefuehrt.");

        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await harness.Poller.StopAsync(stopTimeout.Token);

        var executeTask = harness.Poller.ExecuteTask;
        Assert.NotNull(executeTask);
        Assert.True(executeTask!.IsCompleted);
        Assert.False(executeTask.IsFaulted);
        Assert.DoesNotContain(harness.Logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task ExecuteAsync_WithBounceCheckDisabled_KeepsRunningWithoutFlaggingAnyone()
    {
        // The shipping default: FetchBouncesAsync returns an empty list, nothing else happens.
        using var harness = new PollerHarness();

        await harness.Poller.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await harness.Mailbox.WaitForFetchAsync(2), "Der Poller hat nicht wiederholt gepollt.");
        }
        finally
        {
            await harness.Poller.StopAsync(CancellationToken.None);
        }

        Assert.Empty(harness.Invalidation.Calls);
        Assert.DoesNotContain(harness.Logger.Entries, e => e.Level == LogLevel.Error);
    }

    // ── interval clamping ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(60, 60)]
    public void ClampInterval_NeverGoesBelowOneMinute(int configuredMinutes, int expectedMinutes)
        => Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), BounceMailboxPoller.ClampInterval(configuredMinutes));

    [Theory]
    [InlineData("0", 1)]
    [InlineData("-3", 1)]
    [InlineData("7", 7)]
    [InlineData("Unsinn", 5)]
    [InlineData(null, 5)]
    public async Task ResolveIntervalAsync_ReadsTheConfiguredValueAndClampsIt(string? configured, int expectedMinutes)
    {
        using var harness = new PollerHarness();
        harness.Config.Set(nameof(EmailSettings.BouncePollIntervalMinutes), configured);

        var interval = await harness.Poller.ResolveIntervalAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), interval);
    }

    [Fact]
    public async Task ExecuteAsync_WithAnIntervalBelowOneMinute_SchedulesTheTimerWithTheClampedValue()
    {
        using var harness = new PollerHarness();
        harness.Config.Set(nameof(EmailSettings.BouncePollIntervalMinutes), "0");

        await harness.Poller.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await harness.Mailbox.WaitForFetchAsync(1), "Der Poller hat keinen Durchlauf ausgefuehrt.");
        }
        finally
        {
            await harness.Poller.StopAsync(CancellationToken.None);
        }

        Assert.NotEmpty(harness.Time.RequestedIntervals);
        Assert.All(harness.Time.RequestedIntervals, requested => Assert.Equal(TimeSpan.FromMinutes(1), requested));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Wires the poller to fakes for its three collaborators and runs its loop on a time provider
    /// that keeps the requested interval visible but ticks in milliseconds.
    /// </summary>
    private sealed class PollerHarness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public PollerHarness()
        {
            var services = new ServiceCollection();
            services.AddScoped<IImapMailboxService>(_ => Mailbox);
            services.AddScoped<IMemberEmailInvalidationService>(_ => Invalidation);
            services.AddScoped<ISystemConfigService>(_ => Config);
            _provider = services.BuildServiceProvider();

            Poller = new BounceMailboxPoller(
                _provider.GetRequiredService<IServiceScopeFactory>(),
                Logger,
                Time);
        }

        public FakeMailbox Mailbox { get; } = new();

        public RecordingInvalidation Invalidation { get; } = new();

        public FakeSystemConfig Config { get; } = new();

        public CapturingLogger Logger { get; } = new();

        public FastTimeProvider Time { get; } = new();

        public BounceMailboxPoller Poller { get; }

        public void Dispose()
        {
            Poller.Dispose();
            _provider.Dispose();
        }
    }

    private sealed class FakeMailbox : IImapMailboxService
    {
        private readonly ConcurrentQueue<Func<IReadOnlyList<BounceNotice>>> _responses = new();
        private readonly SemaphoreSlim _fetched = new(0);
        private int _fetchCount;

        public int FetchCount => Volatile.Read(ref _fetchCount);

        public void Enqueue(params BounceNotice[] notices)
            => _responses.Enqueue(() => notices);

        public void EnqueueFailure(Exception exception)
            => _responses.Enqueue(() => throw exception);

        public Task AppendToSentAsync(MimeMessage message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<BounceNotice>> FetchBouncesAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _fetchCount);
            _fetched.Release();

            return _responses.TryDequeue(out var response)
                ? Task.FromResult(response())
                : Task.FromResult<IReadOnlyList<BounceNotice>>(Array.Empty<BounceNotice>());
        }

        public async Task<bool> WaitForFetchAsync(int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (!await _fetched.WaitAsync(TimeSpan.FromSeconds(10)))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private sealed class RecordingInvalidation : IMemberEmailInvalidationService
    {
        private readonly SemaphoreSlim _called = new(0);
        private readonly List<(string Address, string? Reason)> _calls = new();

        public IReadOnlyList<(string Address, string? Reason)> Calls
        {
            get
            {
                lock (_calls)
                {
                    return _calls.ToArray();
                }
            }
        }

        public Task<int> MarkInvalidByEmailAsync(string address, string? reason, CancellationToken cancellationToken = default)
        {
            lock (_calls)
            {
                _calls.Add((address, reason));
            }

            _called.Release();
            return Task.FromResult(1);
        }

        public async Task<bool> WaitForCallsAsync(int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (!await _called.WaitAsync(TimeSpan.FromSeconds(10)))
                {
                    return false;
                }
            }

            return true;
        }
    }

    private sealed class FakeSystemConfig : ISystemConfigService
    {
        private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);

        public void Set(string name, string? value) => _values[name] = value;

        public Task<string?> GetValueAsync(string section, string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_values.TryGetValue(name, out var value) ? value : null);

        public Task<IReadOnlyList<SystemConfigEntry>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SystemConfigEntry>>(Array.Empty<SystemConfigEntry>());

        public Task<IReadOnlyList<SystemConfigEntry>> GetBySectionAsync(string section, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SystemConfigEntry>>(Array.Empty<SystemConfigEntry>());

        public Task UpsertAsync(string section, string name, string value, string description, CancellationToken cancellationToken = default)
        {
            _values[name] = value;
            return Task.CompletedTask;
        }

        public Task UpsertManyAsync(IEnumerable<SystemConfigEntry> entries, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>
    /// Keeps the poll loop honest about the interval it asks for while running it in milliseconds,
    /// so the loop can be exercised without waiting the clamped minimum of one minute.
    /// </summary>
    private sealed class FastTimeProvider : TimeProvider
    {
        private static readonly TimeSpan Fast = TimeSpan.FromMilliseconds(15);
        private readonly ConcurrentQueue<TimeSpan> _requested = new();

        public IReadOnlyCollection<TimeSpan> RequestedIntervals => _requested.ToArray();

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Record(dueTime);
            Record(period);
            return base.CreateTimer(callback, state, Shrink(dueTime), Shrink(period));
        }

        private void Record(TimeSpan value)
        {
            if (value > TimeSpan.Zero)
            {
                _requested.Enqueue(value);
            }
        }

        private static TimeSpan Shrink(TimeSpan value)
            => value > Fast ? Fast : value;
    }

    private sealed class CapturingLogger : ILogger<BounceMailboxPoller>
    {
        private readonly ConcurrentQueue<LogEntry> _entries = new();

        public IReadOnlyCollection<LogEntry> Entries => _entries.ToArray();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => NullLogger.Instance.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            _entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
        }

        internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
    }
}
