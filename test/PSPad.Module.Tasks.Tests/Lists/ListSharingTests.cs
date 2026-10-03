using PSPad.Abstractions;
using PSPad.Module.Tasks.Lists;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Tasks.Tests.Lists;

[UnitTest]
public class ListSharingTests
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
    public void SharingSetsTheTokenAndOwnerName()
    {
        var list = Existing();
        var shared = Assert.IsType<TaskListShared>(Assert.Single(
            TaskList.Decide(list, new ShareTaskList(Guid.NewGuid(), Owner, list.Id, Token, "Łukasz"), Now)));
        list.ApplyAll([shared]);
        Assert.Equal(Token, list.InviteToken);
        Assert.Equal("Łukasz", list.OwnerName);
    }

    [Fact]
    public void AShortTokenIsRejected() =>
        Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(Existing(), new ShareTaskList(Guid.NewGuid(), Owner, ListId, "short", "Ł"), Now));

    [Fact]
    public void RotatingKeepsMembers()
    {
        var list = WithMember();
        list.ApplyAll(TaskList.Decide(list, new ShareTaskList(Guid.NewGuid(), Owner, list.Id, Token + "x", "Ł"), Now));
        Assert.True(list.HasMember(Member));
        Assert.Equal(Token + "x", list.InviteToken);
    }

    [Fact]
    public void OnlyTheOwnerShares() =>
        Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(WithMember(), new ShareTaskList(Guid.NewGuid(), Member, ListId, Token, "M"), Now));

    [Fact]
    public void StoppingClearsTheTokenButKeepsMembers()
    {
        var list = WithMember();
        list.ApplyAll(TaskList.Decide(list, new StopSharingTaskList(Guid.NewGuid(), Owner, list.Id), Now));
        Assert.Null(list.InviteToken);
        Assert.True(list.HasMember(Member));
    }

    [Fact]
    public void StoppingAnUnsharedListEmitsNothing() =>
        Assert.Empty(TaskList.Decide(Existing(), new StopSharingTaskList(Guid.NewGuid(), Owner, ListId), Now));

    [Fact]
    public void JoiningWithTheTokenAddsAMember()
    {
        var list = Shared();
        var joined = Assert.IsType<TaskListJoined>(Assert.Single(
            TaskList.Decide(list, new JoinTaskList(Guid.NewGuid(), Member, list.Id, Token, "Anna"), Now)));
        Assert.Equal(Owner, joined.UserId);
        list.ApplyAll([joined]);
        Assert.Equal(new ListMember(Member, "Anna", Now), Assert.Single(list.Members));
        Assert.True(list.IsShared);
    }

    [Fact]
    public void AWrongTokenIsRejected() =>
        Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(Shared(), new JoinTaskList(Guid.NewGuid(), Member, ListId, "wrong-token-wrong-token", "A"), Now));

    [Fact]
    public void AClearedTokenIsRejected()
    {
        var list = Shared();
        list.ApplyAll(TaskList.Decide(list, new StopSharingTaskList(Guid.NewGuid(), Owner, list.Id), Now));
        Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(list, new JoinTaskList(Guid.NewGuid(), Member, list.Id, Token, "A"), Now));
    }

    [Fact]
    public void TheOwnerOrAnExistingMemberJoiningEmitsNothing()
    {
        Assert.Empty(TaskList.Decide(Shared(), new JoinTaskList(Guid.NewGuid(), Owner, ListId, Token, "O"), Now));
        Assert.Empty(TaskList.Decide(WithMember(), new JoinTaskList(Guid.NewGuid(), Member, ListId, Token, "A"), Now));
    }

    [Fact]
    public void TheOwnerRemovesAMember()
    {
        var list = WithMember();
        list.ApplyAll(TaskList.Decide(list, new RemoveListMember(Guid.NewGuid(), Owner, list.Id, Member), Now));
        Assert.Empty(list.Members);
    }

    [Fact]
    public void RemovingANonMemberIsRejected() =>
        Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(Shared(), new RemoveListMember(Guid.NewGuid(), Owner, ListId, Stranger), Now));

    [Fact]
    public void AMemberLeaves()
    {
        var list = WithMember();
        var left = Assert.IsType<TaskListLeft>(Assert.Single(
            TaskList.Decide(list, new LeaveTaskList(Guid.NewGuid(), Member, list.Id), Now)));
        Assert.Equal(Owner, left.UserId);
        list.ApplyAll([left]);
        Assert.False(list.HasMember(Member));
    }

    [Fact]
    public void TheOwnerCannotLeave() =>
        Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(WithMember(), new LeaveTaskList(Guid.NewGuid(), Owner, ListId), Now));

    [Fact]
    public void MembersCannotRenameDeleteOrMoveTheList()
    {
        var list = WithMember();
        Assert.Throws<DomainRejectedException>(() => TaskList.Decide(list, new RenameTaskList(Guid.NewGuid(), Member, list.Id, "X"), Now));
        Assert.Throws<DomainRejectedException>(() => TaskList.Decide(list, new DeleteTaskList(Guid.NewGuid(), Member, list.Id), Now));
        Assert.Throws<DomainRejectedException>(() => TaskList.Decide(list, new MoveTaskListToArea(Guid.NewGuid(), Member, list.Id, Guid.NewGuid()), Now));
    }
}
