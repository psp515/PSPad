using PSPad.Module.Sharing.Snapshots;
using PSPad.Module.Sharing.Tests.Fakes;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Sharing.Tests.Snapshots;

[UnitTest]
public class SnapshotPublishingTests
{
    static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Stranger = Guid.Parse("55555555-5555-5555-5555-555555555555");
    static readonly Guid AreaId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly Guid ListId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Today = new(2026, 9, 28);

    [Fact]
    public async Task PublishFailsWithNoFoundListWhenTheListIsMissing()
    {
        var publishing = NewPublishing(new FakeListContent { List = null });

        var (outcome, snapshot) = await publishing.PublishAsync(Owner, "Łukasz", ListId, Now.AddDays(1), Today, CancellationToken.None);

        Assert.Equal(PublishOutcome.NotFound, outcome);
        Assert.Null(snapshot);
    }

    [Fact]
    public async Task PublishFailsWithNotFoundWhenTheListIsDeleted()
    {
        var list = TasksList();
        list.ApplyAll(TaskList.Decide(list, new DeleteTaskList(Guid.NewGuid(), Owner, list.Id), Now));
        var publishing = NewPublishing(new FakeListContent { List = list });

        var (outcome, _) = await publishing.PublishAsync(Owner, "Łukasz", ListId, Now.AddDays(1), Today, CancellationToken.None);

        Assert.Equal(PublishOutcome.NotFound, outcome);
    }

    [Fact]
    public async Task PublishFailsWithNotOwnerForAnotherUsersList()
    {
        var publishing = NewPublishing(new FakeListContent { List = TasksList() });

        var (outcome, _) = await publishing.PublishAsync(Stranger, "Łukasz", ListId, Now.AddDays(1), Today, CancellationToken.None);

        Assert.Equal(PublishOutcome.NotOwner, outcome);
    }

    [Theory]
    [MemberData(nameof(BadExpiries))]
    public async Task PublishFailsWithBadExpiryOutsideTheAllowedWindow(DateTimeOffset expiresAt)
    {
        var publishing = NewPublishing(new FakeListContent { List = TasksList() });

        var (outcome, _) = await publishing.PublishAsync(Owner, "Łukasz", ListId, expiresAt, Today, CancellationToken.None);

        Assert.Equal(PublishOutcome.BadExpiry, outcome);
    }

    public static TheoryData<DateTimeOffset> BadExpiries => new()
    {
        Now,
        Now.AddSeconds(-1),
        Now.Add(SnapshotPublishing.LongestLife).AddSeconds(1)
    };

    [Fact]
    public async Task PublishSavesAndReturnsTheSnapshotWhenAllowed()
    {
        var store = new InMemorySnapshotStore();
        var publishing = NewPublishing(new FakeListContent { List = TasksList() }, store);
        var expiresAt = Now.AddDays(7);

        var (outcome, snapshot) = await publishing.PublishAsync(Owner, "Łukasz", ListId, expiresAt, Today, CancellationToken.None);

        Assert.Equal(PublishOutcome.Published, outcome);
        Assert.NotNull(snapshot);
        Assert.NotEqual(Guid.Empty, snapshot!.Id);
        Assert.NotEmpty(snapshot.Token);
        Assert.Equal(expiresAt, snapshot.ExpiresAt);
        Assert.Same(snapshot, await store.FindAsync(snapshot.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ActiveReturnsOnlyTheOwnersLiveSnapshotsNewestFirst()
    {
        var store = new InMemorySnapshotStore();
        var olderPublishing = new SnapshotPublishing(store, new FakeListContent { List = TasksList() }, new FixedClock(Now));
        var newerPublishing = new SnapshotPublishing(
            store, new FakeListContent { List = TasksList() }, new FixedClock(Now.AddMinutes(1)));
        var older = await Publish(olderPublishing, Now.AddDays(1));
        var newer = await Publish(newerPublishing, Now.AddDays(2));
        var expired = await Publish(olderPublishing, Now.AddDays(3));
        await store.SaveAsync(expired with { ExpiresAt = Now.AddSeconds(-1) }, CancellationToken.None);

        var active = await olderPublishing.ActiveAsync(Owner, ListId, CancellationToken.None);

        Assert.Equal([newer.Id, older.Id], active.Select(snapshot => snapshot.Id));
    }

    [Fact]
    public async Task RevokeFailsWhenTheSnapshotIsMissing()
    {
        var publishing = NewPublishing(new FakeListContent { List = TasksList() });

        var revoked = await publishing.RevokeAsync(Owner, Guid.NewGuid(), CancellationToken.None);

        Assert.False(revoked);
    }

    [Fact]
    public async Task RevokeFailsWhenTheCallerIsNotTheOwner()
    {
        var store = new InMemorySnapshotStore();
        var publishing = NewPublishing(new FakeListContent { List = TasksList() }, store);
        var snapshot = await Publish(publishing, Now.AddDays(1));

        var revoked = await publishing.RevokeAsync(Stranger, snapshot.Id, CancellationToken.None);

        Assert.False(revoked);
        Assert.NotNull(await store.FindAsync(snapshot.Id, CancellationToken.None));
    }

    [Fact]
    public async Task RevokeDeletesTheOwnersSnapshot()
    {
        var store = new InMemorySnapshotStore();
        var publishing = NewPublishing(new FakeListContent { List = TasksList() }, store);
        var snapshot = await Publish(publishing, Now.AddDays(1));

        var revoked = await publishing.RevokeAsync(Owner, snapshot.Id, CancellationToken.None);

        Assert.True(revoked);
        Assert.Null(await store.FindAsync(snapshot.Id, CancellationToken.None));
    }

    static async Task<ListSnapshot> Publish(SnapshotPublishing publishing, DateTimeOffset expiresAt)
    {
        var (_, snapshot) = await publishing.PublishAsync(Owner, "Łukasz", ListId, expiresAt, Today, CancellationToken.None);
        return snapshot!;
    }

    static SnapshotPublishing NewPublishing(FakeListContent content, InMemorySnapshotStore? store = null) =>
        new(store ?? new InMemorySnapshotStore(), content, new FixedClock(Now));

    static TaskList TasksList()
    {
        var list = new TaskList();
        list.Apply(new TaskListCreated(ListId, Owner, Now, AreaId, "Groceries", ListKind.Tasks));
        return list;
    }
}
