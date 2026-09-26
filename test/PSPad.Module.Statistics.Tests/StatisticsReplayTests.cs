using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PSPad.Abstractions;
using PSPad.Contracts;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Statistics.Tests;

[UnitTest]
public class StatisticsReplayTests
{
    static readonly DateTimeOffset At = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);
    static readonly Guid User = Guid.NewGuid();

    sealed class FakeEventLog(params RecordedEvent[] events) : IEventLog
    {
        public List<long> ReadsFrom { get; } = [];

        public Task<IReadOnlyList<RecordedEvent>> ReadForwardAsync(
            long afterSeq, int limit, CancellationToken ct)
        {
            ReadsFrom.Add(afterSeq);

            return Task.FromResult<IReadOnlyList<RecordedEvent>>(events
                .Where(recorded => recorded.Seq > afterSeq)
                .OrderBy(recorded => recorded.Seq)
                .Take(limit)
                .ToArray());
        }
    }

    sealed class FailingEventLog : IEventLog
    {
        public Task<IReadOnlyList<RecordedEvent>> ReadForwardAsync(
            long afterSeq, int limit, CancellationToken ct) =>
            Task.FromException<IReadOnlyList<RecordedEvent>>(
                new TimeoutException("the log is unreachable"));
    }

    sealed class FakeMarker(long start) : IProjectionMarker
    {
        public List<long> Written { get; } = [];

        public long Current { get; private set; } = start;

        public Task<long> ReadAsync(CancellationToken ct) => Task.FromResult(Current);

        public Task WriteAsync(long seq, CancellationToken ct)
        {
            Written.Add(seq);
            Current = seq;
            return Task.CompletedTask;
        }
    }

    sealed class RecordingHandler : IDomainEventHandler
    {
        public List<long> Seen { get; } = [];

        public Task HandleAsync(DomainEventEnvelope envelope, CancellationToken ct)
        {
            Seen.Add(envelope.Seq);
            return Task.CompletedTask;
        }
    }

    sealed class ThrowingHandler(long failsOn) : IDomainEventHandler
    {
        public Task HandleAsync(DomainEventEnvelope envelope, CancellationToken ct) =>
            envelope.Seq == failsOn
                ? Task.FromException(new InvalidOperationException("projection failed"))
                : Task.CompletedTask;
    }

    static RecordedEvent Created(long seq) =>
        new(seq, "TodoTask", Guid.NewGuid(), "TaskCreated",
            JsonSerializer.Serialize(
                new TaskCreated(Guid.NewGuid(), User, At, Guid.NewGuid(), $"Task {seq}")),
            At);

    static RecordedEvent Unknown(long seq) =>
        new(seq, "User", Guid.NewGuid(), "UserProvisioned", "{}", At);

    static RecordedEvent Corrupt(long seq) =>
        new(seq, "TodoTask", Guid.NewGuid(), "TaskCreated", "{ truncated", At);

    static StatisticsReplay Replay(IEventLog log, IProjectionMarker marker, params IDomainEventHandler[] handlers) =>
        new(log, marker, handlers, NullLogger<StatisticsReplay>.Instance);

    [Fact]
    public async Task ReplayResumesFromTheStoredMarker()
    {
        var log = new FakeEventLog(Created(1), Created(2), Created(3));
        var marker = new FakeMarker(2);
        var handler = new RecordingHandler();

        await Replay(log, marker, handler).CatchUpAsync(TestContext.Current.CancellationToken);

        Assert.Equal([3], handler.Seen);
        Assert.Equal(2, log.ReadsFrom[0]);
    }

    [Fact]
    public async Task TheMarkerEndsPastEverythingProcessed()
    {
        var log = new FakeEventLog(Created(4), Created(9));
        var marker = new FakeMarker(0);

        await Replay(log, marker, new RecordingHandler())
            .CatchUpAsync(TestContext.Current.CancellationToken);

        Assert.Equal(9, marker.Current);
        Assert.Equal(marker.Written.OrderBy(seq => seq), marker.Written);
    }

    [Fact]
    public async Task ReadingContinuesPastAFullBatch()
    {
        var log = new FakeEventLog(Enumerable.Range(1, 501).Select(seq => Created(seq)).ToArray());
        var marker = new FakeMarker(0);
        var handler = new RecordingHandler();

        await Replay(log, marker, handler).CatchUpAsync(TestContext.Current.CancellationToken);

        Assert.Equal(501, handler.Seen.Count);
        Assert.Equal(501, marker.Current);
        Assert.True(log.ReadsFrom.Count > 1);
    }

    [Fact]
    public async Task AnUnresolvableEventIsSkippedWithoutStallingTheMarker()
    {
        var log = new FakeEventLog(Unknown(5), Created(6));
        var marker = new FakeMarker(0);
        var handler = new RecordingHandler();

        await Replay(log, marker, handler).CatchUpAsync(TestContext.Current.CancellationToken);

        Assert.Equal([6], handler.Seen);
        Assert.Equal(6, marker.Current);
    }

    [Fact]
    public async Task ACorruptPayloadStopsThePassInsteadOfEscapingToTheHost()
    {
        var log = new FakeEventLog(Created(1), Corrupt(2), Created(3));
        var marker = new FakeMarker(0);
        var handler = new RecordingHandler();

        await Replay(log, marker, handler).CatchUpAsync(TestContext.Current.CancellationToken);

        Assert.Equal([1], handler.Seen);
        Assert.Equal(1, marker.Current);
    }

    [Fact]
    public async Task AThrowingHandlerStopsThePassWithTheMarkerBehindTheFailingEvent()
    {
        var log = new FakeEventLog(Created(1), Created(2), Created(3));
        var marker = new FakeMarker(0);
        var recording = new RecordingHandler();

        await Replay(log, marker, recording, new ThrowingHandler(2))
            .CatchUpAsync(TestContext.Current.CancellationToken);

        Assert.Equal([1, 2], recording.Seen);
        Assert.Equal(1, marker.Current);
        Assert.Equal([1], marker.Written);
    }

    [Fact]
    public async Task AFailureOnTheFirstEventOfAPassLeavesTheMarkerUntouched()
    {
        var log = new FakeEventLog(Created(7), Created(8));
        var marker = new FakeMarker(6);

        await Replay(log, marker, new ThrowingHandler(7))
            .CatchUpAsync(TestContext.Current.CancellationToken);

        Assert.Empty(marker.Written);
        Assert.Equal(6, marker.Current);
    }

    [Fact]
    public async Task AnUnreachableLogLeavesTheMarkerAloneInsteadOfEscapingToTheHost()
    {
        var marker = new FakeMarker(4);

        await Replay(new FailingEventLog(), marker, new RecordingHandler())
            .CatchUpAsync(TestContext.Current.CancellationToken);

        Assert.Empty(marker.Written);
        Assert.Equal(4, marker.Current);
    }

    [Fact]
    public async Task AnEmptyLogLeavesTheMarkerWhereItWas()
    {
        var log = new FakeEventLog();
        var marker = new FakeMarker(12);

        await Replay(log, marker, new RecordingHandler())
            .CatchUpAsync(TestContext.Current.CancellationToken);

        Assert.Empty(marker.Written);
        Assert.Equal(12, marker.Current);
    }
}
