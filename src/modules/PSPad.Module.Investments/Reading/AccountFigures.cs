using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

public sealed record AccountFigures(
    Guid AccountId, string Currency, decimal PutIn, decimal TakenOut, decimal Cash, decimal PositionsValue, decimal Value,
    decimal TotalProfit, decimal RealizedGains, decimal RealizedLosses, decimal Unrealized, decimal DividendsNet,
    decimal PassiveIncome, decimal Fees, decimal? AverageRate, decimal? PlnSpent, IReadOnlyList<Position> Positions)
{
    public decimal NetDeposits => PutIn - TakenOut;

    public decimal RealizedResult => RealizedGains + RealizedLosses;

    public static AccountFigures For(
        InvestmentAccount account, IReadOnlyList<InvestmentOperation> operations,
        IReadOnlyDictionary<string, PriceHistory> pricesByTicker, FxBook fx, DateOnly day)
    {
        var live = LiveOperations.Of(operations);
        var (open, closed, oversold) = Lots.Of(live);
        var positions = Valuation.Positions(account.Id, account.Currency, open, oversold, pricesByTicker, fx, day);
        var putIn = CashFlows.PutIn(live, Amount);
        var takenOut = CashFlows.TakenOut(live, Amount);
        var cash = live.Sum(operation => operation.SignedAmount);
        var positionsValue = positions.Sum(position => position.Value);
        var value = cash + positionsValue;
        var fxCost = FxCost.Of(account.Currency, live);

        return new AccountFigures(account.Id, account.Currency, putIn, takenOut, cash, positionsValue, value,
            value - (putIn - takenOut),
            closed.Where(result => result.Result > 0).Sum(result => result.Result),
            closed.Where(result => result.Result < 0).Sum(result => result.Result),
            positions.Sum(position => position.Unrealized),
            CashFlows.DividendsNet(live, Amount),
            CashFlows.PassiveIncome(live, Amount),
            LiveOperations.Sum(live, OperationKind.Fee, Amount),
            fxCost.AverageRate, fxCost.PlnSpent,
            positions);
    }

    static decimal Amount(InvestmentOperation operation) => operation.Money.Amount;
}
