using PSPad.Abstractions;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Goals;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Statistics.Tests;

[UnitTest]
public class StatisticsLabelProjectionTests
{
    static readonly DateTimeOffset At = new(2026, 9, 24, 8, 0, 0, TimeSpan.Zero);
    static readonly Guid User = Guid.NewGuid();

    sealed class FakeLabelStore : ILabelStore
    {
        public List<StatisticsLabel> Saved { get; } = [];

        public Task SaveAsync(StatisticsLabel label, CancellationToken ct)
        {
            Saved.RemoveAll(existing => existing.Id == label.Id);
            Saved.Add(label);
            return Task.CompletedTask;
        }

        public Task<StatisticsLabel?> FindAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Saved.SingleOrDefault(label => label.Id == id));

        public Task<IReadOnlyList<StatisticsLabel>> AllAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StatisticsLabel>>(
                Saved.Where(label => label.UserId == userId).ToList());
    }

    [Fact]
    public async Task AGoalsNameIsRemembered()
    {
        var store = new FakeLabelStore();
        var goalId = Guid.NewGuid();

        await new StatisticsLabelProjection(store).HandleAsync(
            new DomainEventEnvelope(3, new GoalCreated(goalId, User, At, "Fitness")), CancellationToken.None);

        var label = Assert.Single(store.Saved);
        Assert.Equal(LabelKind.Goal, label.Kind);
        Assert.Equal("Fitness", label.Name);
    }

    [Fact]
    public async Task ARenameReplacesTheName()
    {
        var store = new FakeLabelStore();
        var goalId = Guid.NewGuid();
        var projection = new StatisticsLabelProjection(store);

        await projection.HandleAsync(new DomainEventEnvelope(3, new GoalCreated(goalId, User, At, "Fitness")), CancellationToken.None);
        await projection.HandleAsync(new DomainEventEnvelope(8, new GoalRenamed(goalId, User, At, "Health")), CancellationToken.None);

        Assert.Equal("Health", Assert.Single(store.Saved).Name);
    }

    [Fact]
    public async Task ADeletedListKeepsItsNameAndIsMarkedDeleted()
    {
        var store = new FakeLabelStore();
        var listId = Guid.NewGuid();
        var projection = new StatisticsLabelProjection(store);

        await projection.HandleAsync(
            new DomainEventEnvelope(3, new TaskListCreated(listId, User, At, Guid.NewGuid(), "Errands", 0)),
            CancellationToken.None);
        await projection.HandleAsync(
            new DomainEventEnvelope(8, new TaskListDeleted(listId, User, At)), CancellationToken.None);

        var label = Assert.Single(store.Saved);
        Assert.Equal("Errands", label.Name);
        Assert.True(label.Deleted);
    }

    [Fact]
    public async Task AnAreaIsRememberedRenamedAndDeleted()
    {
        var store = new FakeLabelStore();
        var areaId = Guid.NewGuid();
        var projection = new StatisticsLabelProjection(store);

        await projection.HandleAsync(
            new DomainEventEnvelope(1, new AreaCreated(areaId, User, At, "Home", 0)), CancellationToken.None);
        await projection.HandleAsync(
            new DomainEventEnvelope(2, new AreaRenamed(areaId, User, At, "House")), CancellationToken.None);
        await projection.HandleAsync(
            new DomainEventEnvelope(3, new AreaDeleted(areaId, User, At)), CancellationToken.None);

        var label = Assert.Single(store.Saved);
        Assert.Equal(LabelKind.Area, label.Kind);
        Assert.Equal("House", label.Name);
        Assert.True(label.Deleted);
    }

    [Fact]
    public async Task ARenameForALabelThatWasNeverCreatedIsIgnored()
    {
        var store = new FakeLabelStore();
        var goalId = Guid.NewGuid();

        await new StatisticsLabelProjection(store).HandleAsync(
            new DomainEventEnvelope(3, new GoalRenamed(goalId, User, At, "Health")), CancellationToken.None);

        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task ADeleteForALabelThatWasNeverCreatedIsIgnored()
    {
        var store = new FakeLabelStore();
        var listId = Guid.NewGuid();

        await new StatisticsLabelProjection(store).HandleAsync(
            new DomainEventEnvelope(3, new TaskListDeleted(listId, User, At)), CancellationToken.None);

        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task AnUnrelatedEventIsIgnored()
    {
        var store = new FakeLabelStore();
        var goalId = Guid.NewGuid();

        await new StatisticsLabelProjection(store).HandleAsync(
            new DomainEventEnvelope(3, new GoalAchieved(goalId, User, At)), CancellationToken.None);

        Assert.Empty(store.Saved);
    }
}
