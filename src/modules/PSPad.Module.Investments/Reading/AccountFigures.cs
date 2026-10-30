using PSPad.Module.Investments.Accounts;
using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

public sealed record AccountFigures(
    Guid AccountId, string Currency, decimal NetDeposits, decimal Cash, decimal PositionsValue, decimal Value,
    decimal TotalProfit, decimal RealizedGains, decimal RealizedLosses, decimal Unrealized, decimal PassiveIncome,
    decimal Fees, IReadOnlyList<Position> Positions)
{
    public static AccountFigures For(
        InvestmentAccount account, IReadOnlyList<InvestmentOperation> operations,
        IReadOnlyDictionary<string, PriceHistory> pricesByTicker, FxBook fx, DateOnly day)
    {
        var live = LiveOperations.Of(operations);
        var (open, closed, oversold) = Lots.Of(live);
        var positions = Valuation.Positions(account.Id, account.Currency, open, oversold, pricesByTicker, fx, day);
        var netDeposits = NetDepositsOf(live, operation => operation.Money.Amount);
        var cash = live.Sum(operation => operation.SignedAmount);
        var positionsValue = positions.Sum(position => position.Value);
        var value = cash + positionsValue;

        return new AccountFigures(account.Id, account.Currency, netDeposits, cash, positionsValue, value, value - netDeposits,
            closed.Where(result => result.Result > 0).Sum(result => result.Result),
            closed.Where(result => result.Result < 0).Sum(result => result.Result),
            positions.Sum(position => position.Unrealized),
            PassiveIncomeOf(live, operation => operation.Money.Amount),
            LiveOperations.Sum(live, OperationKind.Fee, operation => operation.Money.Amount),
            positions);
    }

    internal static decimal NetDepositsOf(IReadOnlyList<InvestmentOperation> live, Func<InvestmentOperation, decimal> amount) =>
        LiveOperations.Sum(live, OperationKind.Deposit, amount) - LiveOperations.Sum(live, OperationKind.Withdrawal, amount);

    internal static decimal PassiveIncomeOf(IReadOnlyList<InvestmentOperation> live, Func<InvestmentOperation, decimal> amount) =>
        LiveOperations.Sum(live, OperationKind.Dividend, amount) - LiveOperations.Sum(live, OperationKind.Tax, amount)
        + LiveOperations.Sum(live, OperationKind.Interest, amount);
}
