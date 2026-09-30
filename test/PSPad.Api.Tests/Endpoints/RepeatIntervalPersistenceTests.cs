using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.Module.Tasks.Today;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class RepeatIntervalPersistenceTests(MongoFixture fixture)
{
    static DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow.Date);

    [Fact]
    public async Task ARepeatStoredBeforeIntervalsExistedStillOccursDaily()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var listId = await SeedList(client, user);
        var taskId = await SeedTask(client, user, listId, "Read a book");
        await Send(client, new SetTaskRecurrence(
            Guid.NewGuid(), user, taskId, RecurrenceRule.Daily(UtcToday.AddDays(-1))));
        var context = Persistence.TestContext.For(fixture);
        var tasks = context.Collection<BsonDocument>("todotasks");
        var byId = Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(taskId, GuidRepresentation.Standard));
        var stored = await tasks.Find(byId).SingleAsync(ct);
        Assert.True(stored["recurrence"].AsBsonDocument.Contains("interval"));

        await tasks.UpdateOneAsync(byId, Builders<BsonDocument>.Update.Unset("recurrence.interval"), cancellationToken: ct);

        var loaded = await context.Collection<TodoTask>()
            .Find(Builders<TodoTask>.Filter.Eq(task => task.Id, taskId))
            .SingleAsync(ct);
        Assert.Equal(1, loaded.Recurrence!.Every);
        Assert.True(loaded.OccursOn(UtcToday));
        var today = await client.GetFromJsonAsync<TodayEntry[]>("/api/today", ct);
        Assert.True(Assert.Single(today!).Recurring);
    }

    [Fact]
    public async Task AnIntervalOfThreeRoundTripsAndSkipsTheDaysBetween()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var listId = await SeedList(client, user);
        var startedYesterday = await SeedTask(client, user, listId, "Water plants");
        var startedThreeDaysAgo = await SeedTask(client, user, listId, "Change sheets");
        await Send(client,
            new SetTaskRecurrence(Guid.NewGuid(), user, startedYesterday,
                RecurrenceRule.Daily(UtcToday.AddDays(-1)).EveryNth(3)),
            new SetTaskRecurrence(Guid.NewGuid(), user, startedThreeDaysAgo,
                RecurrenceRule.Daily(UtcToday.AddDays(-3)).EveryNth(3)));

        var today = await client.GetFromJsonAsync<TodayEntry[]>("/api/today", ct);
        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        Assert.Equal(startedThreeDaysAgo, Assert.Single(today!).TaskId);
        var synced = sync!.Documents["todotasks"]
            .Single(row => row.GetProperty("id").GetGuid() == startedYesterday);
        Assert.Equal(3, synced.GetProperty("recurrence").GetProperty("interval").GetInt32());
    }

    static async Task<Guid> SeedList(HttpClient client, Guid user)
    {
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        await Send(client,
            new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0),
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Chores"));
        return listId;
    }

    static async Task<Guid> SeedTask(HttpClient client, Guid user, Guid listId, string name)
    {
        var taskId = Guid.NewGuid();
        await Send(client, new CreateTask(Guid.NewGuid(), user, taskId, listId, name));
        return taskId;
    }

    static async Task Send(HttpClient client, params object[] commands)
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var envelopes = commands
            .Select(command => new CommandEnvelope(
                command.GetType().Name, JsonSerializer.SerializeToElement(command, command.GetType())))
            .ToArray();
        var response = await client.PostAsJsonAsync("/api/commands", envelopes, ct);
        response.EnsureSuccessStatusCode();
    }
}
