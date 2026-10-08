using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Driver;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class TimePersistenceTests(MongoFixture fixture)
{
    [Fact]
    public async Task ATimeRoundTripsThroughMongoAndSync()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var listId = await SeedList(client, user);
        var taskId = await SeedTask(client, user, listId, "Sprint planning");

        await Send(client, new SetTaskTime(Guid.NewGuid(), user, taskId, TaskTime.Of(new TimeOnly(9, 30), new TimeOnly(11, 0))));

        var loaded = await Persistence.TestContext.For(fixture).Collection<TodoTask>()
            .Find(Builders<TodoTask>.Filter.Eq(task => task.Id, taskId))
            .SingleAsync(ct);
        Assert.Equal(new TaskTime(new TimeOnly(9, 30), new TimeOnly(11, 0)), loaded.Time);

        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var synced = sync!.Documents["todotasks"].Single(row => row.GetProperty("id").GetGuid() == taskId);
        Assert.Equal("09:30:00", synced.GetProperty("time").GetProperty("start").GetString());
    }

    [Fact]
    public async Task AnEndEqualToTheStartIsRejected()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var listId = await SeedList(client, user);
        var taskId = await SeedTask(client, user, listId, "Instant");

        await Send(client, new SetTaskTime(Guid.NewGuid(), user, taskId, new TaskTime(new TimeOnly(11, 0), new TimeOnly(11, 0))));

        var loaded = await Persistence.TestContext.For(fixture).Collection<TodoTask>()
            .Find(Builders<TodoTask>.Filter.Eq(task => task.Id, taskId))
            .SingleAsync(ct);
        Assert.Null(loaded.Time);
    }

    [Fact]
    public async Task AnOvernightTimeRoundTrips()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var listId = await SeedList(client, user);
        var taskId = await SeedTask(client, user, listId, "Night shift");

        await Send(client, new SetTaskTime(Guid.NewGuid(), user, taskId, new TaskTime(new TimeOnly(22, 0), new TimeOnly(1, 0))));

        var loaded = await Persistence.TestContext.For(fixture).Collection<TodoTask>()
            .Find(Builders<TodoTask>.Filter.Eq(task => task.Id, taskId))
            .SingleAsync(ct);
        Assert.Equal(new TaskTime(new TimeOnly(22, 0), new TimeOnly(1, 0)), loaded.Time);

        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var synced = sync!.Documents["todotasks"].Single(row => row.GetProperty("id").GetGuid() == taskId);
        Assert.Equal("01:00:00", synced.GetProperty("time").GetProperty("end").GetString());
        Assert.False(synced.GetProperty("time").TryGetProperty("overnight", out _));
    }

    static async Task<Guid> SeedList(HttpClient client, Guid user)
    {
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        await Send(client,
            new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0),
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Bills", 0));
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
