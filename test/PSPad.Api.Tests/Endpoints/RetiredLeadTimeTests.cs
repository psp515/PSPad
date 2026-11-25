using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Recurrence;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class RetiredLeadTimeTests(MongoFixture fixture)
{
    static DateOnly UtcToday => DateOnly.FromDateTime(DateTime.UtcNow.Date);

    [Fact]
    public async Task AnOldClientsLeadTimeCommandIsRefusedForGood()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var taskId = await SeedTask(client, user, "Car insurance");
        var payload = JsonSerializer.SerializeToElement(new
        {
            commandId = Guid.NewGuid(),
            userId = user,
            taskId,
            leadTime = new { amount = 2, unit = "Weeks" }
        });

        var response = await client.PostAsJsonAsync("/api/commands", new[] { new CommandEnvelope("SetTaskLeadTime", payload) }, ct);
        response.EnsureSuccessStatusCode();
        var result = Assert.Single((await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct))!);

        Assert.False(result.Accepted);
        Assert.True(result.Unrecoverable);
    }

    [Fact]
    public async Task ATaskStoredWithALeadTimeStillLoadsAndSyncsWithoutIt()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var taskId = await SeedTask(client, user, "Passport");
        await Send(client, new SetTaskRecurrence(Guid.NewGuid(), user, taskId, RecurrenceRule.Yearly(UtcToday)));
        var context = Persistence.TestContext.For(fixture);
        await context.Collection<BsonDocument>("todotasks").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(taskId, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Update.Set("leadTime", new BsonDocument { { "amount", 2 }, { "unit", 1 } }),
            cancellationToken: ct);

        var loaded = await context.Collection<TodoTask>()
            .Find(Builders<TodoTask>.Filter.Eq(task => task.Id, taskId))
            .SingleAsync(ct);
        Assert.Equal(RecurrenceKind.Yearly, loaded.Recurrence!.Kind);

        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var synced = sync!.Documents["todotasks"].Single(row => row.GetProperty("id").GetGuid() == taskId);
        Assert.False(synced.TryGetProperty("leadTime", out _));
    }

    [Fact]
    public async Task TheStartBackfillRemovesAStoredLeadTimeAndKeepsTheRest()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var taskId = await SeedTask(client, user, "Car insurance");
        await Send(client, new SetTaskRecurrence(Guid.NewGuid(), user, taskId, RecurrenceRule.Yearly(UtcToday)));
        var context = Persistence.TestContext.For(fixture);
        var tasks = context.Collection<BsonDocument>("todotasks");
        var byId = Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(taskId, GuidRepresentation.Standard));
        await tasks.UpdateOneAsync(
            byId, Builders<BsonDocument>.Update.Set("leadTime", new BsonDocument { { "amount", 2 }, { "unit", 1 } }),
            cancellationToken: ct);

        await MongoBackfill.DropLeadTimeAsync(context, ct);

        var stored = await tasks.Find(byId).SingleAsync(ct);
        Assert.False(stored.Contains("leadTime"));
        Assert.True(stored["recurrence"].IsBsonDocument);
    }

    static async Task<Guid> SeedTask(HttpClient client, Guid user, string name)
    {
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await Send(client,
            new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0),
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Bills", 0),
            new CreateTask(Guid.NewGuid(), user, taskId, listId, name));
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
