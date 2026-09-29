using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Sync;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class ReferenceSyncTests(MongoFixture fixture)
{
    [Fact]
    public async Task ASyncPullReturnsReferenceItemsChangedSinceTheMarker()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        await Send(client, ct,
            new CreateArea(Guid.NewGuid(), user, areaId, "Print shop", 0),
            new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Filaments", 0, ListKind.Reference),
            new CreateReferenceItem(Guid.NewGuid(), user, itemId, listId, "PLA Black", 0));

        var first = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        Assert.Contains(first!.Documents["referenceitems"], row => row.GetProperty("id").GetGuid() == itemId);

        await Send(client, ct,
            new AddReferenceField(Guid.NewGuid(), user, itemId, Guid.NewGuid(), "Colour", "Black", null));

        var second = await client.GetFromJsonAsync<SyncResponse>($"/api/sync?since={first.Marker}", ct);
        var row = Assert.Single(second!.Documents["referenceitems"]);
        Assert.Equal(itemId, row.GetProperty("id").GetGuid());
        Assert.Single(row.GetProperty("fields").EnumerateArray());
    }

    [Fact]
    public async Task AnotherUsersItemsNeverSync()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var theirsClient = factory.ClientFor(Guid.NewGuid().ToString());
        var theirsMe = await theirsClient.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var theirsUser = theirsMe!.UserId;
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        await Send(theirsClient, ct,
            new CreateArea(Guid.NewGuid(), theirsUser, areaId, "Print shop", 0),
            new CreateTaskList(Guid.NewGuid(), theirsUser, listId, areaId, "Filaments", 0, ListKind.Reference),
            new CreateReferenceItem(Guid.NewGuid(), theirsUser, Guid.NewGuid(), listId, "PLA Black", 0));

        var theirsSync = await theirsClient.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        Assert.True(
            theirsSync!.Documents.TryGetValue("referenceitems", out var theirsItems) && theirsItems.Length > 0);

        var mineClient = factory.ClientFor(Guid.NewGuid().ToString());
        var sync = await mineClient.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        Assert.False(
            sync!.Documents.TryGetValue("referenceitems", out var items) && items.Length > 0);
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
