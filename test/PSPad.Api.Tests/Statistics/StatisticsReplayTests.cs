using MongoDB.Bson;
using MongoDB.Driver;
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
        await MoveMarkerAsync(context, 0, ct);

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
        await context.Collection<StatisticsRecord>("statistics_records").DeleteManyAsync(record => record.UserId == user, ct);
        await MoveMarkerAsync(context, 0, ct);

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

    static Task MoveMarkerAsync(MongoContext context, long seq, CancellationToken ct) =>
        context.Collection<BsonDocument>("statistics_state").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", "statistics"),
            Builders<BsonDocument>.Update.Set("lastProcessedSeq", seq),
            cancellationToken: ct);

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
