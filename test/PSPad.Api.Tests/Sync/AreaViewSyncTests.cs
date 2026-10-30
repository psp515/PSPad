using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Contracts;
using PSPad.Module.Presentation.AreaViews;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;
using static PSPad.Api.Tests.Commands.Commands;

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

        await SendAsync(client, ct,
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
        await SendAsync(client, ct, new CreateArea(Guid.NewGuid(), user, areaId, "Dom", 0));

        await SendAsync(client, ct, reorder);
        await SendAsync(client, ct, reorder);

        var context = Persistence.TestContext.For(fixture);
        var events = await context.Collection<BsonDocument>("events")
            .Find(Builders<BsonDocument>.Filter.Eq("aggregateId", AreaView.IdFor(user, areaId)))
            .CountDocumentsAsync(ct);
        Assert.Equal(1, events);
    }

    [Fact]
    public async Task ReorderingAnExistingViewMovesTheListInPlace()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var user = await SignInAsync(client, ct);
        var areaId = Guid.NewGuid();
        Guid[] lists = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];
        await SendAsync(client, ct,
            new CreateArea(Guid.NewGuid(), user, areaId, "Dom", 0),
            new CreateTaskList(Guid.NewGuid(), user, lists[0], areaId, "Zakupy"),
            new CreateTaskList(Guid.NewGuid(), user, lists[1], areaId, "Remont"),
            new CreateTaskList(Guid.NewGuid(), user, lists[2], areaId, "Ogród"),
            new ReorderLists(Guid.NewGuid(), user, areaId, lists, lists[2], 0));

        await SendAsync(client, ct, new ReorderLists(Guid.NewGuid(), user, areaId, [lists[2], lists[0], lists[1]], lists[2], 2));

        var row = Assert.Single(await SyncedAsync(client, "areaviews", ct));
        Assert.Equal(
            [lists[0], lists[1], lists[2]],
            row.GetProperty("order").EnumerateArray().Select(id => id.GetGuid()).ToArray());
    }

    [Fact]
    public async Task AnAreaTheUserDoesNotOwnIsRejectedAndNoViewIsStored()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await SignInAsync(owner, ct);
        var stranger = factory.ClientFor(Guid.NewGuid().ToString());
        var strangerId = await SignInAsync(stranger, ct);
        var areaId = Guid.NewGuid();
        var list = Guid.NewGuid();
        await SendAsync(owner, ct,
            new CreateArea(Guid.NewGuid(), ownerId, areaId, "Dom", 0),
            new CreateTaskList(Guid.NewGuid(), ownerId, list, areaId, "Zakupy"));

        var result = Assert.Single(await TrySendAsync(stranger, ct,
            new ReorderLists(Guid.NewGuid(), strangerId, areaId, [list], list, 0)));

        Assert.False(result.Accepted);
        Assert.Empty(await SyncedAsync(stranger, "areaviews", ct));
    }
}
