using PSPad.Abstractions;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Statistics.Tests;

[UnitTest]
public class StatisticsRecordProjectionTests
{
    static readonly DateTimeOffset At = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);
    static readonly Guid User = Guid.NewGuid();
    static readonly Guid Member = Guid.NewGuid();

    [Fact]
    public async Task ARecordIsKeyedBySeqAndUser()
    {
        var store = new InMemoryStatisticsStore();
        var taskId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var created = new TaskCreated(taskId, User, At, listId, "Dune");

        await new StatisticsRecordProjection(store).HandleAsync(
            new DomainEventEnvelope(42, created), CancellationToken.None);

        var record = Assert.Single(store.Records);
        Assert.Equal(StatisticsRecord.IdFor(42, User), record.Id);
        Assert.Equal(42, record.Seq);
        Assert.Equal(RecordRole.Owner, record.Role);
    }

    [Fact]
    public async Task CompletingATaskWritesARecordCarryingTheEventsOwnFacts()
    {
        var store = new InMemoryStatisticsStore();
        var taskId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var completed = new TaskCompleted(taskId, User, At, "Fix the sink", listId, null, new DateOnly(2026, 9, 24));

        await new StatisticsRecordProjection(store).HandleAsync(new DomainEventEnvelope(9, completed), CancellationToken.None);

        var record = Assert.Single(store.Records);
        Assert.Equal(StatisticsRecord.IdFor(9, User), record.Id);
        Assert.Equal(9, record.Seq);
        Assert.Equal(RecordRole.Owner, record.Role);
        Assert.Equal(RecordKind.Completed, record.Kind);
        Assert.Equal("Fix the sink", record.TaskName);
        Assert.Equal(listId, record.ListId);
        Assert.Equal(1, record.CompletionNumber);
    }

    [Fact]
    public async Task FinishingTheSameTaskAgainCountsAsTheSecondTime()
    {
        var store = new InMemoryStatisticsStore();
        var taskId = Guid.NewGuid();
        var projection = new StatisticsRecordProjection(store);
        var first = new TaskCompleted(taskId, User, At, "Water the plants", Guid.NewGuid(), null, null);
        var second = first with { At = At.AddDays(1) };

        await projection.HandleAsync(new DomainEventEnvelope(9, first), CancellationToken.None);
        await projection.HandleAsync(new DomainEventEnvelope(14, second), CancellationToken.None);

        Assert.Equal(2, store.Records.Single(record => record.Seq == 14).CompletionNumber);
    }

    [Fact]
    public async Task HandlingTheSameEventTwiceLeavesOneRecord()
    {
        var store = new InMemoryStatisticsStore();
        var completed = new TaskCompleted(Guid.NewGuid(), User, At, "Fix the sink", Guid.NewGuid(), null, null);
        var projection = new StatisticsRecordProjection(store);

        await projection.HandleAsync(new DomainEventEnvelope(9, completed), CancellationToken.None);
        await projection.HandleAsync(new DomainEventEnvelope(9, completed), CancellationToken.None);

        Assert.Single(store.Records);
    }

    [Fact]
    public async Task AnEventNobodyProjectsIsIgnored()
    {
        var store = new InMemoryStatisticsStore();
        var renamed = new TaskRenamed(Guid.NewGuid(), User, At, "New name");

        await new StatisticsRecordProjection(store).HandleAsync(new DomainEventEnvelope(9, renamed), CancellationToken.None);

        Assert.Empty(store.Records);
    }

    [Fact]
    public async Task UntickingARecurringOccurrenceIsRecordedAsAnUntick()
    {
        var store = new InMemoryStatisticsStore();
        var day = new DateOnly(2026, 9, 24);
        var unticked = new OccurrenceCompleted(
            Guid.NewGuid(), User, At, day, false, "Read a book", Guid.NewGuid(), null);

        await new StatisticsRecordProjection(store).HandleAsync(
            new DomainEventEnvelope(9, unticked), CancellationToken.None);

        Assert.Equal(RecordKind.OccurrenceUnticked, Assert.Single(store.Records).Kind);
    }

    [Fact]
    public async Task TickingARecurringOccurrenceIsItsOwnKindAndNotARecheck()
    {
        var store = new InMemoryStatisticsStore();
        var taskId = Guid.NewGuid();
        var day = new DateOnly(2026, 9, 24);
        var ticked = new OccurrenceCompleted(taskId, User, At, day, true, "Read a book", Guid.NewGuid(), null);

        await new StatisticsRecordProjection(store).HandleAsync(new DomainEventEnvelope(9, ticked), CancellationToken.None);

        var record = Assert.Single(store.Records);
        Assert.Equal(RecordKind.OccurrenceTicked, record.Kind);
        Assert.Equal(day, record.OccurrenceDay);
        Assert.Null(record.CompletionNumber);
    }

    [Fact]
    public async Task AnEventWithNoStoredNameProducesAnEmptyTaskName()
    {
        var store = new InMemoryStatisticsStore();
        var deleted = new TaskDeleted(Guid.NewGuid(), User, At, null!);

        await new StatisticsRecordProjection(store).HandleAsync(new DomainEventEnvelope(9, deleted), CancellationToken.None);

        Assert.Equal("", Assert.Single(store.Records).TaskName);
    }

    [Fact]
    public async Task AMembersCompletionCountsForOwnerAndMember()
    {
        var store = new InMemoryStatisticsStore();
        var taskId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var goalId = Guid.NewGuid();
        var completed = new TaskCompleted(taskId, User, At, "Dune", listId, goalId, null) { ActorId = Member };

        await new StatisticsRecordProjection(store).HandleAsync(new DomainEventEnvelope(7, completed), CancellationToken.None);

        var owner = Assert.Single(store.Records, record => record.UserId == User);
        var member = Assert.Single(store.Records, record => record.UserId == Member);
        Assert.Equal(RecordRole.Owner, owner.Role);
        Assert.Equal(goalId, owner.GoalId);
        Assert.Equal(RecordRole.Actor, member.Role);
        Assert.Null(member.GoalId);
        Assert.Equal(StatisticsRecord.IdFor(7, Member), member.Id);
        Assert.Equal(1, member.CompletionNumber);
    }

    [Fact]
    public async Task SnapshotMarksAreNotActivity()
    {
        var store = new InMemoryStatisticsStore();
        var marked = new TaskSnapshotMarkSet(Guid.NewGuid(), User, At, Guid.NewGuid(), null, true);

        await new StatisticsRecordProjection(store).HandleAsync(new DomainEventEnvelope(9, marked), CancellationToken.None);

        Assert.Empty(store.Records);
    }

    [Fact]
    public async Task TheOwnersOwnActionWritesOneRecord()
    {
        var store = new InMemoryStatisticsStore();
        var taskId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var completed = new TaskCompleted(taskId, User, At, "Dune", listId, null, null);

        await new StatisticsRecordProjection(store).HandleAsync(new DomainEventEnvelope(7, completed), CancellationToken.None);

        Assert.Single(store.Records);
    }
}
