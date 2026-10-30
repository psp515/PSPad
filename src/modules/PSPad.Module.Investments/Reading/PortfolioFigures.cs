using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

public sealed record PortfolioFigures(
    decimal ValuePln, decimal NetDepositsPln, decimal TotalProfitPln, decimal RealizedGainsPln, decimal RealizedLossesPln,
    decimal UnrealizedPln, decimal PassiveIncomePln, decimal FeesPln, decimal CurrencyDifferencePln, bool MissingRates)
{
    public static PortfolioFigures For(
        IReadOnlyList<(AccountFigures Figures, IReadOnlyList<InvestmentOperation> Operations)> accounts, FxBook fx,
        DateOnly today)
    {
        var value = 0m;
        var netDeposits = 0m;
        var gains = 0m;
        var losses = 0m;
        var unrealized = 0m;
        var passive = 0m;
        var fees = 0m;
        var missingRates = false;

        foreach (var (figures, operations) in accounts)
        {
            if (fx.ToPln(figures.Currency, today) is not { } rate)
            {
                missingRates = true;
                continue;
            }

            var live = LiveOperations.Of(operations);
            var rateOfSell = live.Where(operation => operation.Kind == OperationKind.Sell)
                .ToDictionary(operation => operation.Id, operation => operation.Money.RateToPln);
            var results = Lots.Of(live).Closed.Select(result => Valuation.Money(result.Result * rateOfSell[result.SellId])).ToList();

            value += Valuation.Money(figures.Value * rate);
            unrealized += Valuation.Money(figures.Unrealized * rate);
            netDeposits += AccountFigures.NetDepositsOf(live, InPln);
            passive += AccountFigures.PassiveIncomeOf(live, InPln);
            fees += LiveOperations.Sum(live, OperationKind.Fee, InPln);
            gains += results.Where(result => result > 0).Sum();
            losses += results.Where(result => result < 0).Sum();
        }

        var totalProfit = value - netDeposits;
        return new PortfolioFigures(value, netDeposits, totalProfit, gains, losses, unrealized, passive, fees,
            totalProfit - (gains + losses + unrealized + passive - fees), missingRates);
    }

    static decimal InPln(InvestmentOperation operation) => operation.Money.InPln;
}
