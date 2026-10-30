using PSPad.App.State.Investments;
using PSPad.TestInfrastructure;

namespace PSPad.App.Tests.State.Investments;

[UnitTest]
public class InvestmentLinkAccountsTests
{
    static readonly IReadOnlyDictionary<Guid, PSPad.Module.Money.Balance.Holding> NoLinks =
        new Dictionary<Guid, PSPad.Module.Money.Balance.Holding>();

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
        var links = new Dictionary<Guid, PSPad.Module.Money.Balance.Holding> { [account.Id] = holding };

        var row = Assert.Single(InvestmentLinkAccounts.Select([account], Given.User, links, refresh: false));

        Assert.Equal(holding, row.Existing);
    }

    [Fact]
    public void RefreshOffersOnlyTheLinkedAccounts()
    {
        var linked = Given.AnAccount("Linked");
        var other = Given.AnAccount("Other");
        var links = new Dictionary<Guid, PSPad.Module.Money.Balance.Holding> { [linked.Id] = Given.Linked(linked) };

        var rows = InvestmentLinkAccounts.Select([linked, other], Given.User, links, refresh: true);

        Assert.Equal(linked.Id, Assert.Single(rows).Account.Id);
    }

    [Fact]
    public void ALinkedArchivedAccountCannotBeRefreshed()
    {
        var archived = Given.AnAccount(archived: true);
        var links = new Dictionary<Guid, PSPad.Module.Money.Balance.Holding> { [archived.Id] = Given.Linked(archived) };

        Assert.Empty(InvestmentLinkAccounts.Select([archived], Given.User, links, refresh: true));
        Assert.False(InvestmentLinkAccounts.CanRefresh([archived], Given.User, links));
    }

    [Fact]
    public void RefreshIsOfferedForAnOwnUnarchivedLinkedAccountWhateverTheHoldingType()
    {
        var account = Given.AnAccount();
        var retyped = Given.Linked(account) with { Type = PSPad.Module.Money.Balance.HoldingType.Cash };
        var links = new Dictionary<Guid, PSPad.Module.Money.Balance.Holding> { [account.Id] = retyped };

        Assert.True(InvestmentLinkAccounts.CanRefresh([account], Given.User, links));
    }
}
