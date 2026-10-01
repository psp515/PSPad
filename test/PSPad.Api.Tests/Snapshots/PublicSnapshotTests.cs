using System.Net;
using System.Net.Http.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Snapshots;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class PublicSnapshotTests(MongoFixture fixture)
{
    [Fact]
    public async Task AnyoneReadsTheSnapshotWithoutSigningIn()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var stepId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct,
            new CreateArea(Guid.NewGuid(), ownerId, areaId, "Home", 0),
            new CreateTaskList(Guid.NewGuid(), ownerId, listId, areaId, "Groceries", ListKind.Tasks),
            new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Buy milk"),
            new SetTaskDescription(Guid.NewGuid(), ownerId, taskId, "Whole milk, two liters"),
            new AddStep(Guid.NewGuid(), ownerId, taskId, stepId, "Go to the store"));

        var published = await PublishAsync(owner, listId, ct);

        var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/public/snapshots/{published.Token}", ct);
        response.EnsureSuccessStatusCode();

        var view = await response.Content.ReadFromJsonAsync<SnapshotView>(ct);
        var task = Assert.Single(view!.Tasks);
        Assert.Equal("Buy milk", task.Name);
        Assert.False(task.Done);
        Assert.Equal("Whole milk, two liters", task.Description);
        var step = Assert.Single(task.Steps);
        Assert.Equal("Go to the store", step.Name);
        Assert.False(step.Done);
    }

    [Fact]
    public async Task TheSnapshotIsFrozen()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct,
            new CreateArea(Guid.NewGuid(), ownerId, areaId, "Home", 0),
            new CreateTaskList(Guid.NewGuid(), ownerId, listId, areaId, "Groceries", ListKind.Tasks),
            new CreateTask(Guid.NewGuid(), ownerId, taskId, listId, "Buy milk"));

        var published = await PublishAsync(owner, listId, ct);

        await Sharing.SendAsync(owner, ct, new RenameTask(Guid.NewGuid(), ownerId, taskId, "Buy oat milk"));

        var anonymous = factory.CreateClient();
        var response = await anonymous.GetFromJsonAsync<SnapshotView>(
            $"/api/public/snapshots/{published.Token}", ct);
        var task = Assert.Single(response!.Tasks);
        Assert.Equal("Buy milk", task.Name);
    }

    [Fact]
    public async Task AnExpiredSnapshotIs404()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var listId = await CreatePlainListAsync(owner, ownerId, ct);
        var published = await PublishAsync(owner, listId, ct);

        var context = Persistence.TestContext.For(fixture);
        await context.Collection<BsonDocument>("list_snapshots").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", published.Id),
            Builders<BsonDocument>.Update.Set("expiresAt", DateTime.UtcNow.AddDays(-1)),
            cancellationToken: ct);

        var anonymous = factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/public/snapshots/{published.Token}", ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownTokenIs404()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var anonymous = factory.CreateClient();

        var response = await anonymous.GetAsync($"/api/public/snapshots/{Sharing.FreshToken()}", ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TooManyRequestsAre429()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(
            fixture, configuration: new Dictionary<string, string?> { ["Sharing:PublicRequestsPerMinute"] = "2" });
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var listId = await CreatePlainListAsync(owner, ownerId, ct);
        var published = await PublishAsync(owner, listId, ct);

        var anonymous = factory.CreateClient();
        await anonymous.GetAsync($"/api/public/snapshots/{published.Token}", ct);
        await anonymous.GetAsync($"/api/public/snapshots/{published.Token}", ct);
        var third = await anonymous.GetAsync($"/api/public/snapshots/{published.Token}", ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
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
