using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

public sealed record PortfolioFigures(
    decimal ValuePln, decimal CashPln, decimal PutInPln, decimal TakenOutPln, decimal MovedInPln, decimal MovedOutPln,
    decimal TotalProfitPln, decimal RealizedGainsPln, decimal RealizedLossesPln, decimal UnrealizedPln, decimal DividendsNetPln,
    decimal PassiveIncomePln, decimal FeesPln, decimal CurrencyDifferencePln, bool MissingRates)
{
    public IReadOnlyDictionary<Guid, PortfolioFigures> Accounts { get; init; } = new Dictionary<Guid, PortfolioFigures>();

    public decimal NetDepositsPln => PutInPln - TakenOutPln + MovedInPln - MovedOutPln;

    public decimal RealizedResultPln => RealizedGainsPln + RealizedLossesPln;

    public static PortfolioFigures For(
        IReadOnlyList<(InvestmentAccount Account, AccountFigures Figures, IReadOnlyList<InvestmentOperation> Operations)> accounts,
        FxBook fx, DateOnly today, IReadOnlySet<Guid>? internalTransfers = null)
    {
        var priced = accounts
            .Select(entry => (entry.Account, entry.Figures, Live: LiveOperations.Of(entry.Operations),
                Rate: fx.ToPln(entry.Figures.Currency, today)))
            .ToList();
        var included = priced.Where(entry => entry.Rate is not null).ToList();
        var moves = internalTransfers ?? InternalTransfers.Among(priced.Select(entry => (entry.Account, entry.Live)));
        var movesAmongIncluded = priced.Count == included.Count
            ? moves
            : InternalTransfers.Among(included.Select(entry => (entry.Account, entry.Live)));

        var byAccount = included.ToDictionary(entry => entry.Account.Id,
            entry => OneAccount(entry.Account, entry.Figures, entry.Live, entry.Rate!.Value, moves));
        var totaled = ReferenceEquals(moves, movesAmongIncluded)
            ? byAccount.Values.ToList()
            : included.Select(entry => OneAccount(entry.Account, entry.Figures, entry.Live, entry.Rate!.Value, movesAmongIncluded)).ToList();

        var value = totaled.Sum(account => account.ValuePln);
        var putIn = totaled.Sum(account => account.PutInPln);
        var takenOut = totaled.Sum(account => account.TakenOutPln);
        var gains = totaled.Sum(account => account.RealizedGainsPln);
        var losses = totaled.Sum(account => account.RealizedLossesPln);
        var unrealized = totaled.Sum(account => account.UnrealizedPln);
        var passive = totaled.Sum(account => account.PassiveIncomePln);
        var fees = totaled.Sum(account => account.FeesPln);
        var totalProfit = value - (putIn - takenOut);

        return new PortfolioFigures(value, totaled.Sum(account => account.CashPln), putIn, takenOut, 0m, 0m, totalProfit, gains,
            losses, unrealized, totaled.Sum(account => account.DividendsNetPln), passive, fees,
            totalProfit - (gains + losses + unrealized + passive - fees), priced.Count != included.Count)
        {
            Accounts = byAccount,
        };
    }

    static PortfolioFigures OneAccount(
        InvestmentAccount account, AccountFigures figures, IReadOnlyList<InvestmentOperation> live, decimal rate, IReadOnlySet<Guid> moves)
    {
        var cost = FxCost.Of(account.Currency, live);
        var external = CashFlows.External(live, moves);
        var results = Lots.Of(live).Closed.Select(result => Valuation.Money(result.Result * cost.RateOf(result.SellId))).ToList();

        var value = Valuation.Money(figures.Value * rate);
        var putIn = CashFlows.PutIn(external, cost.PlnOf);
        var takenOut = CashFlows.TakenOut(external, cost.PlnOf);
        var movedIn = CashFlows.MovedIn(live, moves, cost.PlnOf);
        var movedOut = CashFlows.MovedOut(live, moves, cost.PlnOf);
        var gains = results.Where(result => result > 0).Sum();
        var losses = results.Where(result => result < 0).Sum();
        var unrealized = Valuation.Money(figures.Unrealized * rate);
        var passive = CashFlows.PassiveIncome(live, cost.PlnOf);
        var fees = LiveOperations.Sum(live, OperationKind.Fee, cost.PlnOf);
        var totalProfit = value - (putIn - takenOut + movedIn - movedOut);

        return new PortfolioFigures(value, Valuation.Money(figures.Cash * rate), putIn, takenOut, movedIn, movedOut, totalProfit,
            gains, losses, unrealized, CashFlows.DividendsNet(live, cost.PlnOf), passive, fees,
            totalProfit - (gains + losses + unrealized + passive - fees), false);
    }
}
