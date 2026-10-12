using System.Text.Json;
using Microsoft.JSInterop;
using PSPad.App.State;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.Statistics;
using PSPad.App.Sync;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.Sync;

[UnitTest]
public class DatasetResetTests
{
    [Fact]
    public async Task ResettingEmptiesTheReplicaButKeepsTheOwnerAndTheOutbox()
    {
        var user = Guid.NewGuid();
        var replica = new InMemoryReplica();
        await replica.SetOwnerAsync(user);
        await replica.SetMarkerAsync(40);
        var area = new Area();
        area.ApplyAll(Area.Decide(
            null, new CreateArea(Guid.NewGuid(), user, Guid.NewGuid(), "Home", 0), DateTimeOffset.UnixEpoch));
        await replica.SaveAsync(area);
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), new CommandEnvelope(
            nameof(CreateArea), JsonSerializer.SerializeToElement(new { Name = "Work" })));
        var dataset = Guid.NewGuid();

        await new DatasetReset(replica).ResetAsync(dataset);

        Assert.Null(await replica.LoadAsync<Area>(area.Id));
        Assert.Equal(0, await replica.MarkerAsync());
        Assert.Equal(user, await replica.OwnerAsync());
        Assert.Equal(dataset, await replica.DatasetIdAsync());
        Assert.Equal(1, await outbox.CountAsync());
    }

    [Fact]
    public async Task ResettingDropsTheCachedStatisticsAndSnapshots()
    {
        var js = new RecordingJsRuntime();
        var snapshots = new InMemorySnapshotCache();
        await snapshots.SaveAsync("tok", new SnapshotView(
            Guid.NewGuid(), "List", "Tasks", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), [], []),
            DateTimeOffset.UtcNow);

        await new DatasetReset(new InMemoryReplica(), new StatisticsCache(js), snapshots).ResetAsync(Guid.NewGuid());

        Assert.Null(await snapshots.GetAsync("tok"));
        Assert.Contains("localStorage.removeItem", js.Calls);
    }

    sealed class RecordingJsRuntime : IJSRuntime
    {
        public List<string> Calls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Calls.Add(identifier);
            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
