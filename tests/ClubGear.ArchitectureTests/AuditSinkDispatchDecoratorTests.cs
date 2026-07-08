using ClubGear.Plugin.Contracts;
using ClubGear.Services.Abstractions;
using ClubGear.Services.Core;
using ClubGear.Services.Plugins.AuditSink;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClubGear.ArchitectureTests;

public sealed class AuditSinkDispatchDecoratorTests
{
    // ---------------------------------------------------------------
    // helpers
    // ---------------------------------------------------------------

    private static AuditLogRecord MakeRecord(string action = "test.action")
        => new AuditLogRecord(
            Action: action,
            Actor: "actor1",
            Source: "source1",
            TargetType: "Member",
            TargetId: "42",
            OccurredAtUtc: new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc));

    private AuditSinkDispatchDecorator MakeDecorator(
        IAuditLogService? inner = null,
        IPluginAuditSinkService? sinkService = null)
        => new AuditSinkDispatchDecorator(
            inner ?? new RecordingAuditLogService(),
            sinkService ?? new RecordingSinkService(),
            NullLogger<AuditSinkDispatchDecorator>.Instance);

    // ---------------------------------------------------------------
    // test (a): inner LogAsync is invoked before DispatchAsync
    // ---------------------------------------------------------------

    [Fact]
    public async Task LogAsync_CallsInner_BeforeDispatch()
    {
        var callOrder = new List<string>();

        var inner = new OrderRecordingAuditLogService(callOrder, "inner");
        var sinkService = new OrderRecordingSinkService(callOrder, "dispatch");

        var decorator = new AuditSinkDispatchDecorator(
            inner,
            sinkService,
            NullLogger<AuditSinkDispatchDecorator>.Instance);

        await decorator.LogAsync(MakeRecord());

        Assert.Equal(2, callOrder.Count);
        Assert.Equal("inner", callOrder[0]);
        Assert.Equal("dispatch", callOrder[1]);
    }

    // ---------------------------------------------------------------
    // test (b): DispatchAsync is called exactly once after inner.LogAsync returns
    // ---------------------------------------------------------------

    [Fact]
    public async Task LogAsync_CallsDispatchAsync_ExactlyOnce_AfterInnerReturns()
    {
        var inner = new RecordingAuditLogService();
        var sinkService = new RecordingSinkService();

        var decorator = new AuditSinkDispatchDecorator(
            inner,
            sinkService,
            NullLogger<AuditSinkDispatchDecorator>.Instance);

        await decorator.LogAsync(MakeRecord());

        Assert.Equal(1, inner.LogAsyncCallCount);
        Assert.Equal(1, sinkService.DispatchCallCount);
    }

    // ---------------------------------------------------------------
    // test (c): sink dispatch throws — exception swallowed, not surfaced
    // ---------------------------------------------------------------

    [Fact]
    public async Task LogAsync_SwallowsException_WhenDispatchThrows()
    {
        var inner = new RecordingAuditLogService();
        var sinkService = new ThrowingSinkService();

        var decorator = new AuditSinkDispatchDecorator(
            inner,
            sinkService,
            NullLogger<AuditSinkDispatchDecorator>.Instance);

        // Must complete without throwing.
        var exception = await Record.ExceptionAsync(() => decorator.LogAsync(MakeRecord()));

        Assert.Null(exception);
        Assert.Equal(1, inner.LogAsyncCallCount);
    }

    // ---------------------------------------------------------------
    // test (d): LogChangeAsync builds correct AuditLogRecord and delegates
    //           to LogAsync (inner called exactly once)
    // ---------------------------------------------------------------

    [Fact]
    public async Task LogChangeAsync_BuildsCorrectRecord_AndDelegatesToLogAsync()
    {
        PluginAuditEvent? capturedEvent = null;
        var inner = new RecordingAuditLogService();
        var sinkService = new CapturingSinkService(e => capturedEvent = e);

        var decorator = new AuditSinkDispatchDecorator(
            inner,
            sinkService,
            NullLogger<AuditSinkDispatchDecorator>.Instance);

        await decorator.LogChangeAsync(
            action: "member.update",
            before: null,
            after: null,
            actor: "admin",
            source: "web",
            targetType: "Member",
            targetId: "99");

        // Inner was called exactly once (proving LogChangeAsync delegates to LogAsync).
        Assert.Equal(1, inner.LogAsyncCallCount);
        Assert.Equal("member.update", inner.LastRecord?.Action);
        Assert.Equal("admin", inner.LastRecord?.Actor);
        Assert.Equal("Member", inner.LastRecord?.TargetType);
        Assert.Equal("99", inner.LastRecord?.TargetId);

        // DispatchAsync received an event with the correct fields.
        Assert.NotNull(capturedEvent);
        Assert.Equal("member.update", capturedEvent!.Action);
        Assert.Equal("admin", capturedEvent.Actor);
        Assert.Equal("Member", capturedEvent.TargetType);
        Assert.Equal("99", capturedEvent.TargetId);
    }

    // ---------------------------------------------------------------
    // stub / fake helpers
    // ---------------------------------------------------------------

    private sealed class RecordingAuditLogService : IAuditLogService
    {
        private int _logAsyncCallCount;
        public int LogAsyncCallCount => _logAsyncCallCount;
        public AuditLogRecord? LastRecord { get; private set; }

        public Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _logAsyncCallCount);
            LastRecord = record;
            return Task.CompletedTask;
        }

        public Task LogChangeAsync(
            string action, object? before, object? after,
            string? actor = null, string? source = null,
            string? targetType = null, string? targetId = null,
            object? metadata = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingSinkService : IPluginAuditSinkService
    {
        private int _dispatchCallCount;
        public int DispatchCallCount => _dispatchCallCount;

        public Task DispatchAsync(PluginAuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _dispatchCallCount);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSinkService : IPluginAuditSinkService
    {
        public Task DispatchAsync(PluginAuditEvent auditEvent, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated dispatch failure.");
    }

    private sealed class CapturingSinkService : IPluginAuditSinkService
    {
        private readonly Action<PluginAuditEvent> _onDispatch;

        public CapturingSinkService(Action<PluginAuditEvent> onDispatch)
            => _onDispatch = onDispatch;

        public Task DispatchAsync(PluginAuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            _onDispatch(auditEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class OrderRecordingAuditLogService : IAuditLogService
    {
        private readonly List<string> _callOrder;
        private readonly string _label;

        public OrderRecordingAuditLogService(List<string> callOrder, string label)
        {
            _callOrder = callOrder;
            _label = label;
        }

        public Task LogAsync(AuditLogRecord record, CancellationToken cancellationToken = default)
        {
            _callOrder.Add(_label);
            return Task.CompletedTask;
        }

        public Task LogChangeAsync(
            string action, object? before, object? after,
            string? actor = null, string? source = null,
            string? targetType = null, string? targetId = null,
            object? metadata = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class OrderRecordingSinkService : IPluginAuditSinkService
    {
        private readonly List<string> _callOrder;
        private readonly string _label;

        public OrderRecordingSinkService(List<string> callOrder, string label)
        {
            _callOrder = callOrder;
            _label = label;
        }

        public Task DispatchAsync(PluginAuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            _callOrder.Add(_label);
            return Task.CompletedTask;
        }
    }
}
