using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PSPad.Api.Statistics;
using PSPad.Contracts;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Statistics;
using PSPad.Module.Tasks.Areas;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Statistics;

[IntegrationTest]
[Collection(MongoCollection.Name)]
public class MongoTaskSnapshotSourceTests(MongoFixture fixture)
{
    [Fact]
    public async Task AnOpenTaskComesBackOpenWithItsName()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var taskId = await SeedTask(client, user, "Buy milk", ct);
        var source = SourceFor(factory);

        var snapshots = await source.CurrentAsync([taskId], ct);

        Assert.Equal(new TaskSnapshot(PSPad.Module.Statistics.TaskStatus.Open, "Buy milk"), snapshots[taskId]);
    }

    [Fact]
    public async Task ACompletedTaskComesBackDone()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var taskId = await SeedTask(client, user, "Buy milk", ct);
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CompleteTask(Guid.NewGuid(), user, taskId))
        }, ct);
        var source = SourceFor(factory);

        var snapshots = await source.CurrentAsync([taskId], ct);

        Assert.Equal(new TaskSnapshot(PSPad.Module.Statistics.TaskStatus.Done, "Buy milk"), snapshots[taskId]);
    }

    [Fact]
    public async Task AnIdWithNoDocumentComesBackGoneWithAnEmptyName()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var source = SourceFor(factory);
        var missingId = Guid.NewGuid();

        var snapshots = await source.CurrentAsync([missingId], ct);

        Assert.Equal(new TaskSnapshot(PSPad.Module.Statistics.TaskStatus.Gone, ""), snapshots[missingId]);
    }

    [Fact]
    public async Task ADeletedTaskComesBackGoneRatherThanOpen()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var taskId = await SeedTask(client, user, "Buy milk", ct);
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new DeleteTask(Guid.NewGuid(), user, taskId))
        }, ct);
        var source = SourceFor(factory);

        var snapshots = await source.CurrentAsync([taskId], ct);

        Assert.Equal(PSPad.Module.Statistics.TaskStatus.Gone, snapshots[taskId].Status);
    }

    [Fact]
    public async Task ASingleCallReturnsAnEntryForEveryRequestedId()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        await using var factory = new ApiFactory(fixture);
        var client = factory.ClientFor(Guid.NewGuid().ToString());
        var me = await client.GetFromJsonAsync<MeResponse>("/api/me", ct);
        var user = me!.UserId;
        var openId = await SeedTask(client, user, "Open one", ct);
        var doneId = await SeedTask(client, user, "Done one", ct);
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CompleteTask(Guid.NewGuid(), user, doneId))
        }, ct);
        var missingId = Guid.NewGuid();
        var source = SourceFor(factory);

        var snapshots = await source.CurrentAsync([openId, doneId, missingId], ct);

        Assert.Equal(3, snapshots.Count);
        Assert.Equal(PSPad.Module.Statistics.TaskStatus.Open, snapshots[openId].Status);
        Assert.Equal(PSPad.Module.Statistics.TaskStatus.Done, snapshots[doneId].Status);
        Assert.Equal(PSPad.Module.Statistics.TaskStatus.Gone, snapshots[missingId].Status);
    }

    static ITaskSnapshotSource SourceFor(ApiFactory factory) =>
        new MongoTaskSnapshotSource(factory.Services.GetRequiredService<MongoContext>());

    static async Task<Guid> SeedTask(HttpClient client, Guid user, string name, CancellationToken ct)
    {
        var areaId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await client.PostAsJsonAsync("/api/commands", new[]
        {
            Envelope(new CreateArea(Guid.NewGuid(), user, areaId, "Home", 0)),
            Envelope(new CreateTaskList(Guid.NewGuid(), user, listId, areaId, "Errands", 0)),
            Envelope(new CreateTask(Guid.NewGuid(), user, taskId, listId, name))
        }, ct);
        return taskId;
    }

    static CommandEnvelope Envelope<T>(T command) where T : notnull =>
        new(typeof(T).Name, JsonSerializer.SerializeToElement(command));
}
