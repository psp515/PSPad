using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PSPad.Api.Snapshots;
using PSPad.Api.Tests.Sync;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Sharing.Snapshots;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Snapshots;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class SnapshotMarkTests(MongoFixture fixture)
{
    [Fact]
    public async Task AVisitorsTickShowsToOtherVisitors()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, listId, taskId, _) = await SeedTaskAsync(factory, ct);
        var published = await PublishAsync(owner, listId, ct);

        var anonymous = factory.CreateClient();
        var markResponse = await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{published.Token}/marks",
            new MarkSnapshotEntryRequest(taskId, null, true), ct);
        Assert.Equal(HttpStatusCode.NoContent, markResponse.StatusCode);

        var view = await anonymous.GetFromJsonAsync<SnapshotView>($"/api/public/snapshots/{published.Token}", ct);
        var task = Assert.Single(view!.Tasks);
        Assert.True(task.Marked);
    }

    [Fact]
    public async Task AVisitorsTickReachesTheOwnerAsAChip()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, listId, taskId, _) = await SeedTaskAsync(factory, ct);
        var published = await PublishAsync(owner, listId, ct);

        var anonymous = factory.CreateClient();
        (await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{published.Token}/marks",
            new MarkSnapshotEntryRequest(taskId, null, true), ct)).EnsureSuccessStatusCode();

        var sync = await owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var taskRow = Assert.Single(sync!.Documents["todotasks"], row => row.GetProperty("id").GetGuid() == taskId);
        var marks = taskRow.GetProperty("_snapshotMarks").EnumerateArray().ToArray();
        Assert.Single(marks);
        Assert.False(taskRow.TryGetProperty("completedAt", out var completedAt) && completedAt.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task UntickingRemovesItFromBoth()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, listId, taskId, _) = await SeedTaskAsync(factory, ct);
        var published = await PublishAsync(owner, listId, ct);

        var anonymous = factory.CreateClient();
        (await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{published.Token}/marks",
            new MarkSnapshotEntryRequest(taskId, null, true), ct)).EnsureSuccessStatusCode();
        (await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{published.Token}/marks",
            new MarkSnapshotEntryRequest(taskId, null, false), ct)).EnsureSuccessStatusCode();

        var view = await anonymous.GetFromJsonAsync<SnapshotView>($"/api/public/snapshots/{published.Token}", ct);
        Assert.False(Assert.Single(view!.Tasks).Marked);

        var sync = await owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var taskRow = Assert.Single(sync!.Documents["todotasks"], row => row.GetProperty("id").GetGuid() == taskId);
        Assert.Empty(taskRow.GetProperty("_snapshotMarks").EnumerateArray());
    }

    [Fact]
    public async Task AStepTickCarriesTheStep()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, listId, taskId, stepId) = await SeedTaskAsync(factory, ct);
        var published = await PublishAsync(owner, listId, ct);

        var anonymous = factory.CreateClient();
        (await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{published.Token}/marks",
            new MarkSnapshotEntryRequest(taskId, stepId, true), ct)).EnsureSuccessStatusCode();

        var view = await anonymous.GetFromJsonAsync<SnapshotView>($"/api/public/snapshots/{published.Token}", ct);
        var task = Assert.Single(view!.Tasks);
        Assert.False(task.Marked);
        var step = Assert.Single(task.Steps);
        Assert.True(step.Marked);
    }

    [Fact]
    public async Task ADeletedTaskKeepsTheSnapshotTick()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, listId, taskId, _) = await SeedTaskAsync(factory, ct);
        var published = await PublishAsync(owner, listId, ct);

        await Sharing.SendAsync(owner, ct, new DeleteTask(Guid.NewGuid(), ownerId, taskId));

        var anonymous = factory.CreateClient();
        var markResponse = await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{published.Token}/marks",
            new MarkSnapshotEntryRequest(taskId, null, true), ct);
        Assert.Equal(HttpStatusCode.NoContent, markResponse.StatusCode);

        var view = await anonymous.GetFromJsonAsync<SnapshotView>($"/api/public/snapshots/{published.Token}", ct);
        Assert.True(Assert.Single(view!.Tasks).Marked);
    }

    [Fact]
    public async Task MarkCommandsAreRefusedFromClients()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, listId, taskId, _) = await SeedTaskAsync(factory, ct);
        var published = await PublishAsync(owner, listId, ct);

        var command = new MarkTaskFromSnapshot(Guid.NewGuid(), ownerId, taskId, null, published.Id, true);
        var envelope = new CommandEnvelope(nameof(MarkTaskFromSnapshot), JsonSerializer.SerializeToElement(command));

        var response = await owner.PostAsJsonAsync("/api/commands", new[] { envelope }, ct);
        response.EnsureSuccessStatusCode();
        var results = await response.Content.ReadFromJsonAsync<CommandResponse[]>(ct);
        var result = Assert.Single(results!);
        Assert.False(result.Accepted);
        Assert.True(result.Unrecoverable);
    }

    [Fact]
    public async Task MarkingAnExpiredSnapshotIs404()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, listId, taskId, _) = await SeedTaskAsync(factory, ct);
        var published = await PublishAsync(owner, listId, ct);

        var context = Persistence.TestContext.For(fixture);
        await context.Collection<BsonDocument>("list_snapshots").UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", published.Id),
            Builders<BsonDocument>.Update.Set("expiresAt", DateTime.UtcNow.AddDays(-1)),
            cancellationToken: ct);

        var anonymous = factory.CreateClient();
        var markResponse = await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{published.Token}/marks",
            new MarkSnapshotEntryRequest(taskId, null, true), ct);

        Assert.Equal(HttpStatusCode.NotFound, markResponse.StatusCode);
    }

    [Fact]
    public async Task VisitorMarksLeaveNoRowInTheEventLog()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, listId, taskId, _) = await SeedTaskAsync(factory, ct);
        var published = await PublishAsync(owner, listId, ct);

        var anonymous = factory.CreateClient();
        foreach (var marked in new[] { true, false, true })
        {
            (await anonymous.PostAsJsonAsync(
                $"/api/public/snapshots/{published.Token}/marks",
                new MarkSnapshotEntryRequest(taskId, null, marked), ct)).EnsureSuccessStatusCode();
        }

        var context = Persistence.TestContext.For(fixture);
        var markEvents = await context.Collection<StoredEvent>("events").CountDocumentsAsync(
            Builders<StoredEvent>.Filter.Eq(entry => entry.AggregateId, taskId) &
            Builders<StoredEvent>.Filter.Eq(entry => entry.Type, nameof(TaskSnapshotMarkSet)),
            cancellationToken: ct);
        Assert.Equal(0, markEvents);
    }

    [Fact]
    public async Task MarkingAListWhoseDailyBudgetIsSpentIs429()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, listId, taskId, _) = await SeedTaskAsync(factory, ct);
        var published = await PublishAsync(owner, listId, ct);

        var budget = new MongoMarkBudget(Persistence.TestContext.For(fixture));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        while (await budget.TryTakeAsync(listId, today, SnapshotMarking.DailyMarksPerList, ct))
        {
        }

        var anonymous = factory.CreateClient();
        var markResponse = await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{published.Token}/marks",
            new MarkSnapshotEntryRequest(taskId, null, true), ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, markResponse.StatusCode);
        var view = await anonymous.GetFromJsonAsync<SnapshotView>($"/api/public/snapshots/{published.Token}", ct);
        Assert.False(Assert.Single(view!.Tasks).Marked);
    }

    [Fact]
    public async Task MarkingWithAnUnknownTokenIs404()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var anonymous = factory.CreateClient();

        var markResponse = await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{Sharing.FreshToken()}/marks",
            new MarkSnapshotEntryRequest(Guid.NewGuid(), null, true), ct);

        Assert.Equal(HttpStatusCode.NotFound, markResponse.StatusCode);
    }

    [Fact]
    public async Task MarkingAnEntryTheSnapshotDoesNotHoldIs404()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, listId, taskId, _) = await SeedTaskAsync(factory, ct);
        var published = await PublishAsync(owner, listId, ct);
        var laterTask = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct, new CreateTask(Guid.NewGuid(), ownerId, laterTask, listId, "Added later"));

        var anonymous = factory.CreateClient();
        var unknown = await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{published.Token}/marks",
            new MarkSnapshotEntryRequest(Guid.NewGuid(), null, true), ct);
        var postDated = await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{published.Token}/marks",
            new MarkSnapshotEntryRequest(laterTask, null, true), ct);
        var unknownStep = await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{published.Token}/marks",
            new MarkSnapshotEntryRequest(taskId, Guid.NewGuid(), true), ct);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, postDated.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownStep.StatusCode);
    }

    [Fact]
    public async Task RepeatingATickChangesNothingAndNeedsNoBudget()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var (owner, ownerId, listId, taskId, _) = await SeedTaskAsync(factory, ct);
        var published = await PublishAsync(owner, listId, ct);
        var anonymous = factory.CreateClient();
        (await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{published.Token}/marks",
            new MarkSnapshotEntryRequest(taskId, null, true), ct)).EnsureSuccessStatusCode();
        var budget = new MongoMarkBudget(Persistence.TestContext.For(fixture));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        while (await budget.TryTakeAsync(listId, today, SnapshotMarking.DailyMarksPerList, ct))
        {
        }

        var repeated = await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{published.Token}/marks",
            new MarkSnapshotEntryRequest(taskId, null, true), ct);
        var untick = await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{published.Token}/marks",
            new MarkSnapshotEntryRequest(taskId, null, false), ct);

        Assert.Equal(HttpStatusCode.NoContent, repeated.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, untick.StatusCode);
    }

    [Fact]
    public async Task AVisitorsTickOnAReferenceItemShowsToVisitorsAndReachesTheOwnersItem()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var owner = factory.ClientFor(Guid.NewGuid().ToString());
        var ownerId = await Sharing.SignInAsync(owner, ct);
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await Sharing.SendAsync(owner, ct,
            new CreateArea(Guid.NewGuid(), ownerId, areaId, "Home", 0),
            new CreateTaskList(Guid.NewGuid(), ownerId, listId, areaId, "Filaments", ListKind.Reference),
            new CreateReferenceItem(Guid.NewGuid(), ownerId, itemId, listId, "PLA white", 0));
        var published = await PublishAsync(owner, listId, ct);

        var anonymous = factory.CreateClient();
        var markResponse = await anonymous.PostAsJsonAsync(
            $"/api/public/snapshots/{published.Token}/marks",
            new MarkSnapshotEntryRequest(itemId, null, true), ct);

        Assert.Equal(HttpStatusCode.NoContent, markResponse.StatusCode);
        var view = await anonymous.GetFromJsonAsync<SnapshotView>($"/api/public/snapshots/{published.Token}", ct);
        Assert.True(Assert.Single(view!.Items).Marked);
        var sync = await owner.GetFromJsonAsync<SyncResponse>("/api/sync?since=0", ct);
        var itemRow = Assert.Single(sync!.Documents["referenceitems"], row => row.GetProperty("id").GetGuid() == itemId);
        Assert.Single(itemRow.GetProperty("_snapshotMarks").EnumerateArray());
    }

    static async Task<PublishedSnapshotView> PublishAsync(HttpClient owner, Guid listId, CancellationToken ct)
    {
        var response = await owner.PostAsJsonAsync(
            $"/api/lists/{listId}/snapshots", new PublishSnapshotRequest(DateTimeOffset.UtcNow.AddDays(1)), ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PublishedSnapshotView>(ct))!;
    }

    static async Task<(HttpClient Owner, Guid OwnerId, Guid ListId, Guid TaskId, Guid StepId)> SeedTaskAsync(
        ApiFactory factory, CancellationToken ct)
    {
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
            new AddStep(Guid.NewGuid(), ownerId, taskId, stepId, "Go to the store"));
        return (owner, ownerId, listId, taskId, stepId);
    }
}
