using System.Text.Json;
using PSPad.App.State;
using PSPad.App.State.Dispatch;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;
using PSPad.Contracts;
using PSPad.Module.Tasks.Areas;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Outbox;

[UnitTest]
public class OutboxTests
{
    [Fact]
    public async Task EntriesComeBackInTheOrderTheyWereAppended()
    {
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), Envelope("first"));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope("second"));

        var peeked = await outbox.PeekAsync(10);

        Assert.Equal(["first", "second"], peeked.Select(NameIn));
    }

    [Fact]
    public async Task RemovingThroughAPositionLeavesEverythingAfterIt()
    {
        var outbox = new InMemoryOutbox();
        await outbox.AppendAsync(Guid.NewGuid(), Envelope("first"));
        await outbox.AppendAsync(Guid.NewGuid(), Envelope("second"));
        var peeked = await outbox.PeekAsync(10);

        await outbox.RemoveThroughAsync(peeked[0].Position);

        Assert.Equal(["second"], (await outbox.PeekAsync(10)).Select(NameIn));
    }

    [Fact]
    public async Task CommittingQueuesInsteadOfPosting()
    {
        var replica = new InMemoryReplica();
        var outbox = new InMemoryOutbox();
        var work = new ReplicaUnitOfWork(replica, outbox);
        var command = new CreateArea(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Home", 0);
        var area = new Area();
        var events = Area.Decide(null, command, DateTimeOffset.UnixEpoch);
        area.ApplyAll(events);

        work.Queue(command);
        work.Stage(area, events);
        await work.CommitAsync(command.CommandId, command.UserId, CancellationToken.None);

        Assert.Equal(1, await outbox.CountAsync());
        Assert.NotNull(await replica.LoadAsync<Area>(area.Id));
    }

    static CommandEnvelope Envelope(string name) =>
        new(nameof(CreateArea), JsonSerializer.SerializeToElement(new { Name = name }));

    static string NameIn(OutboxEntry entry) =>
        entry.Envelope.Payload.GetProperty("Name").GetString()!;
}
