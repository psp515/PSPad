using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Portfolio;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;

namespace PSPad.Module.Investments.Tests.Accounts;

[UnitTest]
public class DeleteInvestmentAccountTests
{
    static DeleteInvestmentAccount ADelete(InvestmentAccount account, Guid? by = null) =>
        new(Guid.NewGuid(), by ?? Owner, account.Id);

    static InvestmentAccount Deleted(InvestmentAccount account)
    {
        account.ApplyAll(InvestmentAccount.Decide(account, ADelete(account), Now));
        return account;
    }

    [Fact]
    public void DeletingMarksTheAccountDeleted()
    {
        var account = AnAccount();

        var events = InvestmentAccount.Decide(account, ADelete(account), Now);
        account.ApplyAll(events);

        Assert.IsType<InvestmentAccountDeleted>(Assert.Single(events));
        Assert.True(account.Deleted);
    }

    [Fact]
    public void AnArchivedAccountCanBeDeleted() =>
        Assert.True(Deleted(Archive(AnAccount())).Deleted);

    [Fact]
    public void DeletingTwiceChangesNothing() =>
        Assert.Empty(Again(Deleted(AnAccount())));

    static IReadOnlyList<DomainEvent> Again(InvestmentAccount account) => InvestmentAccount.Decide(account, ADelete(account), Now);

    [Fact]
    public void OnlyTheOwnerCanDelete()
    {
        var account = AnAccount();

        var rejection = Assert.Throws<DomainRejectedException>(
            () => InvestmentAccount.Decide(account, ADelete(account, Stranger), Now));

        Assert.Equal("That account does not exist.", rejection.Message);
    }

    [Fact]
    public void ADeletedAccountTakesNoOperations()
    {
        var account = Deleted(AnAccount());

        var rejection = Assert.Throws<DomainRejectedException>(
            () => InvestmentOperation.Decide(null, account, ABuyCommand(account), Now));

        Assert.Equal("That account does not exist.", rejection.Message);
    }

    [Fact]
    public void ADeletedAccountCannotBeRenamed() =>
        Assert.Throws<DomainRejectedException>(() => InvestmentAccount.Decide(Deleted(AnAccount()),
            new RenameInvestmentAccount(Guid.NewGuid(), Owner, Guid.Empty, "Main"), Now));

    [Fact]
    public void DeletingFreesTheAccountsPlaceInThePortfolio()
    {
        var portfolio = APortfolioWith(accounts: 32);
        var deleted = portfolio.AccountIds[5];

        portfolio.ApplyAll(InvestmentPortfolio.Decide(portfolio, new DeleteInvestmentAccount(Guid.NewGuid(), Owner, deleted), Now));

        Assert.Equal(31, portfolio.AccountIds.Count);
        Assert.DoesNotContain(deleted, portfolio.AccountIds);
        Assert.Single(InvestmentPortfolio.Decide(portfolio,
            new CreateInvestmentAccount(Guid.NewGuid(), Owner, Guid.NewGuid(), "IKE", "PLN", Broker.Xtb, null), Now));
    }

    [Fact]
    public void DeletingAnAccountThePortfolioDoesNotHoldChangesNothing() =>
        Assert.Empty(InvestmentPortfolio.Decide(APortfolioWith(accounts: 1),
            new DeleteInvestmentAccount(Guid.NewGuid(), Owner, Guid.NewGuid()), Now));
}
