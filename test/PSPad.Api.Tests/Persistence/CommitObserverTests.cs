using Microsoft.Extensions.Logging.Abstractions;
using PSPad.Abstractions;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Persistence;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class CommitObserverTests(MongoFixture fixture)
{
    sealed class NoDispatcher : IDomainEventDispatcher
    {
        public Task PublishAsync(IReadOnlyList<DomainEventEnvelope> events, CancellationToken ct) => Task.CompletedTask;
    }

    sealed record AreaTouched(Guid AggregateId, Guid UserId, DateTimeOffset At)
        : DomainEvent(AggregateId, UserId, At), IUnloggedEvent;

    MongoUnitOfWork Work(ICommitObserver observer) =>
        new(TestContext.For(fixture), new NoDispatcher(), observer, NullLogger<MongoUnitOfWork>.Instance);

    static (Area Area, IReadOnlyList<DomainEvent> Events) NewArea(Guid user)
    {
        var area = new Area();
        var events = Area.Decide(null, new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0), DateTimeOffset.UtcNow);
        area.ApplyAll(events);
        return (area, events);
    }

    [Fact]
    public async Task TheObserverHearsTheCommittedAggregateItsEventsAndTheActor()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var observer = new RecordingCommitObserver();
        var (area, events) = NewArea(user);
        var work = Work(observer);

        work.Stage(area, events);
        await work.CommitAsync(Guid.NewGuid(), user, ct);

        var (actor, changes) = Assert.Single(observer.Calls);
        Assert.Equal(user, actor);
        var change = Assert.Single(changes);
        Assert.Same(area, change.Aggregate);
        Assert.Equal(events, change.Events);
    }

    [Fact]
    public async Task AnUnloggedOnlyCommitStillReachesTheObserver()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var (area, events) = NewArea(user);
        var create = Work(new NoCommitObserver());
        create.Stage(area, events);
        await create.CommitAsync(Guid.NewGuid(), user, ct);

        var observer = new RecordingCommitObserver();
        var touch = Work(observer);
        DomainEvent[] touched = [new AreaTouched(area.Id, user, DateTimeOffset.UtcNow)];
        area.ApplyAll(touched);
        touch.Stage(area, touched);
        await touch.CommitAsync(Guid.NewGuid(), user, ct);

        Assert.Single(observer.Calls);
    }

    [Fact]
    public async Task AThrowingObserverLeavesTheCommandCommitted()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var (area, events) = NewArea(user);
        var work = Work(new ThrowingCommitObserver());

        work.Stage(area, events);
        await work.CommitAsync(Guid.NewGuid(), user, ct);

        Assert.NotNull(await new MongoDocumentStore<Area>(TestContext.For(fixture)).LoadAsync(area.Id, ct));
    }

    [Fact]
    public async Task AReplayedCommandReachesNoObserver()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var (area, events) = NewArea(user);
        var first = Work(new NoCommitObserver());
        first.Stage(area, events);
        await first.CommitAsync(commandId, user, ct);

        var observer = new RecordingCommitObserver();
        var replay = Work(observer);
        var (again, againEvents) = NewArea(user);
        replay.Stage(again, againEvents);
        await replay.CommitAsync(commandId, user, ct);

        Assert.Empty(observer.Calls);
    }
}
