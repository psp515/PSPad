using System.Net.Http.Json;
using System.Text.Json;
using PSPad.Contracts;
using PSPad.Module.Identity.Provisioning;
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
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(
                new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0)))
        }, ct);

        var sync = await client.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        Assert.True(sync!.Marker > 0);
        Assert.Equal(ProvisioningPlan.SeedAreaNames.Count + 1, sync.Documents["areas"].Length);
        Assert.NotEmpty(sync.Events);
    }

    [Fact]
    public async Task SyncingFromTheLastMarkerReturnsOnlyWhatChangedSince()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
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
        await using var factory = new ApiFactory(fixture);
        var theirsClient = factory.ClientFor(Guid.NewGuid().ToString());
        var theirsMe = await theirsClient.GetFromJsonAsync<MeResponse>("/api/me", ct);
        await theirsClient.PostAsJsonAsync("/api/commands", new[]
        {
            new CommandEnvelope(nameof(CreateArea), JsonSerializer.SerializeToElement(
                new CreateArea(Guid.NewGuid(), theirsMe!.UserId, Guid.NewGuid(), "Theirs", 0)))
        }, ct);

        var mineClient = factory.ClientFor(Guid.NewGuid().ToString());
        var sync = await mineClient.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);

        Assert.Empty(sync!.Events);
        Assert.False(sync.Documents.TryGetValue("areas", out var areas) && areas.Length > 0);
    }
}
