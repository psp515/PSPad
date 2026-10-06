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
    const string Code = "K7M4PX";

    static TaskList Existing()
    {
        var list = new TaskList();
        list.Apply(new TaskListCreated(ListId, Owner, Now, Guid.NewGuid(), "Errands"));
        return list;
    }

    static TaskList Shared()
    {
        var list = Existing();
        list.Apply(new TaskListShared(list.Id, Owner, Now, Token, "Łukasz", Code));
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
            TaskList.Decide(list, new ShareTaskList(Guid.NewGuid(), Owner, list.Id, Token, Code, "Łukasz"), Now)));
        list.ApplyAll([shared]);
        Assert.Equal(Token, list.InviteToken);
        Assert.Equal("Łukasz", list.OwnerName);
    }

    [Fact]
    public void AShortTokenIsRejected() =>
        Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(Existing(), new ShareTaskList(Guid.NewGuid(), Owner, ListId, "short", Code, "Ł"), Now));

    [Fact]
    public void RotatingKeepsMembers()
    {
        var list = WithMember();
        list.ApplyAll(TaskList.Decide(list, new ShareTaskList(Guid.NewGuid(), Owner, list.Id, Token + "x", Code, "Ł"), Now));
        Assert.True(list.HasMember(Member));
        Assert.Equal(Token + "x", list.InviteToken);
    }

    [Fact]
    public void OnlyTheOwnerShares() =>
        Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(WithMember(), new ShareTaskList(Guid.NewGuid(), Member, ListId, Token, Code, "M"), Now));

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
            TaskList.Decide(list, new JoinTaskList(Guid.NewGuid(), Member, list.Id, Token, Code, "Anna"), Now)));
        Assert.Equal(Owner, joined.UserId);
        list.ApplyAll([joined]);
        Assert.Equal(new ListMember(Member, "Anna", Now), Assert.Single(list.Members));
        Assert.True(list.IsShared);
    }

    [Fact]
    public void AWrongTokenIsRejected() =>
        Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(Shared(), new JoinTaskList(Guid.NewGuid(), Member, ListId, "wrong-token-wrong-token", Code, "A"), Now));

    [Fact]
    public void AClearedTokenIsRejected()
    {
        var list = Shared();
        list.ApplyAll(TaskList.Decide(list, new StopSharingTaskList(Guid.NewGuid(), Owner, list.Id), Now));
        Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(list, new JoinTaskList(Guid.NewGuid(), Member, list.Id, Token, Code, "A"), Now));
    }

    [Fact]
    public void TheOwnerOrAnExistingMemberJoiningEmitsNothing()
    {
        Assert.Empty(TaskList.Decide(Shared(), new JoinTaskList(Guid.NewGuid(), Owner, ListId, Token, Code, "O"), Now));
        Assert.Empty(TaskList.Decide(WithMember(), new JoinTaskList(Guid.NewGuid(), Member, ListId, Token, Code, "A"), Now));
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

    [Fact]
    public void SharingStartsAThirtyMinuteInvite()
    {
        var list = Shared();
        Assert.Equal(Now.AddMinutes(30), list.InviteExpiresAt);
        Assert.Equal(Code, list.InviteCode);
        Assert.True(list.IsInviteLiveAt(Now.AddMinutes(29)));
        Assert.False(list.IsInviteLiveAt(Now.AddMinutes(30)));
    }

    [Fact]
    public void AMalformedCodeIsRejected() =>
        Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(Existing(), new ShareTaskList(Guid.NewGuid(), Owner, ListId, Token, "OOOOOO", "Ł"), Now));

    [Fact]
    public void ANewLinkRestartsTheClockAndClearsWrongCodes()
    {
        var list = Shared();
        list.ApplyAll(TaskList.Decide(list, new JoinTaskList(Guid.NewGuid(), Stranger, ListId, Token, "AAAAAA", "S"), Now));
        var later = Now.AddMinutes(40);
        list.ApplyAll(TaskList.Decide(list, new ShareTaskList(Guid.NewGuid(), Owner, list.Id, Token + "y", "BBBBBB", "Łukasz"), later));
        Assert.Equal(later.AddMinutes(30), list.InviteExpiresAt);
        Assert.Equal(0, list.WrongCodes);
    }

    [Fact]
    public void ReSharingTheSameTokenAfterExpiryRestartsIt() =>
        Assert.Single(TaskList.Decide(Shared(),
            new ShareTaskList(Guid.NewGuid(), Owner, ListId, Token, Code, "Łukasz"), Now.AddHours(2)));

    [Theory]
    [InlineData("k3Jv9s2mQ0x7b1nR4tYw8eZa", "K7M4PX", 10, InviteCheck.Open)]
    [InlineData("k3Jv9s2mQ0x7b1nR4tYw8eZa", "k7m-4px", 10, InviteCheck.Open)]
    [InlineData("k3Jv9s2mQ0x7b1nR4tYw8eZa", "AAAAAA", 10, InviteCheck.WrongCode)]
    [InlineData("k3Jv9s2mQ0x7b1nR4tYw8eZa", "AAAAAA", 40, InviteCheck.Unknown)]
    [InlineData("k3Jv9s2mQ0x7b1nR4tYw8eZa", "K7M4PX", 40, InviteCheck.Expired)]
    [InlineData("wrong-token-000000000000", "K7M4PX", 10, InviteCheck.Unknown)]
    public void CheckInviteClassifies(string token, string code, int minutesLater, InviteCheck expected) =>
        Assert.Equal(expected, Shared().CheckInvite(token, code, Now.AddMinutes(minutesLater)));

    [Fact]
    public void JoiningAnExpiredInviteIsRejected()
    {
        var rejection = Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(Shared(), new JoinTaskList(Guid.NewGuid(), Stranger, ListId, Token, Code, "S"), Now.AddMinutes(31)));
        Assert.Equal("This invite has expired.", rejection.Message);
    }

    [Fact]
    public void AWrongCodeIsRecordedNotRejected()
    {
        var events = TaskList.Decide(Shared(), new JoinTaskList(Guid.NewGuid(), Stranger, ListId, Token, "AAAAAA", "S"), Now);
        var rejected = Assert.IsType<InviteCodeRejected>(Assert.Single(events));
        Assert.Equal(1, rejected.WrongCodes);
        Assert.Equal(Owner, rejected.UserId);
    }

    [Fact]
    public void FiveWrongCodesCloseTheInvite()
    {
        var list = Shared();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            list.ApplyAll(TaskList.Decide(list, new JoinTaskList(Guid.NewGuid(), Stranger, ListId, Token, "AAAAAA", "S"), Now));
        }

        Assert.Null(list.InviteToken);
        Assert.Null(list.InviteCode);
        Assert.True(list.ClosedByWrongCodes);
        Assert.Throws<DomainRejectedException>(() =>
            TaskList.Decide(list, new JoinTaskList(Guid.NewGuid(), Stranger, ListId, Token, Code, "S"), Now));
    }

    [Fact]
    public void JoiningWithTheRightCodeInsideThirtyMinutesWorks() =>
        Assert.IsType<TaskListJoined>(Assert.Single(
            TaskList.Decide(Shared(), new JoinTaskList(Guid.NewGuid(), Stranger, ListId, Token, Code, "S"), Now.AddMinutes(29))));
}
