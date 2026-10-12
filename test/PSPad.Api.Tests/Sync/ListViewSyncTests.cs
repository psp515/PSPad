using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Presentation.ListViews;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class ListViewSyncTests(MongoFixture fixture)
{
    [Fact]
    public async Task APlacementSyncsAsAListView()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = (await client.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();

        await Send(client, ct,
            new CreateArea(Guid.NewGuid(), user, areaId, "Dom", 0),
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Zakupy"),
            new PlaceList(Guid.NewGuid(), user, listId, areaId));

        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        var row = Assert.Single(sync!.Documents["listviews"]);
        Assert.Equal(ListView.IdFor(user, listId), row.GetProperty("id").GetGuid());
        Assert.Equal(areaId, row.GetProperty("areaId").GetGuid());
    }

    [Fact]
    public async Task AListTheCallerCannotSeeIsNeverPlaced()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var stranger = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = (await owner.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var strangerId = (await stranger.GetFromJsonAsync<MeResponse>("/api/me", ct))!.UserId;
        var ownerArea = Guid.NewGuid();
        var strangerArea = Guid.NewGuid();
        var listId = Guid.NewGuid();
        await Send(owner, ct,
            new CreateArea(Guid.NewGuid(), ownerId, ownerArea, "Dom", 0),
            new CreateTaskList(Guid.NewGuid(), ownerId, listId, ownerArea, "Zakupy"));
        await Send(stranger, ct, new CreateArea(Guid.NewGuid(), strangerId, strangerArea, "Praca", 0));

        var result = Assert.Single(await Results(stranger, ct, new PlaceList(Guid.NewGuid(), strangerId, listId, strangerArea)));

        Assert.False(result.Accepted);
        var sync = await stranger.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        Assert.Empty(sync!.Documents["listviews"]);
    }

    static async Task Send(HttpClient client, CancellationToken ct, params object[] commands) =>
        Assert.All(await Results(client, ct, commands), result => Assert.True(result.Accepted, result.Rejection));

    static async Task<CommandResponse[]> Results(HttpClient client, CancellationToken ct, params object[] commands)
    {
        var envelopes = commands
            .Select(command => new CommandEnvelope(
                command.GetType().Name, JsonSerializer.SerializeToElement(command, command.GetType())))
            .ToArray();
        var response = await client.PostAsJsonAsync("/api/commands", envelopes, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct))!;
    }
}
