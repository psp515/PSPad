using PSPad.Abstractions;
using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Instruments;
using PSPad.Module.Investments.Portfolio;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;

namespace PSPad.Module.Investments.Tests.Portfolio;

[UnitTest]
public class InvestmentPortfolioTests
{
    static CreateInvestmentAccount ACreate() =>
        new(Guid.NewGuid(), Owner, Guid.NewGuid(), "IKE", "PLN", Broker.Xtb, null);

    [Fact]
    public void TheFirstAccountStartsThePortfolio()
    {
        var command = ACreate();
        var portfolio = new InvestmentPortfolio();

        portfolio.ApplyAll(InvestmentPortfolio.Decide(null, command, Now));

        Assert.Equal(InvestmentPortfolio.IdFor(Owner), portfolio.Id);
        Assert.Equal(Owner, portfolio.UserId);
        Assert.Equal([command.AccountId], portfolio.AccountIds);
    }

    [Fact]
    public void The32ndAccountIsAllowed() =>
        Assert.Single(InvestmentPortfolio.Decide(APortfolioWith(accounts: 31), ACreate(), Now));

    [Fact]
    public void The33rdAccountIsRejected()
    {
        var portfolio = APortfolioWith(accounts: 32);
        var command = new CreateInvestmentAccount(Guid.NewGuid(), Owner, Guid.NewGuid(), "IKE", "PLN", Broker.Xtb, null);

        var rejection = Assert.Throws<DomainRejectedException>(() => InvestmentPortfolio.Decide(portfolio, command, Now));

        Assert.Equal("You can have at most 32 investment accounts.", rejection.Message);
    }

    [Fact]
    public void ANewTickerIsCountedUpperCased()
    {
        var portfolio = APortfolioWith();

        portfolio.ApplyAll(InvestmentPortfolio.Decide(portfolio, new EnsureInstrument(Guid.NewGuid(), Owner, " abc.us ", "Abc"), Now));

        Assert.Equal(["ABC.US"], portfolio.Tickers);
    }

    [Fact]
    public void A501stInstrumentIsRejected()
    {
        var portfolio = APortfolioWith(tickers: 500);

        var rejection = Assert.Throws<DomainRejectedException>(() =>
            InvestmentPortfolio.Decide(portfolio, new EnsureInstrument(Guid.NewGuid(), Owner, "NEW.US", "New"), Now));

        Assert.Equal("You can have at most 500 instruments.", rejection.Message);
    }

    [Fact]
    public void AKnownTickerIsNotCountedTwice()
    {
        var portfolio = APortfolioWith(tickers: 1);

        Assert.Empty(InvestmentPortfolio.Decide(portfolio, new EnsureInstrument(Guid.NewGuid(), Owner, "t0.us", "Again"), Now));
    }

    [Fact]
    public void EveryUserHasTheirOwnPortfolioId() =>
        Assert.NotEqual(InvestmentPortfolio.IdFor(Owner), InvestmentPortfolio.IdFor(Stranger));

    [Fact]
    public void ThePortfolioIdIsNotTheUserId() =>
        Assert.NotEqual(Owner, InvestmentPortfolio.IdFor(Owner));
}
