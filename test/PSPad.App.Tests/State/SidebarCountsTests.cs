using PSPad.App.State;
using PSPad.App.State.Replica;
using PSPad.Module.Tasks.Inbox;
using PSPad.Module.Tasks.Tasks;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State;

[UnitTest]
public class SidebarCountsTests
{
    static readonly Guid User = Guid.NewGuid();
    static readonly DateOnly Today = new(2026, 9, 12);

    [Fact]
    public async Task AUserWithNoInboxHasAZeroCount()
    {
        var counts = Counts(new InMemoryReplica());

        await counts.RefreshAsync();

        Assert.Equal(0, counts.Inbox);
    }

    static SidebarCounts Counts(InMemoryReplica replica) =>
        new(new ReplicaDocumentStore<TodoTask>(replica),
            new ReplicaDocumentStore<Inbox>(replica),
            new AppState { UserId = User, Today = Today });
}
