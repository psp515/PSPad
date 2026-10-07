using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using PSPad.Abstractions;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Persistence;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class ActorStampTests(MongoFixture fixture)
{
    [Fact]
    public async Task AnEventCommittedBySomebodyElseRecordsTheActor()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = TestContext.For(fixture);
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var list = new TaskList();
        var created = new TaskListCreated(Guid.NewGuid(), owner, DateTimeOffset.UtcNow, Guid.NewGuid(), "Books", ListKind.Tasks);
        list.ApplyAll([created]);
        var work = new MongoUnitOfWork(context, new NullDispatcher(), NullLogger<MongoUnitOfWork>.Instance);

        work.Stage(list, [created]);
        await work.CommitAsync(Guid.NewGuid(), member, ct);

        var stored = await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Eq(entry => entry.AggregateId, list.Id))
            .SingleAsync(ct);
        Assert.Equal(owner, stored.UserId);
        Assert.Equal(member, JsonDocument.Parse(stored.Payload).RootElement.GetProperty("ActorId").GetGuid());
    }

    [Fact]
    public async Task AnEventCommittedByItsOwnerCarriesNoActor()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var context = TestContext.For(fixture);
        var owner = Guid.NewGuid();
        var list = new TaskList();
        var created = new TaskListCreated(Guid.NewGuid(), owner, DateTimeOffset.UtcNow, Guid.NewGuid(), "Books", ListKind.Tasks);
        list.ApplyAll([created]);
        var work = new MongoUnitOfWork(context, new NullDispatcher(), NullLogger<MongoUnitOfWork>.Instance);

        work.Stage(list, [created]);
        await work.CommitAsync(Guid.NewGuid(), owner, ct);

        var stored = await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Eq(entry => entry.AggregateId, list.Id))
            .SingleAsync(ct);
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(stored.Payload).RootElement.GetProperty("ActorId").ValueKind);
    }

    sealed class NullDispatcher : IDomainEventDispatcher
    {
        public Task PublishAsync(IReadOnlyList<DomainEventEnvelope> envelopes, CancellationToken ct) => Task.CompletedTask;
    }
}
