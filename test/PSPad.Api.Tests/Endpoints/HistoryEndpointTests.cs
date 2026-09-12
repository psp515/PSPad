using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class HistoryEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task HistoryComesBackNewestFirst()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var areaId = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CreateArea(Guid.NewGuid(), me!.UserId, areaId, "Home", 0)),
            Envelope(new RenameArea(Guid.NewGuid(), me.UserId, areaId, "House"))
        }, ct);

        var history = await client.GetFromJsonAsync<HistoryEntry[]>("/api/history", ct);

        Assert.Equal("Renamed an area", history![0].Description);
        Assert.True(history[0].Seq > history[1].Seq);
    }

    [Fact]
    public async Task ProvisioningShowsUpInHistory()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        await client.GetFromJsonAsync<MeResponse>("/api/me", ct);

        var history = await client.GetFromJsonAsync<HistoryEntry[]>("/api/history", ct);

        Assert.Contains(history!, entry => entry.Description == "Signed in for the first time");
    }

    [Fact]
    public async Task PagingWithBeforeWalksBackwards()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var areaId = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CreateArea(Guid.NewGuid(), me!.UserId, areaId, "Home", 0)),
            Envelope(new RenameArea(Guid.NewGuid(), me.UserId, areaId, "House"))
        }, ct);
        var first = await client.GetFromJsonAsync<HistoryEntry[]>("/api/history?limit=1", ct);

        var next = await client.GetFromJsonAsync<HistoryEntry[]>(
            $"/api/history?limit=1&before={first![0].Seq}", ct);

        Assert.True(next![0].Seq < first[0].Seq);
    }

    [Fact]
    public async Task HistoryNeverShowsAnotherUsersEvents()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var theirs = factory.ClientFor(Guid.NewGuid().ToString());
        var them = await theirs.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var theirAreaId = Guid.NewGuid();
        await theirs.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CreateArea(Guid.NewGuid(), them!.UserId, theirAreaId, "Theirs", 0))
        }, ct);

        var mine = factory.ClientFor(Guid.NewGuid().ToString());
        await mine.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var history = await mine.GetFromJsonAsync<HistoryEntry[]>("/api/history", ct);

        Assert.DoesNotContain(history!, entry => entry.AggregateId == theirAreaId);
    }

    static CommandEnvelope Envelope<T>(T command) where T : notnull =>
        new(typeof(T).Name, JsonSerializer.SerializeToElement(command));
}
