using System.Net;
using System.Net.Http.Json;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Snapshots;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class SnapshotEndpointTests(MongoFixture fixture)
{
    [Fact]
    public async Task TheOwnerPublishesAndListsASnapshot()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var listId = await CreatePlainListAsync(owner, ownerId, ct);

        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);
        var response = await owner.PostAsJsonAsync(
            $"/api/lists/{listId}/snapshots", new PublishSnapshotRequest(expiresAt), ct);

        response.EnsureSuccessStatusCode();
        var published = await response.Content.ReadFromJsonAsync<PublishedSnapshotView>(ct);
        Assert.Equal(24, published!.Token.Length);

        var list = await owner.GetFromJsonAsync<PublishedSnapshotView[]>($"/api/lists/{listId}/snapshots", ct);
        var listed = Assert.Single(list!, snapshot => snapshot.Token == published.Token);
        Assert.Equal(0, listed.Ticks);
        Assert.Equal(0, listed.Entries);
    }

    [Fact]
    public async Task AMemberCannotPublish()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var member = factory.ClientFor(Guid.NewGuid().ToString());
        await Sharing.SignInAsync(member, ct);

        var token = Sharing.FreshToken();
        var listId = await Sharing.SharedListAsync(owner, ownerId, ct, token: token);
        await Sharing.JoinAsync(member, ct, token);

        var response = await member.PostAsJsonAsync(
            $"/api/lists/{listId}/snapshots", new PublishSnapshotRequest(DateTimeOffset.UtcNow.AddDays(1)), ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnExpiryBeyondAYearIsRefused()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var listId = await CreatePlainListAsync(owner, ownerId, ct);

        var response = await owner.PostAsJsonAsync(
            $"/api/lists/{listId}/snapshots",
            new PublishSnapshotRequest(DateTimeOffset.UtcNow.AddDays(366)), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnExpiryInThePastIsRefused()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var listId = await CreatePlainListAsync(owner, ownerId, ct);

        var response = await owner.PostAsJsonAsync(
            $"/api/lists/{listId}/snapshots",
            new PublishSnapshotRequest(DateTimeOffset.UtcNow.AddDays(-1)), ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RevokingRemovesTheSnapshot()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var listId = await CreatePlainListAsync(owner, ownerId, ct);

        var published = await PublishAsync(owner, listId, ct);

        var deleteResponse = await owner.DeleteAsync($"/api/snapshots/{published.Id}", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var anonymous = factory.CreateClient();
        var publicResponse = await anonymous.GetAsync($"/api/public/snapshots/{published.Token}", ct);
        Assert.Equal(HttpStatusCode.NotFound, publicResponse.StatusCode);
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
