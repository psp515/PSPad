using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Statistics.Tests;

[UnitTest]
public class StatisticsRecordProjectionTests
{
    static readonly DateTimeOffset At = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);
    static readonly Guid User = Guid.NewGuid();

    sealed class FakeStore : IStatisticsStore
    {
        public List<StatisticsRecord> Saved { get; } = [];

        public Task SaveAsync(StatisticsRecord record, CancellationToken ct)
        {
            Saved.RemoveAll(existing => existing.Id == record.Id);
            Saved.Add(record);
            return Task.CompletedTask;
        }

        public Task<int> CountCompletionsBeforeAsync(Guid userId, Guid taskId, long seq, CancellationToken ct) =>
            Task.FromResult(Saved.Count(record =>
                record.UserId == userId && record.TaskId == taskId &&
                record.Kind == RecordKind.Completed && record.Id < seq));

        public Task<IReadOnlyList<StatisticsRecord>> PageAsync(Guid userId, long? before, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StatisticsRecord>>(Saved);

        public Task<IReadOnlyList<StatisticsRecord>> SinceAsync(Guid userId, DateTimeOffset from, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StatisticsRecord>>(Saved);

        public Task<IReadOnlySet<Guid>> OpenTaskIdsBeforeAsync(Guid userId, DateTimeOffset from, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task CompletingATaskWritesARecordCarryingTheEventsOwnFacts()
    {
        var store = new FakeStore();
        var taskId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var completed = new TaskCompleted(taskId, User, At, "Fix the sink", listId, null, new DateOnly(2026, 9, 24));

        await new StatisticsRecordProjection(store).HandleAsync(new DomainEventEnvelope(9, completed), CancellationToken.None);

        var record = Assert.Single(store.Saved);
        Assert.Equal(9, record.Id);
        Assert.Equal(RecordKind.Completed, record.Kind);
        Assert.Equal("Fix the sink", record.TaskName);
        Assert.Equal(listId, record.ListId);
        Assert.Equal(1, record.CompletionNumber);
    }

    [Fact]
    public async Task FinishingTheSameTaskAgainCountsAsTheSecondTime()
    {
        var store = new FakeStore();
        var taskId = Guid.NewGuid();
        var projection = new StatisticsRecordProjection(store);
        var first = new TaskCompleted(taskId, User, At, "Water the plants", Guid.NewGuid(), null, null);
        var second = first with { At = At.AddDays(1) };

        await projection.HandleAsync(new DomainEventEnvelope(9, first), CancellationToken.None);
        await projection.HandleAsync(new DomainEventEnvelope(14, second), CancellationToken.None);

        Assert.Equal(2, store.Saved.Single(record => record.Id == 14).CompletionNumber);
    }

    [Fact]
    public async Task HandlingTheSameEventTwiceLeavesOneRecord()
    {
        var store = new FakeStore();
        var completed = new TaskCompleted(Guid.NewGuid(), User, At, "Fix the sink", Guid.NewGuid(), null, null);
        var projection = new StatisticsRecordProjection(store);

        await projection.HandleAsync(new DomainEventEnvelope(9, completed), CancellationToken.None);
        await projection.HandleAsync(new DomainEventEnvelope(9, completed), CancellationToken.None);

        Assert.Single(store.Saved);
    }

    [Fact]
    public async Task AnEventNobodyProjectsIsIgnored()
    {
        var store = new FakeStore();
        var renamed = new TaskRenamed(Guid.NewGuid(), User, At, "New name");

        await new StatisticsRecordProjection(store).HandleAsync(new DomainEventEnvelope(9, renamed), CancellationToken.None);

        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task UntickingARecurringOccurrenceIsRecordedAsAnUntick()
    {
        var store = new FakeStore();
        var day = new DateOnly(2026, 9, 24);
        var unticked = new OccurrenceCompleted(
            Guid.NewGuid(), User, At, day, false, "Read a book", Guid.NewGuid(), null);

        await new StatisticsRecordProjection(store).HandleAsync(
            new DomainEventEnvelope(9, unticked), CancellationToken.None);

        Assert.Equal(RecordKind.OccurrenceUnticked, Assert.Single(store.Saved).Kind);
    }

    [Fact]
    public async Task TickingARecurringOccurrenceIsItsOwnKindAndNotARecheck()
    {
        var store = new FakeStore();
        var taskId = Guid.NewGuid();
        var day = new DateOnly(2026, 9, 24);
        var ticked = new OccurrenceCompleted(taskId, User, At, day, true, "Read a book", Guid.NewGuid(), null);

        await new StatisticsRecordProjection(store).HandleAsync(new DomainEventEnvelope(9, ticked), CancellationToken.None);

        var record = Assert.Single(store.Saved);
        Assert.Equal(RecordKind.OccurrenceTicked, record.Kind);
        Assert.Equal(day, record.OccurrenceDay);
        Assert.Null(record.CompletionNumber);
    }

    [Fact]
    public async Task AnEventWithNoStoredNameProducesAnEmptyTaskName()
    {
        var store = new FakeStore();
        var deleted = new TaskDeleted(Guid.NewGuid(), User, At, null!);

        await new StatisticsRecordProjection(store).HandleAsync(new DomainEventEnvelope(9, deleted), CancellationToken.None);

        Assert.Equal("", Assert.Single(store.Saved).TaskName);
    }
}
