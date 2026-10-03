using System.Net.Http.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Snapshots;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class SnapshotVisitTests(MongoFixture fixture)
{
    [Fact]
    public async Task ASignedInVisitIsListed()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var listId = await CreatePlainListAsync(owner, ownerId, ct);
        var published = await PublishAsync(owner, listId, ct);

        var visitor = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(visitor, ct);

        (await visitor.PostAsJsonAsync("/api/me/snapshot-visits", new RecordVisitRequest(published.Token), ct))
            .EnsureSuccessStatusCode();

        var visits = await visitor.GetFromJsonAsync<SnapshotVisitView[]>("/api/me/snapshot-visits", ct);
        var visit = Assert.Single(visits!);
        Assert.Equal(published.Token, visit.Token);
    }

    [Fact]
    public async Task ExpiredVisitsDropOut()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var listId = await CreatePlainListAsync(owner, ownerId, ct);
        var published = await PublishAsync(owner, listId, ct);

        var visitor = factory.ClientFor(Guid.NewGuid().ToString());
        var visitorId = await Sharing.SignInAsync(visitor, ct);

        (await visitor.PostAsJsonAsync("/api/me/snapshot-visits", new RecordVisitRequest(published.Token), ct))
            .EnsureSuccessStatusCode();

        var context = Persistence.TestContext.For(fixture);
        await context.Collection<BsonDocument>("snapshot_visits").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("userId", visitorId),
            Builders<BsonDocument>.Update.Set("expiresAt", DateTime.UtcNow.AddDays(-1)),
            cancellationToken: ct);

        var visits = await visitor.GetFromJsonAsync<SnapshotVisitView[]>("/api/me/snapshot-visits", ct);
        Assert.Empty(visits!);
    }

    static async Task<PublishedSnapshotView> PublishAsync(HttpClient owner, Guid listId, CancellationToken ct)
    {
        var response = await owner.PostAsJsonAsync(
            $"/api/lists/{listId}/snapshots", new PublishSnapshotRequest(DateTimeOffset.UtcNow.AddDays(1)), ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PublishedSnapshotView>(ct))!;
    }

    static async Task<Guid> CreatePlainListAsync(HttpClient owner, Guid ownerId, CancellationToken ct)
    {
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct,
            new CreateArea(Guid.NewGuid(), ownerId, areaId, "Home", 0),
            new CreateTaskList(Guid.NewGuid(), ownerId, listId, areaId, "Groceries", ListKind.Tasks));
        return listId;
    }
}
