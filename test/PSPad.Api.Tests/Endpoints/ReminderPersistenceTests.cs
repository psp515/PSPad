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
public class ReminderPersistenceTests(MongoFixture fixture)
{
    [Fact]
    public async Task AReminderRoundTripsThroughMongoAndSync()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var listId = await SeedList(client, user);
        var taskId = await SeedTask(client, user, listId, "Pay rent");

        await Send(client, new SetTaskReminder(Guid.NewGuid(), user, taskId, TaskReminder.Of(2, new TimeOnly(18, 30))));

        var loaded = await Persistence.TestContext.For(fixture).Collection<TodoTask>()
            .Find(Builders<TodoTask>.Filter.Eq(task => task.Id, taskId))
            .SingleAsync(ct);
        Assert.Equal(new TaskReminder(2, new TimeOnly(18, 30)), loaded.Reminder);
        Assert.NotNull(loaded.ReminderSetAt);

        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var synced = sync!.Documents["todotasks"].Single(row => row.GetProperty("id").GetGuid() == taskId);
        Assert.Equal(2, synced.GetProperty("reminder").GetProperty("daysBefore").GetInt32());
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
