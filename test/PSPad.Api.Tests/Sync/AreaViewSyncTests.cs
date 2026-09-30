using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Contracts;
using PSPad.Module.Presentation.AreaViews;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class AreaViewSyncTests(MongoFixture fixture)
{
    [Fact]
    public async Task AReorderSyncsAsAnAreaView()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var areaId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await Send(client, ct,
            new CreateArea(Guid.NewGuid(), user, areaId, "Dom", 0),
            new CreateTaskList(Guid.NewGuid(), user, first, areaId, "Zakupy"),
            new CreateTaskList(Guid.NewGuid(), user, second, areaId, "Remont"),
            new ReorderLists(Guid.NewGuid(), user, areaId, [first, second], second, 0));

        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        var row = Assert.Single(sync!.Documents["areaviews"]);
        Assert.Equal(AreaView.IdFor(user, areaId), row.GetProperty("id").GetGuid());
        Assert.Equal(
            [second, first],
            row.GetProperty("order").EnumerateArray().Select(id => id.GetGuid()).ToArray());
    }

    [Fact]
    public async Task ResendingTheSameReorderIsANoOp()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var areaId = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var reorder = new ReorderLists(Guid.NewGuid(), user, areaId, [first, second], second, 0);

        await Send(client, ct, reorder);
        await Send(client, ct, reorder);

        var context = Persistence.TestContext.For(fixture);
        var events = await context.Collection<BsonDocument>("events")
            .Find(Builders<BsonDocument>.Filter.Eq("aggregateId", AreaView.IdFor(user, areaId)))
            .CountDocumentsAsync(ct);
        Assert.Equal(1, events);
    }

    static async Task Send(HttpClient client, CancellationToken ct, params object[] commands)
    {
        var envelopes = commands
            .Select(command => new CommandEnvelope(
                command.GetType().Name, JsonSerializer.SerializeToElement(command, command.GetType())))
            .ToArray();
        var response = await client.PostAsJsonAsync("/api/commands", envelopes, ct);
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct);
        Assert.All(results!, result => Assert.True(result.Accepted, result.Rejection));
    }
}
