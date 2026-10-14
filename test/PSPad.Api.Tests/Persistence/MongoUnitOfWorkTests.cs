using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using PSPad.Abstractions;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Persistence;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MongoUnitOfWorkTests(MongoFixture fixture)
{
    sealed class NoDispatcher : IDomainEventDispatcher
    {
        public Task PublishAsync(IReadOnlyList<DomainEventEnvelope> events, CancellationToken ct) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task CommittingWritesTheAggregateTheEventAndTheCommandId()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var context = TestContext.For(fixture);
        var work = new MongoUnitOfWork(context, new NoDispatcher(), new NoCommitObserver(), NullLogger<MongoUnitOfWork>.Instance);
        var commandId = Guid.NewGuid();
        var (area, events) = NewArea(user);

        work.Stage(area, events);
        await work.CommitAsync(commandId, user, ct);

        var stored = await new MongoDocumentStore<Area>(context).LoadAsync(area.Id, ct);
        var log = await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, user)).ToListAsync(ct);
        var processed = await context.Collection<ProcessedCommand>("processed_commands")
            .Find(Builders<ProcessedCommand>.Filter.Eq(entry => entry.Id, commandId)).ToListAsync(ct);

        Assert.NotNull(stored);
        Assert.Equal(nameof(AreaCreated), Assert.Single(log).Type);
        Assert.Single(processed);
    }

    [Fact]
    public async Task TheSequenceAdvancesAndStampsBothTheEventAndTheAggregate()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var context = TestContext.For(fixture);
        var (first, firstEvents) = NewArea(user);
        var (second, secondEvents) = NewArea(user);

        var work = new MongoUnitOfWork(context, new NoDispatcher(), new NoCommitObserver(), NullLogger<MongoUnitOfWork>.Instance);
        work.Stage(first, firstEvents);
        await work.CommitAsync(Guid.NewGuid(), user, ct);

        var later = new MongoUnitOfWork(context, new NoDispatcher(), new NoCommitObserver(), NullLogger<MongoUnitOfWork>.Instance);
        later.Stage(second, secondEvents);
        await later.CommitAsync(Guid.NewGuid(), user, ct);

        var store = new MongoDocumentStore<Area>(context);
        var storedFirst = await store.LoadAsync(first.Id, ct);
        var storedSecond = await store.LoadAsync(second.Id, ct);

        Assert.True(storedSecond!.Seq > storedFirst!.Seq);
    }

    [Fact]
    public async Task ReplayingACommandIdWritesNothingASecondTime()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var context = TestContext.For(fixture);
        var commandId = Guid.NewGuid();
        var (area, events) = NewArea(user);

        var first = new MongoUnitOfWork(context, new NoDispatcher(), new NoCommitObserver(), NullLogger<MongoUnitOfWork>.Instance);
        first.Stage(area, events);
        await first.CommitAsync(commandId, user, ct);

        var replay = new MongoUnitOfWork(context, new NoDispatcher(), new NoCommitObserver(), NullLogger<MongoUnitOfWork>.Instance);
        replay.Stage(area, events);
        await replay.CommitAsync(commandId, user, ct);

        var log = await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, user)).ToListAsync(ct);

        Assert.Single(log);
    }

    [Fact]
    public async Task AStaleSaveIsRefusedInsteadOfOverwritingANewerOne()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var context = TestContext.For(fixture);
        var store = new MongoDocumentStore<Area>(context);
        var (area, events) = NewArea(user);
        var create = new MongoUnitOfWork(context, new NoDispatcher(), new NoCommitObserver(), NullLogger<MongoUnitOfWork>.Instance);
        create.Stage(area, events);
        await create.CommitAsync(Guid.NewGuid(), user, ct);
        var fresh = (await store.LoadAsync(area.Id, ct))!;
        var stale = (await store.LoadAsync(area.Id, ct))!;

        await Rename(context, fresh, user, "Work", ct);
        var refused = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => Rename(context, stale, user, "Play", ct));

        Assert.Equal(area.Id, refused.AggregateId);
        Assert.Equal("Work", (await store.LoadAsync(area.Id, ct))!.Name);
    }

    [Fact]
    public async Task ANewAggregateIsNeverInsertedOverAnExistingOne()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var context = TestContext.For(fixture);
        var (area, events) = NewArea(user);
        var first = new MongoUnitOfWork(context, new NoDispatcher(), new NoCommitObserver(), NullLogger<MongoUnitOfWork>.Instance);
        first.Stage(area, events);
        await first.CommitAsync(Guid.NewGuid(), user, ct);
        var twin = new Area();
        twin.ApplyAll(events);

        var second = new MongoUnitOfWork(context, new NoDispatcher(), new NoCommitObserver(), NullLogger<MongoUnitOfWork>.Instance);
        second.Stage(twin, events);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.CommitAsync(Guid.NewGuid(), user, ct));
    }

    static async Task Rename(MongoContext context, Area area, Guid user, string name, CancellationToken ct)
    {
        var events = Area.Decide(area, new RenameArea(Guid.NewGuid(), user, area.Id, name), DateTimeOffset.UtcNow);
        area.ApplyAll(events);
        var work = new MongoUnitOfWork(context, new NoDispatcher(), new NoCommitObserver(), NullLogger<MongoUnitOfWork>.Instance);
        work.Stage(area, events);
        await work.CommitAsync(Guid.NewGuid(), user, ct);
    }

    sealed record AreaTouched(Guid AggregateId, Guid UserId, DateTimeOffset At)
        : DomainEvent(AggregateId, UserId, At), IUnloggedEvent;

    sealed class RecordingDispatcher : IDomainEventDispatcher
    {
        public List<DomainEventEnvelope> Published { get; } = [];

        public Task PublishAsync(IReadOnlyList<DomainEventEnvelope> events, CancellationToken ct)
        {
            Published.AddRange(events);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task AnUnloggedEventSavesTheAggregateUnderANewSeqWithoutALogRowOrCommandId()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        var context = TestContext.For(fixture);
        var (area, events) = NewArea(user);
        var created = new MongoUnitOfWork(context, new NoDispatcher(), new NoCommitObserver(), NullLogger<MongoUnitOfWork>.Instance);
        created.Stage(area, events);
        await created.CommitAsync(Guid.NewGuid(), user, ct);
        var createdSeq = area.Seq;

        var dispatcher = new RecordingDispatcher();
        var commandId = Guid.NewGuid();
        var touch = new MongoUnitOfWork(context, dispatcher, new NoCommitObserver(), NullLogger<MongoUnitOfWork>.Instance);
        DomainEvent[] touched = [new AreaTouched(area.Id, user, DateTimeOffset.UtcNow)];
        area.ApplyAll(touched);
        touch.Stage(area, touched);
        await touch.CommitAsync(commandId, user, ct);

        var stored = await new MongoDocumentStore<Area>(context).LoadAsync(area.Id, ct);
        var log = await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, user)).ToListAsync(ct);
        var processed = await context.Collection<ProcessedCommand>("processed_commands")
            .Find(Builders<ProcessedCommand>.Filter.Eq(entry => entry.Id, commandId)).ToListAsync(ct);

        Assert.True(stored!.Seq > createdSeq);
        Assert.Equal(nameof(AreaCreated), Assert.Single(log).Type);
        Assert.Empty(processed);
        Assert.Empty(dispatcher.Published);
    }

    static (Area Area, IReadOnlyList<DomainEvent> Events) NewArea(Guid user)
    {
        var area = new Area();
        var events = Area.Decide(
            null, new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0), DateTimeOffset.UtcNow);
        area.ApplyAll(events);
        return (area, events);
    }
}
