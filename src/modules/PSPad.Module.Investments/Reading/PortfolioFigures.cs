using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

public sealed record PortfolioFigures(
    decimal ValuePln, decimal PutInPln, decimal TakenOutPln, decimal TotalProfitPln, decimal RealizedGainsPln,
    decimal RealizedLossesPln, decimal UnrealizedPln, decimal DividendsNetPln, decimal PassiveIncomePln, decimal FeesPln,
    decimal CurrencyDifferencePln, bool MissingRates)
{
    public decimal NetDepositsPln => PutInPln - TakenOutPln;

    public decimal RealizedResultPln => RealizedGainsPln + RealizedLossesPln;

    public static PortfolioFigures For(
        IReadOnlyList<(InvestmentAccount Account, AccountFigures Figures, IReadOnlyList<InvestmentOperation> Operations)> accounts,
        FxBook fx, DateOnly today)
    {
        var priced = accounts
            .Select(entry => (entry.Account, entry.Figures, Live: LiveOperations.Of(entry.Operations),
                Rate: fx.ToPln(entry.Figures.Currency, today)))
            .ToList();
        var included = priced.Where(entry => entry.Rate is not null).ToList();
        var internalIds = InternalTransfers.Among(included.Select(entry => (entry.Account, entry.Live)));

        var value = 0m;
        var putIn = 0m;
        var takenOut = 0m;
        var gains = 0m;
        var losses = 0m;
        var unrealized = 0m;
        var dividendsNet = 0m;
        var passive = 0m;
        var fees = 0m;

        foreach (var (account, figures, live, rate) in included)
        {
            var cost = FxCost.Of(account.Currency, live);
            var external = live.Where(operation => !internalIds.Contains(operation.Id)).ToList();
            var results = Lots.Of(live).Closed.Select(result => Valuation.Money(result.Result * cost.RateOf(result.SellId))).ToList();

            value += Valuation.Money(figures.Value * rate!.Value);
            unrealized += Valuation.Money(figures.Unrealized * rate.Value);
            putIn += CashFlows.PutIn(external, cost.PlnOf);
            takenOut += CashFlows.TakenOut(external, cost.PlnOf);
            dividendsNet += CashFlows.DividendsNet(live, cost.PlnOf);
            passive += CashFlows.PassiveIncome(live, cost.PlnOf);
            fees += LiveOperations.Sum(live, OperationKind.Fee, cost.PlnOf);
            gains += results.Where(result => result > 0).Sum();
            losses += results.Where(result => result < 0).Sum();
        }

        var totalProfit = value - (putIn - takenOut);
        return new PortfolioFigures(value, putIn, takenOut, totalProfit, gains, losses, unrealized, dividendsNet, passive, fees,
            totalProfit - (gains + losses + unrealized + passive - fees), priced.Count != included.Count);
    }
}
