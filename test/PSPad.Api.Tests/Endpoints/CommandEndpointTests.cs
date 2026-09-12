using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class CommandEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task AnAcceptedCommandComesBackAcceptedAndIsStored()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var command = new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0);

        var response = await client.PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(command))
        }, ct);

        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct);
        Assert.True(Assert.Single(results!).Accepted);
    }

    [Fact]
    public async Task ABatchIsAppliedInOrderAndReportsEachResult()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var areaId = Guid.NewGuid();
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;

        var response = await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0)),
            Envelope(new RenameArea(Guid.NewGuid(), user, areaId, "House")),
            Envelope(new RenameArea(Guid.NewGuid(), user, Guid.NewGuid(), "Nowhere"))
        }, ct);

        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct);

        Assert.True(results![0].Accepted);
        Assert.True(results[1].Accepted);
        Assert.False(results[2].Accepted);
        Assert.NotNull(results[2].Rejection);
    }

    [Fact]
    public async Task ReplayingTheSameBatchChangesNothing()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var baseline = await client.GetFromJsonAsync<JsonElement>("/api/sync?since=0", ct);
        var marker = baseline.GetProperty("marker").GetInt64();
        var batch = new[] { Envelope(new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0)) };

        await client.PostAsJsonAsync("/api/commands", batch, ct);
        var replay = await client.PostAsJsonAsync("/api/commands", batch, ct);

        var results = await replay.Content.ReadFromJsonAsync<CommandResponse[]>(ct);
        Assert.True(Assert.Single(results!).Accepted);

        var sync = await client.GetFromJsonAsync<JsonElement>($"/api/sync?since={marker}", ct);
        Assert.Equal(1, sync.GetProperty("events").GetArrayLength());
    }

    static CommandEnvelope Envelope<T>(T command) where T : notnull =>
        new(typeof(T).Name, JsonSerializer.SerializeToElement(command));
}
