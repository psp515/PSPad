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
        var work = new MongoUnitOfWork(context, new NoDispatcher(), NullLogger<MongoUnitOfWork>.Instance);
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

        var work = new MongoUnitOfWork(context, new NoDispatcher(), NullLogger<MongoUnitOfWork>.Instance);
        work.Stage(first, firstEvents);
        await work.CommitAsync(Guid.NewGuid(), user, ct);

        var later = new MongoUnitOfWork(context, new NoDispatcher(), NullLogger<MongoUnitOfWork>.Instance);
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

        var first = new MongoUnitOfWork(context, new NoDispatcher(), NullLogger<MongoUnitOfWork>.Instance);
        first.Stage(area, events);
        await first.CommitAsync(commandId, user, ct);

        var replay = new MongoUnitOfWork(context, new NoDispatcher(), NullLogger<MongoUnitOfWork>.Instance);
        replay.Stage(area, events);
        await replay.CommitAsync(commandId, user, ct);

        var log = await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Eq(entry => entry.UserId, user)).ToListAsync(ct);

        Assert.Single(log);
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
