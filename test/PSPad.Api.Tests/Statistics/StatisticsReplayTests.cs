using System.Net;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Abstractions;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Statistics;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;
using static PSPad.Api.Tests.Statistics.StatisticsFeed;

namespace PSPad.Api.Tests.Statistics;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class StatisticsReplayTests(MongoFixture fixture)
{
    [Fact]
    public async Task AStartCatchesUpEverythingPastTheMarker()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var completed = await CompleteATaskAsync(subject, ct);
        var context = Persistence.TestContext.For(fixture);
        await MoveMarkerAsync(context, completed.Seq - 1, ct);
        await context.Collection<StatisticsRecord>("statistics_records").DeleteOneAsync(
            record => record.Seq == completed.Seq && record.Kind == RecordKind.Completed, ct);

        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(subject);
        var feed = await FeedWhereAsync(client, records => records.Any(record => record.Kind == "Completed"), ct);

        var record = Assert.Single(feed, entry => entry.Kind == "Completed");
        Assert.Equal(completed.Seq, record.Seq);
        Assert.Equal(1, record.CompletionNumber);
        Assert.Equal(1, feed.Count(entry => entry.Kind == "Created"));
    }

    [Fact]
    public async Task ReplayingEveryEventAgainLeavesOneRecordEach()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var before = await SeedAsync(subject, ct);
        var context = Persistence.TestContext.For(fixture);
        await MoveMarkerAsync(context, before.Feed.Min(record => record.Seq) - 1, ct);

        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(subject);
        await MarkerReachesHeadAsync(context, ct);

        var feed = await RecordsAsync(client, ct);
        var overview = await OverviewAsync(client, ct);
        Assert.Equal(before.Feed.Select(record => (record.Seq, record.Kind, record.CompletionNumber)),
            feed.Select(record => (record.Seq, record.Kind, record.CompletionNumber)));
        Assert.Equal(2, overview.InboxCaptures[^1].Count);
        Assert.Equal(before.Overview.Tiles, overview.Tiles);
    }

    [Fact]
    public async Task ReplayingFromTheStartRebuildsEveryRecordPastAFullBatch()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var user = Guid.Empty;
        await using (var factory = new ApiFactory(fixture))
        {
            var client = factory.ClientFor(subject);
            user = await SignInAsync(client, ct);
            var listId = await ListAsync(client, user, ct);
            foreach (var batch in Enumerable.Range(0, 505).Chunk(CommandBatch.MaxEnvelopes))
            {
                await SendAsync(client, ct, batch
                    .Select(index => (object)new CreateTask(Guid.NewGuid(), user, Guid.NewGuid(), listId, $"Task {index}"))
                    .ToArray());
            }

            await EventuallyAsync(() => OverviewAsync(client, ct), view => view.Tiles.OpenedToday == 505, ct);
        }

        var context = Persistence.TestContext.For(fixture);
        var first = await context.Collection<StatisticsRecord>("statistics_records")
            .Find(record => record.UserId == user).SortBy(record => record.Seq).FirstAsync(ct);
        await context.Collection<StatisticsRecord>("statistics_records").DeleteManyAsync(record => record.UserId == user, ct);
        await MoveMarkerAsync(context, first.Seq - 1, ct);

        await using var restarted = new ApiFactory(fixture);
        var rebuilt = await EventuallyAsync(
            () => OverviewAsync(restarted.ClientFor(subject), ct), view => view.Tiles.OpenedToday == 505, ct);

        Assert.Equal(505, rebuilt.Tiles.OpenedToday);
    }

    [Fact]
    public async Task AnEventNobodyCanResolveIsSkippedWithoutStallingTheReplay()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var context = Persistence.TestContext.For(fixture);
        var user = Guid.Empty;
        var after = Guid.NewGuid();
        long unresolvable;
        await using (var factory = new ApiFactory(fixture))
        {
            var client = factory.ClientFor(subject);
            user = await SignInAsync(client, ct);
            var listId = await ListAsync(client, user, ct);
            await SendAsync(client, ct, new CreateTask(Guid.NewGuid(), user, Guid.NewGuid(), listId, "Before"));
            await FeedWhereAsync(client, records => records.Count == 1, ct);
            unresolvable = await AppendUnresolvableEventAsync(context, user, ct);
            await SendAsync(client, ct, new CreateTask(Guid.NewGuid(), user, after, listId, "After"));
            await FeedWhereAsync(client, records => records.Count == 2, ct);
        }

        await context.Collection<StatisticsRecord>("statistics_records")
            .DeleteManyAsync(record => record.UserId == user && record.TaskId == after, ct);
        await MoveMarkerAsync(context, unresolvable - 1, ct);

        await using var restarted = new ApiFactory(fixture);
        var feed = await FeedWhereAsync(
            restarted.ClientFor(subject), records => records.Any(record => record.TaskId == after), ct);

        Assert.Equal(["After", "Before"], feed.Select(record => record.TaskName));
    }

    [Fact]
    public async Task AThrowingHandlerStopsTheReplayJustBehindTheFailingEventAndTheHostStaysUp()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var seeded = await SeedPoisonedAsync(subject, ct);
        var context = Persistence.TestContext.For(fixture);
        await MoveMarkerAsync(context, seeded.Before - 1, ct);
        var handler = new ThrowsOnTaskNamed("Poison", seeded.User);

        await using (var faulty = new ApiFactory(fixture, testServices: services =>
            services.AddSingleton<IDomainEventHandler>(handler)))
        {
            var client = faulty.ClientFor(subject);
            await handler.Attempted.WaitAsync(TimeSpan.FromSeconds(30), ct);
            await EventuallyAsync(() => MarkerAsync(context, ct), marker => marker == seeded.Poison - 1, ct);

            Assert.Equal(HttpStatusCode.OK, await OverviewStatusAsync(client, ct));
            var feed = await RecordsAsync(client, ct);
            Assert.Contains(feed, record => record.TaskName == "Before");
            Assert.DoesNotContain(feed, record => record.TaskName == "After");
        }

        await using var clean = new ApiFactory(fixture);
        var healed = await FeedWhereAsync(
            clean.ClientFor(subject), records => records.Any(record => record.TaskName == "After"), ct);

        Assert.Equal(3, healed.Count);
    }

    [Fact]
    public async Task AFailureOnTheFirstEventOfAPassLeavesTheMarkerWhereItWas()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var seeded = await SeedPoisonedAsync(subject, ct);
        var context = Persistence.TestContext.For(fixture);
        await MoveMarkerAsync(context, seeded.Poison - 1, ct);
        var handler = new ThrowsOnTaskNamed("Poison", seeded.User);

        await using (var faulty = new ApiFactory(fixture, testServices: services =>
            services.AddSingleton<IDomainEventHandler>(handler)))
        {
            var client = faulty.ClientFor(subject);
            await handler.Attempted.WaitAsync(TimeSpan.FromSeconds(30), ct);

            Assert.Equal(HttpStatusCode.OK, await OverviewStatusAsync(client, ct));
            Assert.Equal(seeded.Poison - 1, await MarkerAsync(context, ct));
            Assert.DoesNotContain(await RecordsAsync(client, ct), record => record.TaskName == "After");
        }

        await using var clean = new ApiFactory(fixture);
        await FeedWhereAsync(
            clean.ClientFor(subject), records => records.Any(record => record.TaskName == "After"), ct);
    }

    [Fact]
    public async Task AnUnreachableLogLeavesTheMarkerAloneAndTheHostStaysUp()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var context = Persistence.TestContext.For(fixture);
        await using (var seed = new ApiFactory(fixture))
        {
            await SignInAsync(seed.ClientFor(subject), ct);
        }

        var original = await StateAsync(context, ct);
        var log = new ThrowingEventLog();
        try
        {
            await using var faulty = new ApiFactory(fixture, testServices: services =>
                services.AddSingleton<IEventLog>(log));
            var client = faulty.ClientFor(subject);
            await log.Attempted.WaitAsync(TimeSpan.FromSeconds(30), ct);

            Assert.Equal(HttpStatusCode.OK, await OverviewStatusAsync(client, ct));
            Assert.Equal(original["lastProcessedSeq"].ToInt64(), await MarkerAsync(context, ct));
        }
        finally
        {
            await RestoreStateAsync(context, original, ct);
        }
    }

    [Fact]
    public async Task AFailedClearLeavesTheOldVersionAndTheHostStaysUp()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        var context = Persistence.TestContext.For(fixture);
        await using (var seed = new ApiFactory(fixture))
        {
            await SignInAsync(seed.ClientFor(subject), ct);
        }

        var original = await StateAsync(context, ct);
        var behind = original.DeepClone().AsBsonDocument;
        behind["projectionVersion"] = StatisticsProjection.Version - 1;
        await RestoreStateAsync(context, behind, ct);
        var reset = new ThrowingStatisticsReset();
        try
        {
            await using var faulty = new ApiFactory(fixture, testServices: services =>
                services.AddSingleton<IStatisticsReset>(reset));
            var client = faulty.ClientFor(subject);
            await reset.Attempted.WaitAsync(TimeSpan.FromSeconds(30), ct);

            Assert.Equal(HttpStatusCode.OK, await OverviewStatusAsync(client, ct));
            var state = await StateAsync(context, ct);
            Assert.Equal(StatisticsProjection.Version - 1, state["projectionVersion"].ToInt32());
            Assert.Equal(original["lastProcessedSeq"].ToInt64(), state["lastProcessedSeq"].ToInt64());
        }
        finally
        {
            await RestoreStateAsync(context, original, ct);
        }
    }

    async Task<(IReadOnlyList<StatisticsRecordView> Feed, StatisticsOverview Overview)> SeedAsync(
        string subject, CancellationToken ct)
    {
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(subject);
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var taskId = Guid.NewGuid();
        var inboxId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Buy milk"),
            new CompleteTask(Guid.NewGuid(), user, taskId),
            new CreateInbox(Guid.NewGuid(), user, inboxId),
            new CaptureToInbox(Guid.NewGuid(), user, inboxId, Guid.NewGuid(), "Ring the plumber"),
            new CaptureToInbox(Guid.NewGuid(), user, inboxId, Guid.NewGuid(), "Read the manual"));

        var overview = await EventuallyAsync(
            () => OverviewAsync(client, ct),
            view => view.Tiles.DoneToday == 1 && view.InboxCaptures[^1].Count == 2,
            ct);

        return (await RecordsAsync(client, ct), overview);
    }

    async Task<StatisticsRecordView> CompleteATaskAsync(string subject, CancellationToken ct)
    {
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(subject);
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        var taskId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateTask(Guid.NewGuid(), user, taskId, listId, "Buy milk"),
            new CompleteTask(Guid.NewGuid(), user, taskId));

        var feed = await FeedWhereAsync(client, records => records.Any(record => record.Kind == "Completed"), ct);

        return Assert.Single(feed, record => record.Kind == "Completed");
    }

    static async Task MoveMarkerAsync(MongoContext context, long seq, CancellationToken ct)
    {
        var result = await context.Collection<BsonDocument>("statistics_state").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", "statistics"),
            Builders<BsonDocument>.Update.Set("lastProcessedSeq", seq),
            cancellationToken: ct);
        Assert.Equal(1, result.MatchedCount);
    }

    static async Task<BsonDocument> StateAsync(MongoContext context, CancellationToken ct) =>
        await context.Collection<BsonDocument>("statistics_state")
            .Find(Builders<BsonDocument>.Filter.Eq("_id", "statistics")).FirstAsync(ct);

    static Task RestoreStateAsync(MongoContext context, BsonDocument state, CancellationToken ct) =>
        context.Collection<BsonDocument>("statistics_state").ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", "statistics"), state, cancellationToken: ct);

    static async Task<long> MarkerAsync(MongoContext context, CancellationToken ct) =>
        (await StateAsync(context, ct))["lastProcessedSeq"].ToInt64();

    async Task<Poisoned> SeedPoisonedAsync(string subject, CancellationToken ct)
    {
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(subject);
        var user = await SignInAsync(client, ct);
        var listId = await ListAsync(client, user, ct);
        foreach (var name in new[] { "Before", "Poison", "After" })
        {
            await SendAsync(client, ct, new CreateTask(Guid.NewGuid(), user, Guid.NewGuid(), listId, name));
        }

        var feed = await FeedWhereAsync(client, records => records.Count == 3, ct);
        var context = Persistence.TestContext.For(fixture);
        await context.Collection<StatisticsRecord>("statistics_records").DeleteManyAsync(record => record.UserId == user, ct);

        return new Poisoned(
            user,
            feed.Single(record => record.TaskName == "Before").Seq,
            feed.Single(record => record.TaskName == "Poison").Seq,
            feed.Single(record => record.TaskName == "After").Seq);
    }

    static async Task<HttpStatusCode> OverviewStatusAsync(HttpClient client, CancellationToken ct) =>
        (await client.GetAsync("/api/statistics/overview", ct)).StatusCode;

    sealed record Poisoned(Guid User, long Before, long Poison, long After);

    static async Task MarkerReachesHeadAsync(MongoContext context, CancellationToken ct)
    {
        var head = (await context.Collection<StoredEvent>("events")
            .Find(Builders<StoredEvent>.Filter.Empty)
            .SortByDescending(entry => entry.Seq)
            .Limit(1)
            .FirstAsync(ct)).Seq;

        await EventuallyAsync(
            async () => (await context.Collection<BsonDocument>("statistics_state")
                .Find(Builders<BsonDocument>.Filter.Eq("_id", "statistics")).FirstAsync(ct))["lastProcessedSeq"].ToInt64(),
            marker => marker >= head,
            ct);
    }

    static async Task<long> AppendUnresolvableEventAsync(MongoContext context, Guid user, CancellationToken ct)
    {
        var counter = await context.Collection<BsonDocument>("counters").FindOneAndUpdateAsync(
            Builders<BsonDocument>.Filter.Eq("_id", "events"),
            Builders<BsonDocument>.Update.Inc("value", 1L),
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            ct);
        var seq = counter["value"].ToInt64();
        await context.Collection<StoredEvent>("events").InsertOneAsync(
            new StoredEvent
            {
                Seq = seq,
                UserId = user,
                AggregateType = "User",
                AggregateId = Guid.NewGuid(),
                Type = "AnEventFromALaterVersion",
                Payload = "{}",
                At = DateTimeOffset.UtcNow
            },
            cancellationToken: ct);

        return seq;
    }
}
