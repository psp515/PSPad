using PSPad.Contracts;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Statistics.Tests;

[UnitTest]
public class HistoryReaderTests
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateTimeOffset At = new(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

    sealed class FakeEventLog(params RecordedEvent[] events) : IEventLog
    {
        public long? LastBefore { get; private set; }

        public int LastLimit { get; private set; }

        public Task<IReadOnlyList<RecordedEvent>> ReadAsync(
            Guid userId, long? before, int limit, CancellationToken ct)
        {
            LastBefore = before;
            LastLimit = limit;
            return Task.FromResult<IReadOnlyList<RecordedEvent>>(events);
        }

        public Task<IReadOnlyList<RecordedEvent>> ReadForwardAsync(
            long afterSeq, int limit, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task AnEventBecomesASentence()
    {
        var log = new FakeEventLog(new RecordedEvent(
            7, "TodoTask", Guid.NewGuid(), "TaskCompleted", "{}", At));

        var entries = await new HistoryReader(log).ReadAsync(User, null, 50, CancellationToken.None);

        Assert.Equal("Completed a task", Assert.Single(entries).Description);
    }

    [Fact]
    public async Task AnEventNobodyWroteADescriptionForStillReadsSensibly()
    {
        var log = new FakeEventLog(new RecordedEvent(
            7, "TodoTask", Guid.NewGuid(), "TaskSomethingNew", "{}", At));

        var entries = await new HistoryReader(log).ReadAsync(User, null, 50, CancellationToken.None);

        Assert.Equal("Task something new", Assert.Single(entries).Description);
    }

    [Fact]
    public async Task TheLimitIsClampedToSomethingAPageCanHold()
    {
        var log = new FakeEventLog();

        await new HistoryReader(log).ReadAsync(User, null, 5000, CancellationToken.None);

        Assert.Equal(200, log.LastLimit);
    }

    [Fact]
    public async Task ALimitBelowOneBecomesTheDefault()
    {
        var log = new FakeEventLog();

        await new HistoryReader(log).ReadAsync(User, null, 0, CancellationToken.None);

        Assert.Equal(50, log.LastLimit);
    }

    [Fact]
    public async Task TheCursorIsPassedStraightThrough()
    {
        var log = new FakeEventLog();

        await new HistoryReader(log).ReadAsync(User, 120, 50, CancellationToken.None);

        Assert.Equal(120, log.LastBefore);
    }
}
