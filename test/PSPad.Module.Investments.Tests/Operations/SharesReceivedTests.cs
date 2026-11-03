using PSPad.Module.Investments.Accounts;
using System.Globalization;
using PSPad.Abstractions;
using PSPad.Module.Investments.Operations;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;

namespace PSPad.Module.Investments.Tests.Operations;

[UnitTest]
public class SharesReceivedTests
{
    static RecordInvestmentOperation Received(
        InvestmentAccount account, decimal amount = 0.04m, string? ticker = "NEW.PL", decimal? quantity = 4m,
        decimal? price = 0.01m) =>
        ABuyCommand(account, ticker: ticker, quantity: quantity, price: price, money: Cash(amount, account.Currency),
            kind: OperationKind.SharesReceived) with { Note = "spinoff" };

    [Fact]
    public void SharesReceivedKeepTickerQuantityPriceAndNoteAndMoveNoCash()
    {
        var account = AnAccount();

        var received = Recorded(account, Received(account));

        Assert.Equal(OperationKind.SharesReceived, received.Kind);
        Assert.Equal("NEW.PL", received.Ticker);
        Assert.Equal(4m, received.Quantity);
        Assert.Equal(0.01m, received.Price);
        Assert.Equal(0.04m, received.Money.Amount);
        Assert.Equal("spinoff", received.Note);
        Assert.Equal(0m, received.SignedAmount);
    }

    [Fact]
    public void SharesReceivedAtNoCostAreAllowed()
    {
        var account = AnAccount("USD");

        var received = Recorded(account, Received(account, amount: 0m));

        Assert.Equal(0m, received.Money.Amount);
        Assert.Equal("USD", received.Money.Currency);
        Assert.Equal(4m, received.Money.RateToPln);
    }

    [Fact]
    public void OnlySharesReceivedMayCostNothing()
    {
        var account = AnAccount();

        Assert.Throws<DomainRejectedException>(() =>
            InvestmentOperation.Decide(null, account, ABuyCommand(account, money: Cash(0m)), Now));
    }

    [Theory]
    [InlineData(null, 4.0, 0.01)]
    [InlineData("NEW.PL", null, 0.01)]
    [InlineData("NEW.PL", 4.0, null)]
    public void SharesReceivedNeedATickerAQuantityAndAPrice(string? ticker, double? quantity, double? price)
    {
        var account = AnAccount();

        Assert.Throws<DomainRejectedException>(() => InvestmentOperation.Decide(null, account,
            Received(account, ticker: ticker, quantity: (decimal?)quantity, price: (decimal?)price), Now));
    }

    [Fact]
    public void ImportedSharesReceivedTakeTheirIdFromThePositionNotACashRow()
    {
        var account = AnAccount();
        const long position = 700000001;
        var external = position.ToString(CultureInfo.InvariantCulture);
        var command = Received(account) with
        {
            OperationId = InvestmentOperation.ReceivedIdFor(account.Id, position),
            Source = OperationSource.Xtb,
            ExternalId = external,
            PositionId = external
        };

        var received = Recorded(account, command);

        Assert.Equal(InvestmentOperation.ReceivedIdFor(account.Id, position), received.Id);
        Assert.NotEqual(InvestmentOperation.IdFor(account.Id, position), received.Id);
        Assert.Throws<DomainRejectedException>(() => InvestmentOperation.Decide(null, account,
            command with { OperationId = InvestmentOperation.IdFor(account.Id, position) }, Now));
    }
}
