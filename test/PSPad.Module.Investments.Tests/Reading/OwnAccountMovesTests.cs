using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;
using PSPad.Module.Investments.Reading;
using PSPad.TestInfrastructure;
using static PSPad.Module.Investments.Tests.Given;
using static PSPad.Module.Investments.Tests.Reading.Market;

namespace PSPad.Module.Investments.Tests.Reading;

[UnitTest]
public class OwnAccountMovesTests
{
    static readonly FxBook Today = Rates(("USD", Day(1), 3.80m), ("USD", Day(20), 4.00m));

    readonly InvestmentAccount _plnAccount = AnAccount(number: "1001");
    readonly InvestmentAccount _usdAccount = AnAccount("USD", number: "1002");
    readonly InvestmentAccount _ikeAccount = AnAccount(number: "1003");
    readonly Dictionary<Guid, InvestmentOperation[]> _operations = [];

    public OwnAccountMovesTests()
    {
        var at = new DateTimeOffset(Day(4).ToDateTime(new TimeOnly(10, 0)), TimeSpan.Zero);
        _operations[_plnAccount.Id] = [
            Deposit(_plnAccount, 10000m, Day(1)),
            TransferOut(_plnAccount, TransferType.CurrencyConversion, 3700m, Day(2), counterparty: "1002",
                counter: Foreign(1000m, "USD", 3.7m, Day(2))),
            TransferOut(_plnAccount, TransferType.Subaccount, 100m, Day(3), counterparty: "9999"),
            TransferAt(_plnAccount, OperationKind.TransferOut, TransferType.Ike, 500m, at),
            TransferIn(_plnAccount, TransferType.Subaccount, 50m, Day(5), counterparty: "1003"),
        ];
        _operations[_usdAccount.Id] = [
            TransferIn(_usdAccount, TransferType.CurrencyConversion, 1000m, Day(2), counterparty: "1001",
                counter: Pln(3700m, Day(2)), rate: 3.80m),
        ];
        _operations[_ikeAccount.Id] = [
            TransferAt(_ikeAccount, OperationKind.TransferIn, TransferType.Ike, 500m, at.AddHours(2)),
            TransferOut(_ikeAccount, TransferType.Subaccount, 50m, Day(5), counterparty: "1001"),
        ];
        Archive(_ikeAccount);
    }

    IReadOnlySet<Guid> Internal() =>
        InternalTransfers.Among([.. Accounts.Select(account => (account, (IReadOnlyList<InvestmentOperation>)_operations[account.Id]))]);

    IEnumerable<InvestmentAccount> Accounts => [_plnAccount, _usdAccount, _ikeAccount];

    AccountFigures Figures(InvestmentAccount account, IReadOnlySet<Guid> moves) =>
        AccountFigures.For(account, _operations[account.Id], Prices(), Today, Day(20), moves);

    static void IdentityHolds(AccountFigures figures) =>
        Assert.Equal(figures.RealizedGains + figures.RealizedLosses + figures.Unrealized + figures.PassiveIncome - figures.Fees,
            figures.TotalProfit);

    [Fact]
    public void AnAccountCountsMovesToItsOtherOwnAccountsAsMovedOutNotTakenOut()
    {
        var figures = Figures(_plnAccount, Internal());

        Assert.Equal(10000m, figures.PutIn);
        Assert.Equal(100m, figures.TakenOut);
        Assert.Equal(50m, figures.MovedIn);
        Assert.Equal(4200m, figures.MovedOut);
        Assert.Equal(5750m, figures.NetDeposits);
        IdentityHolds(figures);
    }

    [Fact]
    public void AConversionAndAnIkeTransferInFromOwnAccountsAreMovedInNotPutIn()
    {
        var moves = Internal();
        var usd = Figures(_usdAccount, moves);
        var ike = Figures(_ikeAccount, moves);

        Assert.Equal(0m, usd.PutIn);
        Assert.Equal(1000m, usd.MovedIn);
        Assert.Equal(1000m, usd.NetDeposits);
        Assert.Equal(0m, ike.PutIn);
        Assert.Equal(500m, ike.MovedIn);
        Assert.Equal(50m, ike.MovedOut);
        Assert.Equal(450m, ike.NetDeposits);
        IdentityHolds(usd);
        IdentityHolds(ike);
    }

    [Fact]
    public void MovesChangeNeitherNetDepositsNorTotalProfit()
    {
        var moves = Internal();

        foreach (var account in Accounts)
        {
            var plain = Figures(account, NoMoves);
            var split = Figures(account, moves);
            Assert.Equal(plain.NetDeposits, split.NetDeposits);
            Assert.Equal(plain.TotalProfit, split.TotalProfit);
            Assert.Equal(plain.PutIn + plain.TakenOut, split.PutIn + split.TakenOut + split.MovedIn + split.MovedOut);
        }
    }

    [Fact]
    public void WithoutTheOwnAccountsEveryTransferIsPutInOrTakenOut()
    {
        var figures = Figures(_plnAccount, NoMoves);

        Assert.Equal(10050m, figures.PutIn);
        Assert.Equal(4300m, figures.TakenOut);
        Assert.Equal(0m, figures.MovedIn);
        Assert.Equal(0m, figures.MovedOut);
    }

    [Fact]
    public void EachAccountHasItsOwnPlnFiguresWithItsMovesKeptApart()
    {
        var moves = Internal();
        var entries = Accounts.Select(account =>
            (account, Figures(account, moves), (IReadOnlyList<InvestmentOperation>)_operations[account.Id])).ToList();

        var portfolio = PortfolioFigures.For(entries, Today, Day(20), moves);

        var pln = portfolio.Accounts[_plnAccount.Id];
        var usd = portfolio.Accounts[_usdAccount.Id];
        Assert.Equal(10000m, pln.PutInPln);
        Assert.Equal(100m, pln.TakenOutPln);
        Assert.Equal(50m, pln.MovedInPln);
        Assert.Equal(4200m, pln.MovedOutPln);
        Assert.Equal(0m, usd.PutInPln);
        Assert.Equal(3700m, usd.MovedInPln);
        Assert.Equal(4000m, usd.ValuePln);
        Assert.Equal(4000m, usd.CashPln);
        Assert.Equal(300m, usd.TotalProfitPln);
        Assert.Equal(300m, usd.CurrencyDifferencePln);
        Assert.Equal(portfolio.PutInPln, portfolio.Accounts.Values.Sum(account => account.PutInPln));
        Assert.Equal(portfolio.TakenOutPln, portfolio.Accounts.Values.Sum(account => account.TakenOutPln));
        Assert.Equal(portfolio.ValuePln, portfolio.Accounts.Values.Sum(account => account.ValuePln));
        Assert.Equal(0m, portfolio.MovedInPln);
        Assert.Equal(0m, portfolio.MovedOutPln);
    }

    [Fact]
    public void ASingleCurrencyAccountsPlnFiguresAreTheAllAccountsFigures()
    {
        var account = AnAccount("USD");
        var operations = new[] {
            TransferIn(account, TransferType.CurrencyConversion, 1000m, Day(1), counter: Pln(3700m, Day(1)), rate: 3.80m),
            Buy(account, "ABC.US", position: "A", quantity: 10m, price: 50m, amount: 500m, Day(2), rate: 3.80m),
            Sell(account, "ABC.US", position: "A", quantity: 4m, price: 60m, amount: 240m, Day(3), rate: 3.80m),
            Dividend(account, "ABC.US", 10m, Day(4), rate: 3.80m),
            Fee(account, 2m, Day(5), rate: 3.80m),
            Withdrawal(account, 100m, Day(6), rate: 3.80m),
        };
        var prices = Prices(("ABC.US", "USD", (Day(20), 55m)));
        var figures = AccountFigures.For(account, operations, prices, Today, Day(20), NoMoves);

        var portfolio = PortfolioFigures.For([(account, figures, operations)], Today, Day(20), NoMoves);

        Assert.Equal(portfolio with { Accounts = portfolio.Accounts }, portfolio.Accounts[account.Id] with { Accounts = portfolio.Accounts });
    }
}
