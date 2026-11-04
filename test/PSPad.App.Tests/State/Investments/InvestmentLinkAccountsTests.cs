using PSPad.App.State.Investments;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Money.Balance;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class InvestmentLinkAccountsTests
{
    static readonly IReadOnlyDictionary<Guid, Holding> NoLinks =
        new Dictionary<Guid, Holding>();

    [Fact]
    public void AddOffersOnlyOwnUnarchivedAccountsInNameOrder()
    {
        var mine = Given.AnAccount("B");
        var first = Given.AnAccount("A");
        var archived = Given.AnAccount("Old", archived: true);
        var theirs = Given.AnAccount("Theirs", owner: Guid.NewGuid());

        var rows = InvestmentLinkAccounts.Select([mine, first, archived, theirs], Given.User, NoLinks, refresh: false);

        Assert.Equal(["A", "B"], rows.Select(row => row.Account.Name));
        Assert.All(rows, row => Assert.True(row.Ticked));
    }

    [Fact]
    public void AnAccountAlreadyLinkedCarriesItsHolding()
    {
        var account = Given.AnAccount();
        var holding = Given.Linked(account);
        var links = new Dictionary<Guid, Holding> { [account.Id] = holding };

        var row = Assert.Single(InvestmentLinkAccounts.Select([account], Given.User, links, refresh: false));

        Assert.Equal(holding, row.Existing);
    }

    [Fact]
    public void RefreshOffersOnlyTheLinkedAccounts()
    {
        var linked = Given.AnAccount("Linked");
        var other = Given.AnAccount("Other");
        var links = new Dictionary<Guid, Holding> { [linked.Id] = Given.Linked(linked) };

        var rows = InvestmentLinkAccounts.Select([linked, other], Given.User, links, refresh: true);

        Assert.Equal(linked.Id, Assert.Single(rows).Account.Id);
    }

    [Fact]
    public void ALinkedArchivedAccountCannotBeRefreshed()
    {
        var archived = Given.AnAccount(archived: true);
        var links = new Dictionary<Guid, Holding> { [archived.Id] = Given.Linked(archived) };

        Assert.Empty(InvestmentLinkAccounts.Select([archived], Given.User, links, refresh: true));
        Assert.False(InvestmentLinkAccounts.CanRefresh([archived], Given.User, links));
    }

    [Fact]
    public void RefreshIsOfferedForAnOwnUnarchivedLinkedAccountWhateverTheHoldingType()
    {
        var account = Given.AnAccount();
        var retyped = Given.Linked(account) with { Type = HoldingType.Cash };
        var links = new Dictionary<Guid, Holding> { [account.Id] = retyped };

        Assert.True(InvestmentLinkAccounts.CanRefresh([account], Given.User, links));
    }

    [Fact]
    public void AHoldingLinkedToADeletedAccountSaysSo()
    {
        var deleted = Given.AnAccount("Gone");
        deleted.ApplyAll(InvestmentAccount.Decide(deleted,
            new DeleteInvestmentAccount(Guid.NewGuid(), Given.User, deleted.Id), DateTimeOffset.UnixEpoch));
        var kept = Given.AnAccount("Kept");

        var gone = InvestmentLinkAccounts.DeletedOf([deleted, kept]);

        Assert.Equal([deleted.Id], gone);
    }

    [Fact]
    public void RefreshSkipsAHoldingWhoseAccountIsDeleted()
    {
        var deleted = Given.AnAccount("Gone");
        var links = new Dictionary<Guid, Holding> { [deleted.Id] = Given.Linked(deleted) };
        deleted.ApplyAll(InvestmentAccount.Decide(deleted,
            new DeleteInvestmentAccount(Guid.NewGuid(), Given.User, deleted.Id), DateTimeOffset.UnixEpoch));

        Assert.Empty(InvestmentLinkAccounts.Select([deleted], Given.User, links, refresh: true));
        Assert.False(InvestmentLinkAccounts.CanRefresh([deleted], Given.User, links));
    }
}
