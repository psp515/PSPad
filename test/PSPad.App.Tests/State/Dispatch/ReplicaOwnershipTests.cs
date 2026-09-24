using System.Text.Json;
using Microsoft.JSInterop;
using PSPad.App.State.Dispatch;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.App.Statistics;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Dispatch;

[UnitTest]
public class ReplicaOwnershipTests
{
    [Fact]
    public async Task FirstSignInOnAGenuinelyEmptyDeviceRecordsTheOwner()
    {
        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        var ownership = new ReplicaOwnership(replica, outbox, NewCache());
        var user = Guid.NewGuid();

        await ownership.EnsureCurrentUserAsync(user);

        Assert.Equal(user, await replica.OwnerAsync());
        Assert.Equal(0, await outbox.CountAsync());
    }

    [Fact]
    public async Task AFreshSignInReportsThatTheDeviceHoldsNothingYet()
    {
        var replica = new InMemoryReplica();
        var ownership = new ReplicaOwnership(replica, new InMemoryOutbox(), NewCache());

        await ownership.EnsureCurrentUserAsync(Guid.NewGuid());

        Assert.True(await ownership.NothingSyncedYetAsync());
    }

    [Fact]
    public async Task ADeviceThatHasPulledBeforeIsNotReportedAsEmpty()
    {
        var replica = new InMemoryReplica();
        var user = Guid.NewGuid();
        var ownership = new ReplicaOwnership(replica, new InMemoryOutbox(), NewCache());
        await ownership.EnsureCurrentUserAsync(user);
        await replica.SetMarkerAsync(41);

        await ownership.EnsureCurrentUserAsync(user);

        Assert.False(await ownership.NothingSyncedYetAsync());
    }

    [Fact]
    public async Task SigningInAsSomeoneElseMakesTheDeviceEmptyAgain()
    {
        var replica = new InMemoryReplica();
        var ownership = new ReplicaOwnership(replica, new InMemoryOutbox(), NewCache());
        await ownership.EnsureCurrentUserAsync(Guid.NewGuid());
        await replica.SetMarkerAsync(41);

        await ownership.EnsureCurrentUserAsync(Guid.NewGuid());

        Assert.True(await ownership.NothingSyncedYetAsync());
    }

    [Fact]
    public async Task DataQueuedBeforeAnyOwnerWasEverRecordedIsClearedToo()
    {
        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        var ownership = new ReplicaOwnership(replica, outbox, NewCache());
        var user = Guid.NewGuid();

        await ownership.EnsureCurrentUserAsync(user);

        Assert.Equal(user, await replica.OwnerAsync());
        Assert.Equal(0, await outbox.CountAsync());
    }

    [Fact]
    public async Task TheSameUserSigningInAgainKeepsThePendingOutbox()
    {
        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        var user = Guid.NewGuid();
        await replica.SetOwnerAsync(user);
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        var ownership = new ReplicaOwnership(replica, outbox, NewCache());

        await ownership.EnsureCurrentUserAsync(user);

        Assert.Equal(1, await outbox.CountAsync());
    }

    [Fact]
    public async Task ADifferentUserSigningInClearsTheStaleReplicaAndOutbox()
    {
        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        var previousUser = Guid.NewGuid();
        await replica.SetOwnerAsync(previousUser);
        await replica.SetMarkerAsync(42);
        await outbox.AppendAsync(Guid.NewGuid(), Envelope());
        var ownership = new ReplicaOwnership(replica, outbox, NewCache());
        var nextUser = Guid.NewGuid();

        await ownership.EnsureCurrentUserAsync(nextUser);

        Assert.Equal(nextUser, await replica.OwnerAsync());
        Assert.Equal(0, await outbox.CountAsync());
        Assert.Equal(0, await replica.MarkerAsync());
    }

    [Fact]
    public async Task ADifferentUserSigningInClearsTheStatisticsCacheToo()
    {
        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        var cache = NewCache();
        await cache.WriteAsync(30, SampleOverview());
        var previousUser = Guid.NewGuid();
        await replica.SetOwnerAsync(previousUser);
        var ownership = new ReplicaOwnership(replica, outbox, cache);

        await ownership.EnsureCurrentUserAsync(Guid.NewGuid());

        Assert.Null(await cache.ReadAsync(30));
    }

    [Fact]
    public async Task TheSameUserSigningInAgainKeepsTheStatisticsCache()
    {
        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        var cache = NewCache();
        var user = Guid.NewGuid();
        await replica.SetOwnerAsync(user);
        await cache.WriteAsync(30, SampleOverview());
        var ownership = new ReplicaOwnership(replica, outbox, cache);

        await ownership.EnsureCurrentUserAsync(user);

        Assert.NotNull(await cache.ReadAsync(30));
    }

    static StatisticsCache NewCache() => new(new FakeJsRuntime());

    static StatisticsOverview SampleOverview() =>
        new(new StatisticsTilesView(0, 0, 0, 0), [], [], [], [], []);

    static CommandEnvelope Envelope() =>
        new(nameof(CreateArea), JsonSerializer.SerializeToElement(new { Name = "Home" }));

    sealed class FakeJsRuntime : IJSRuntime
    {
        readonly Dictionary<string, string> _values = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            var key = (string)args![0]!;

            switch (identifier)
            {
                case "localStorage.getItem":
                    return ValueTask.FromResult((TValue)(object?)(_values.TryGetValue(key, out var value) ? value : null)!);
                case "localStorage.setItem":
                    _values[key] = (string)args[1]!;
                    return ValueTask.FromResult(default(TValue)!);
                case "localStorage.removeItem":
                    _values.Remove(key);
                    return ValueTask.FromResult(default(TValue)!);
                default:
                    return ValueTask.FromResult(default(TValue)!);
            }
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
