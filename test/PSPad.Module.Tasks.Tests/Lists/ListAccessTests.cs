using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.Tests.Fakes;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Lists;

[UnitTest]
public class ListAccessTests
{
    static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid Member = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly Guid Stranger = Guid.Parse("33333333-3333-3333-3333-333333333333");
    static readonly Guid ListId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);
    const string Token = "k3Jv9s2mQ0x7b1nR4tYw8eZa";

    static TaskList Existing()
    {
        var list = new TaskList();
        list.Apply(new TaskListCreated(ListId, Owner, Now, Guid.NewGuid(), "Errands"));
        return list;
    }

    static TaskList Shared()
    {
        var list = Existing();
        list.Apply(new TaskListShared(list.Id, Owner, Now, Token, "Łukasz"));
        return list;
    }

    static TaskList WithMember()
    {
        var list = Shared();
        list.Apply(new TaskListJoined(list.Id, Owner, Now, Member, "Anna"));
        return list;
    }

    [Fact]
    public void TheOwnerHasAccess() => Assert.Equal(new ListAccess(Owner, Owner), ListAccess.To(WithMember(), Owner));

    [Fact]
    public void AMemberHasAccessOnTheOwnersBehalf() => Assert.Equal(new ListAccess(Owner, Member), ListAccess.To(WithMember(), Member));

    [Fact]
    public void AStrangerIsRejected() => Assert.Throws<DomainRejectedException>(() => ListAccess.To(WithMember(), Stranger));

    [Fact]
    public void ADeletedListIsRejected()
    {
        var list = WithMember();
        list.ApplyAll(TaskList.Decide(list, new DeleteTaskList(Guid.NewGuid(), Owner, list.Id), Now));
        Assert.Throws<DomainRejectedException>(() => ListAccess.To(list, Owner));
    }

    [Fact]
    public void AMissingListIsRejected() => Assert.Throws<DomainRejectedException>(() => ListAccess.To(null, Owner));

    [Fact]
    public void OwnerAccessIsByTheOwner() => Assert.True(ListAccess.Owner(Owner).ByOwner);

    [Fact]
    public async Task LoadingWithoutAListFallsBackToOwnerAccess() =>
        Assert.Equal(ListAccess.Owner(Member), await new FakeDocumentStore<TaskList>().AccessAsync(null, Member, CancellationToken.None));

    [Fact]
    public async Task AMembersTaskListIsAccessible()
    {
        var store = new FakeDocumentStore<TaskList>();
        var list = WithMember();
        store.Seed(list);
        Assert.Equal(new ListAccess(Owner, Member), await store.AccessAsync(list.Id, Member, CancellationToken.None));
    }
}
