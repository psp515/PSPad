using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

internal static class CashFlows
{
    public static bool IsPutIn(OperationKind kind) => kind is OperationKind.Deposit or OperationKind.TransferIn;

    public static bool IsTakenOut(OperationKind kind) => kind is OperationKind.Withdrawal or OperationKind.TransferOut;

    public static decimal NetDepositOf(InvestmentOperation operation, decimal amount) =>
        IsPutIn(operation.Kind) ? amount : IsTakenOut(operation.Kind) ? -amount : 0m;

    public static decimal PutIn(IEnumerable<InvestmentOperation> live, Func<InvestmentOperation, decimal> amount) =>
        live.Where(operation => IsPutIn(operation.Kind)).Sum(amount);

    public static decimal TakenOut(IEnumerable<InvestmentOperation> live, Func<InvestmentOperation, decimal> amount) =>
        live.Where(operation => IsTakenOut(operation.Kind)).Sum(amount);

    public static decimal DividendsNet(IReadOnlyList<InvestmentOperation> live, Func<InvestmentOperation, decimal> amount) =>
        LiveOperations.Sum(live, OperationKind.Dividend, amount)
        - live.Where(operation => operation is { Kind: OperationKind.Tax, Ticker: not null }).Sum(amount)
        + LiveOperations.Sum(live, OperationKind.TaxRefund, amount);

    public static decimal PassiveIncome(IReadOnlyList<InvestmentOperation> live, Func<InvestmentOperation, decimal> amount) =>
        DividendsNet(live, amount) + LiveOperations.Sum(live, OperationKind.Interest, amount)
        - live.Where(operation => operation is { Kind: OperationKind.Tax, Ticker: null }).Sum(amount);
}
