using System.Text.Json;
using PSPad.App.State.Dispatch;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
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
        var ownership = new ReplicaOwnership(replica, outbox);
        var user = Guid.NewGuid();

        await ownership.EnsureCurrentUserAsync(user);

        Assert.Equal(user, await replica.OwnerAsync());
        Assert.Equal(0, await outbox.CountAsync());
    }

    [Fact]
    public async Task AFreshSignInReportsThatTheDeviceHoldsNothingYet()
    {
        var replica = new InMemoryReplica();
        var ownership = new ReplicaOwnership(replica, new InMemoryOutbox());

        await ownership.EnsureCurrentUserAsync(Guid.NewGuid());

        Assert.True(await ownership.NothingSyncedYetAsync());
    }

    [Fact]
    public async Task ADeviceThatHasPulledBeforeIsNotReportedAsEmpty()
    {
        var replica = new InMemoryReplica();
        var user = Guid.NewGuid();
        var ownership = new ReplicaOwnership(replica, new InMemoryOutbox());
        await ownership.EnsureCurrentUserAsync(user);
        await replica.SetMarkerAsync(41);

        await ownership.EnsureCurrentUserAsync(user);

        Assert.False(await ownership.NothingSyncedYetAsync());
    }

    [Fact]
    public async Task SigningInAsSomeoneElseMakesTheDeviceEmptyAgain()
    {
        var replica = new InMemoryReplica();
        var ownership = new ReplicaOwnership(replica, new InMemoryOutbox());
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
        var ownership = new ReplicaOwnership(replica, outbox);
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
        var ownership = new ReplicaOwnership(replica, outbox);

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
        var ownership = new ReplicaOwnership(replica, outbox);
        var nextUser = Guid.NewGuid();

        await ownership.EnsureCurrentUserAsync(nextUser);

        Assert.Equal(nextUser, await replica.OwnerAsync());
        Assert.Equal(0, await outbox.CountAsync());
        Assert.Equal(0, await replica.MarkerAsync());
    }

    static CommandEnvelope Envelope() =>
        new(nameof(CreateArea), JsonSerializer.SerializeToElement(new { Name = "Home" }));
}
