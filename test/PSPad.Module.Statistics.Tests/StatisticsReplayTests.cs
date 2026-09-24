using System.Text.Json;
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

    static RecordedEvent Created(long seq) =>
        new(seq, "TodoTask", Guid.NewGuid(), "TaskCreated",
            JsonSerializer.Serialize(
                new TaskCreated(Guid.NewGuid(), User, At, Guid.NewGuid(), $"Task {seq}")),
            At);

    static RecordedEvent Unknown(long seq) =>
        new(seq, "User", Guid.NewGuid(), "UserProvisioned", "{}", At);

    [Fact]
    public async Task ReplayResumesFromTheStoredMarker()
    {
        var log = new FakeEventLog(Created(1), Created(2), Created(3));
        var marker = new FakeMarker(2);
        var handler = new RecordingHandler();

        await new StatisticsReplay(log, marker, [handler]).CatchUpAsync(TestContext.Current.CancellationToken);

        Assert.Equal([3], handler.Seen);
        Assert.Equal(2, log.ReadsFrom[0]);
    }

    [Fact]
    public async Task TheMarkerEndsPastEverythingProcessed()
    {
        var log = new FakeEventLog(Created(4), Created(9));
        var marker = new FakeMarker(0);

        await new StatisticsReplay(log, marker, [new RecordingHandler()])
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

        await new StatisticsReplay(log, marker, [handler]).CatchUpAsync(TestContext.Current.CancellationToken);

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

        await new StatisticsReplay(log, marker, [handler]).CatchUpAsync(TestContext.Current.CancellationToken);

        Assert.Equal([6], handler.Seen);
        Assert.Equal(6, marker.Current);
    }

    [Fact]
    public async Task AnEmptyLogLeavesTheMarkerWhereItWas()
    {
        var log = new FakeEventLog();
        var marker = new FakeMarker(12);

        await new StatisticsReplay(log, marker, [new RecordingHandler()])
            .CatchUpAsync(TestContext.Current.CancellationToken);

        Assert.Empty(marker.Written);
        Assert.Equal(12, marker.Current);
    }
}
