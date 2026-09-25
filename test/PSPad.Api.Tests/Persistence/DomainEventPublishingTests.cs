using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using PSPad.Abstractions;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Persistence;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class DomainEventPublishingTests(MongoFixture fixture)
{
    sealed class RecordingDispatcher : IDomainEventDispatcher
    {
        public List<DomainEventEnvelope> Published { get; } = [];

        public Task PublishAsync(IReadOnlyList<DomainEventEnvelope> events, CancellationToken ct)
        {
            Published.AddRange(events);
            return Task.CompletedTask;
        }
    }

    sealed class ThrowingDispatcher : IDomainEventDispatcher
    {
        public Task PublishAsync(IReadOnlyList<DomainEventEnvelope> events, CancellationToken ct) =>
            throw new InvalidOperationException("the bus is down");
    }

    [Fact]
    public async Task CommittedEventsArePublishedWithTheirSeq()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var dispatcher = new RecordingDispatcher();
        var work = NewUnitOfWork(dispatcher);
        var userId = Guid.NewGuid();
        var area = new Area();
        var events = Area.Decide(null, new CreateArea(Guid.NewGuid(), userId, Guid.NewGuid(), "Home", 0), DateTimeOffset.UtcNow);
        area.ApplyAll(events);
        work.Stage(area, events);

        await work.CommitAsync(Guid.NewGuid(), userId, ct);

        var published = Assert.Single(dispatcher.Published);
        Assert.IsType<AreaCreated>(published.Event);
        Assert.Equal(area.Seq, published.Seq);
    }

    [Fact]
    public async Task ADispatcherFailureNeverFailsAnAcceptedCommand()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var work = NewUnitOfWork(new ThrowingDispatcher());
        var userId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var area = new Area();
        var events = Area.Decide(null, new CreateArea(Guid.NewGuid(), userId, areaId, "Home", 0), DateTimeOffset.UtcNow);
        area.ApplyAll(events);
        work.Stage(area, events);

        await work.CommitAsync(Guid.NewGuid(), userId, ct);

        var stored = await fixture.Database
            .GetCollection<Area>("areas")
            .Find(document => document.Id == areaId)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(stored);
    }

    MongoUnitOfWork NewUnitOfWork(IDomainEventDispatcher dispatcher) =>
        new(TestContext.For(fixture), dispatcher, NullLogger<MongoUnitOfWork>.Instance);
}
