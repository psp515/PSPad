using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;
using PSPad.TestInfrastructure;

namespace PSPad.Module.Money.Tests.Budgets;

[UnitTest]
public class BudgetSharingTests
{
    const string Token = "abcdefghijklmnopqrstuvwxyz";
    const string Code = "K7M4PX";
    static readonly Guid Owner = Given.Owner;
    static readonly Guid Member = Given.Member;
    static readonly Guid Stranger = Given.Stranger;
    static readonly DateTimeOffset Now = Given.Now;

    static Budget Shared() => Given.Share(Given.ABudget());

    static string Rejection(Budget? budget, ICommand command, DateTimeOffset? at = null) =>
        Assert.Throws<DomainRejectedException>(() => Budget.Decide(budget, command, at ?? Now)).Message;

    static IReadOnlyList<DomainEvent> Apply(Budget budget, ICommand command, DateTimeOffset? at = null)
    {
        var events = Budget.Decide(budget, command, at ?? Now);
        budget.ApplyAll(events);
        return events;
    }

    static JoinBudget Join(Budget budget, Guid who, string code = Code, string token = Token) =>
        new(Guid.NewGuid(), who, budget.Id, token, code, "Anna Nowak");

    [Fact]
    public void SharingSetsTheInviteForThirtyMinutes()
    {
        var budget = Shared();

        Assert.Equal(Token, budget.InviteToken);
        Assert.Equal(Code, budget.InviteCode);
        Assert.Equal(Now + Invites.Life, budget.InviteExpiresAt);
        Assert.Equal("Lukasz Kolber", budget.OwnerName);
        Assert.True(budget.IsShared);
        Assert.True(budget.IsInviteLiveAt(Now));
    }

    [Fact]
    public void SharingNormalizesTheCode()
    {
        var budget = Given.ABudget();
        Apply(budget, new ShareBudget(Guid.NewGuid(), Owner, budget.Id, Token, "k7m-4px", "Lukasz Kolber"));

        Assert.Equal(Code, budget.InviteCode);
    }

    [Fact]
    public void AShortTokenOrBadCodeIsRejected()
    {
        var budget = Given.ABudget();

        Assert.Equal("An invite link needs a longer token.",
            Rejection(budget, new ShareBudget(Guid.NewGuid(), Owner, budget.Id, "short", Code, "L")));
        Assert.Equal("An invite needs a 6-character code.",
            Rejection(budget, new ShareBudget(Guid.NewGuid(), Owner, budget.Id, Token, "K7M", "L")));
    }

    [Fact]
    public void OnlyTheOwnerShares()
    {
        var budget = Given.Join(Shared());

        Assert.Equal("That budget does not exist.",
            Rejection(budget, new ShareBudget(Guid.NewGuid(), Member, budget.Id, Token, Code, "Anna")));
    }

    [Fact]
    public void AnArchivedBudgetCannotBeShared()
    {
        var budget = Given.Archive(Given.ABudget());

        Assert.Equal("This budget is archived.",
            Rejection(budget, new ShareBudget(Guid.NewGuid(), Owner, budget.Id, Token, Code, "L")));
    }

    [Fact]
    public void RotatingKeepsMembersAndRestartsTheClock()
    {
        var budget = Given.Join(Shared());
        var later = Now.AddMinutes(10);

        Apply(budget, new ShareBudget(Guid.NewGuid(), Owner, budget.Id, Token + "2", "AAAAAA", "Lukasz Kolber"), later);

        Assert.Single(budget.Members);
        Assert.Equal("AAAAAA", budget.InviteCode);
        Assert.Equal(later + Invites.Life, budget.InviteExpiresAt);
    }

    [Fact]
    public void JoiningAddsAMember()
    {
        var budget = Shared();

        var joined = Assert.IsType<BudgetJoined>(Assert.Single(Apply(budget, Join(budget, Member))));

        Assert.Equal(Owner, joined.UserId);
        Assert.Equal(Member, joined.MemberId);
        var member = Assert.Single(budget.Members);
        Assert.Equal(new BudgetMember(Member, "Anna Nowak", Now), member);
        Assert.True(budget.HasMember(Member));
    }

    [Fact]
    public void TheOwnerOrAMemberJoiningAgainChangesNothing()
    {
        var budget = Given.Join(Shared());

        Assert.Empty(Budget.Decide(budget, Join(budget, Owner), Now));
        Assert.Empty(Budget.Decide(budget, Join(budget, Member), Now));
    }

    [Fact]
    public void AWrongCodeIsCountedAndTheFifthClosesTheInvite()
    {
        var budget = Shared();

        for (var attempt = 1; attempt <= Invites.MostWrongCodes; attempt++)
        {
            var rejected = Assert.IsType<BudgetInviteCodeRejected>(Assert.Single(Apply(budget, Join(budget, Stranger, "AAAAAA"))));
            Assert.Equal(attempt, rejected.WrongCodes);
        }

        Assert.Null(budget.InviteToken);
        Assert.Null(budget.InviteCode);
        Assert.True(budget.ClosedByWrongCodes);
    }

    [Fact]
    public void AnExpiredInviteIsRejected()
    {
        var budget = Shared();

        Assert.Equal("This invite has expired.", Rejection(budget, Join(budget, Stranger), Now + Invites.Life));
    }

    [Fact]
    public void AWrongTokenIsRejected()
    {
        var budget = Shared();

        Assert.Equal("This invite link no longer works.", Rejection(budget, Join(budget, Stranger, token: Token + "x")));
    }

    [Fact]
    public void AnArchivedBudgetCannotBeJoined()
    {
        var budget = Shared();
        Apply(budget, new ArchiveBudget(Guid.NewGuid(), Owner, budget.Id));

        Assert.Equal("This invite link no longer works.", Rejection(budget, Join(budget, Stranger)));
    }

    [Fact]
    public void ArchivingEndsTheInvite()
    {
        var budget = Given.Join(Shared());

        Apply(budget, new ArchiveBudget(Guid.NewGuid(), Owner, budget.Id));

        Assert.Null(budget.InviteToken);
        Assert.Null(budget.InviteCode);
        Assert.Null(budget.InviteExpiresAt);
        Assert.Single(budget.Members);
    }

    [Fact]
    public void AFullBudgetRejectsAJoin()
    {
        var budget = Shared();
        for (var index = 0; index < ContentLimits.MembersPerBudget; index++)
        {
            Apply(budget, Join(budget, Guid.NewGuid()));
        }

        Assert.Equal("This budget already has as many members as it can hold.", Rejection(budget, Join(budget, Stranger)));
    }

    [Fact]
    public void StoppingClearsTheInviteAndKeepsMembers()
    {
        var budget = Given.Join(Shared());

        Assert.IsType<BudgetSharingStopped>(Assert.Single(Apply(budget, new StopSharingBudget(Guid.NewGuid(), Owner, budget.Id))));

        Assert.Null(budget.InviteToken);
        Assert.Single(budget.Members);
        Assert.Empty(Budget.Decide(budget, new StopSharingBudget(Guid.NewGuid(), Owner, budget.Id), Now));
    }

    [Fact]
    public void RemovingAMemberDropsThem()
    {
        var budget = Given.Join(Shared());

        Apply(budget, new RemoveBudgetMember(Guid.NewGuid(), Owner, budget.Id, Member));

        Assert.Empty(budget.Members);
        Assert.Equal("That person is not a member of this budget.",
            Rejection(budget, new RemoveBudgetMember(Guid.NewGuid(), Owner, budget.Id, Member)));
    }

    [Fact]
    public void OnlyTheOwnerRemovesOrStops()
    {
        var budget = Given.Join(Shared());

        Assert.Equal("That budget does not exist.",
            Rejection(budget, new RemoveBudgetMember(Guid.NewGuid(), Member, budget.Id, Member)));
        Assert.Equal("That budget does not exist.",
            Rejection(budget, new StopSharingBudget(Guid.NewGuid(), Member, budget.Id)));
    }

    [Fact]
    public void AMemberLeaves()
    {
        var budget = Given.Join(Shared());

        var left = Assert.IsType<BudgetLeft>(Assert.Single(Apply(budget, new LeaveBudget(Guid.NewGuid(), Member, budget.Id))));

        Assert.Equal(Owner, left.UserId);
        Assert.Empty(budget.Members);
    }

    [Fact]
    public void TheOwnerAndStrangersCannotLeave()
    {
        var budget = Shared();

        Assert.Equal("The owner cannot leave their own budget.",
            Rejection(budget, new LeaveBudget(Guid.NewGuid(), Owner, budget.Id)));
        Assert.Equal("You are not a member of this budget.",
            Rejection(budget, new LeaveBudget(Guid.NewGuid(), Stranger, budget.Id)));
    }

    [Fact]
    public void LeavingRemovingAndStoppingWorkOnAnArchivedBudget()
    {
        var budget = Given.Join(Given.Join(Shared(), Stranger));
        Apply(budget, new ArchiveBudget(Guid.NewGuid(), Owner, budget.Id));

        Apply(budget, new LeaveBudget(Guid.NewGuid(), Member, budget.Id));
        Apply(budget, new RemoveBudgetMember(Guid.NewGuid(), Owner, budget.Id, Stranger));

        Assert.Empty(budget.Members);
        Assert.Empty(Budget.Decide(budget, new StopSharingBudget(Guid.NewGuid(), Owner, budget.Id), Now));
    }

    [Fact]
    public void AMemberCannotRenameArchiveOrRestore()
    {
        var budget = Given.Join(Shared());

        Assert.Equal("That budget does not exist.",
            Rejection(budget, new RenameBudget(Guid.NewGuid(), Member, budget.Id, "Mine")));
        Assert.Equal("That budget does not exist.",
            Rejection(budget, new ArchiveBudget(Guid.NewGuid(), Member, budget.Id)));
        Assert.Equal("That budget does not exist.",
            Rejection(budget, new RestoreBudget(Guid.NewGuid(), Member, budget.Id)));
    }

    [Fact]
    public void SharingCommandsAreServerOnly()
    {
        Assert.IsAssignableFrom<IServerOnlyCommand>(new ShareBudget(Guid.NewGuid(), Owner, Guid.NewGuid(), Token, Code, "L"));
        Assert.IsAssignableFrom<IServerOnlyCommand>(new StopSharingBudget(Guid.NewGuid(), Owner, Guid.NewGuid()));
        Assert.IsAssignableFrom<IServerOnlyCommand>(new RemoveBudgetMember(Guid.NewGuid(), Owner, Guid.NewGuid(), Member));
        Assert.IsAssignableFrom<IServerOnlyCommand>(new LeaveBudget(Guid.NewGuid(), Member, Guid.NewGuid()));
        Assert.IsAssignableFrom<IServerOnlyCommand>(new JoinBudget(Guid.NewGuid(), Member, Guid.NewGuid(), Token, Code, "A"));
    }
}
