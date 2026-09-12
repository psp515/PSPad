using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Endpoints;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class SyncEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task SyncingFromZeroReturnsEverythingTheUserHas()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(user);
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(
                new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0)))
        }, ct);

        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        Assert.True(sync!.Marker > 0);
        Assert.Single(sync.Documents["areas"]);
        Assert.Single(sync.Events);
    }

    [Fact]
    public async Task SyncingFromTheLastMarkerReturnsOnlyWhatChangedSince()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var user = Guid.NewGuid();
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(user);
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(
                new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0)))
        }, ct);
        var first = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        var second = await client.GetFromJsonAsync<SyncResponse>($"/api/sync?since={first!.Marker}", ct);

        Assert.Empty(second!.Events);
        Assert.Equal(first.Marker, second.Marker);
    }

    [Fact]
    public async Task SyncNeverLeaksAnotherUsersDocuments()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        await using var factory = new ApiFactory(fixture);
        await factory.ClientFor(theirs).PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(
                new CreateArea(Guid.NewGuid(), theirs, Guid.NewGuid(), "Theirs", 0)))
        }, ct);

        var sync = await factory.ClientFor(mine).GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        Assert.Empty(sync!.Events);
        Assert.False(sync.Documents.TryGetValue("areas", out var areas) && areas.Length > 0);
    }
}
