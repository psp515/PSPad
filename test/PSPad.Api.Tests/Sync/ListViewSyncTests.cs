using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Presentation.ListViews;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;

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

        await SendAsync(client, ct,
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
        await SendAsync(owner, ct,
            new CreateArea(Guid.NewGuid(), ownerId, ownerArea, "Dom", 0),
            new CreateTaskList(Guid.NewGuid(), ownerId, listId, ownerArea, "Zakupy"));
        await SendAsync(stranger, ct, new CreateArea(Guid.NewGuid(), strangerId, strangerArea, "Praca", 0));

        var result = Assert.Single(await TrySendAsync(stranger, ct, new PlaceList(Guid.NewGuid(), strangerId, listId, strangerArea)));

        Assert.False(result.Accepted);
        var sync = await stranger.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        Assert.Empty(sync!.Documents["listviews"]);
    }

    [Fact]
    public async Task APlacedListCanBeUnfiled()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        await SendAsync(client, ct,
            new CreateArea(Guid.NewGuid(), user, areaId, "Dom", 0),
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Zakupy"),
            new PlaceList(Guid.NewGuid(), user, listId, areaId));

        await SendAsync(client, ct, new PlaceList(Guid.NewGuid(), user, listId, null));

        var row = Assert.Single(await SyncedAsync(client, "listviews", ct));
        Assert.Equal(JsonValueKind.Null, row.GetProperty("areaId").ValueKind);
    }

    [Fact]
    public async Task AnAreaTheUserDoesNotOwnIsRejectedAndNoPlacementIsStored()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await SignInAsync(owner, ct);
        var ownerArea = Guid.NewGuid();
        var listId = Guid.NewGuid();
        await SendAsync(owner, ct,
            new CreateArea(Guid.NewGuid(), ownerId, ownerArea, "Dom", 0),
            new CreateTaskList(Guid.NewGuid(), ownerId, listId, ownerArea, "Zakupy"));

        var result = Assert.Single(await TrySendAsync(owner, ct, new PlaceList(Guid.NewGuid(), ownerId, listId, Guid.NewGuid())));

        Assert.False(result.Accepted);
        Assert.Empty(await SyncedAsync(owner, "listviews", ct));
    }
}
