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
