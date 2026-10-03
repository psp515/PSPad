using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Contracts;
using PSPad.Module.Statistics;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Statistics;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class StatisticsRebuildTests(MongoFixture fixture)
{
    [Fact]
    public async Task AStartWithAnOlderVersionRebuildsEveryRecord()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var subject = Guid.NewGuid().ToString();
        await using var seedFactory = new ApiFactory(fixture);
        var client = seedFactory.ClientFor(subject);
        var user = await UserId(client, ct);
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0)),
            Envelope(new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Errands")),
            Envelope(new CreateTask(Guid.NewGuid(), user, taskId, listId, "Buy milk"))
        }, ct);
        await EventuallyAsync(() => Records(client, ct), feed => feed.Count > 0, ct);

        var context = Persistence.TestContext.For(fixture);
        var head = await context.Collection<BsonDocument>("events")
            .Find(Builders<BsonDocument>.Filter.Empty)
            .SortByDescending(document => document["seq"])
            .Limit(1)
            .FirstAsync(ct);
        await context.Collection<BsonDocument>("statistics_state").ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", "statistics"),
            new BsonDocument
            {
                ["_id"] = "statistics",
                ["lastProcessedSeq"] = head["seq"].ToInt64(),
                ["projectionVersion"] = 1
            },
            new ReplaceOptions { IsUpsert = true },
            ct);
        await context.Collection<StatisticsRecord>("statistics_records").InsertOneAsync(
            new StatisticsRecord
            {
                Id = StatisticsRecord.IdFor(999999, user),
                Seq = 999999,
                Role = RecordRole.Owner,
                UserId = user,
                At = DateTimeOffset.UtcNow,
                Kind = RecordKind.Created,
                TaskId = Guid.NewGuid(),
                TaskName = "junk"
            },
            cancellationToken: ct);

        await using var rebuiltFactory = new ApiFactory(fixture);
        var rebuiltClient = rebuiltFactory.ClientFor(subject);
        var records = await EventuallyAsync(
            () => Records(rebuiltClient, ct),
            feed => feed.Any(record => record.TaskId == taskId),
            ct);

        Assert.DoesNotContain(records, record => record.Seq == 999999);
        Assert.Contains(records, record => record.TaskId == taskId && record.Kind == "Created");
    }

    static async Task<IReadOnlyList<StatisticsRecordView>> Records(HttpClient client, CancellationToken ct) =>
        await client.GetFromJsonAsync<StatisticsRecordView[]>("/api/statistics/records", ct) ?? [];

    static async Task<T> EventuallyAsync<T>(Func<Task<T>> read, Func<T, bool> until, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var value = await read();

            if (until(value))
            {
                return value;
            }

            await Task.Delay(100, ct);
        }

        throw new TimeoutException("the projection never caught up");
    }

    static async Task<Guid> UserId(HttpClient client, CancellationToken ct) =>
        (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;

    static CommandEnvelope Envelope<T>(T command) where T : notnull =>
        new(typeof(T).Name, JsonSerializer.SerializeToElement(command));
}
