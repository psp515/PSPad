using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

internal sealed class FxCost
{
    const string Pln = "PLN";

    readonly Dictionary<Guid, (decimal Pln, decimal Rate)> byOperation = [];

    FxCost()
    {
    }

    public decimal? AverageRate { get; private set; }

    public decimal? PlnSpent { get; private set; }

    public static FxCost Of(string currency, IReadOnlyList<InvestmentOperation> live)
    {
        var cost = new FxCost();
        if (string.Equals(currency, Pln, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var operation in live)
            {
                cost.byOperation[operation.Id] = (operation.Money.Amount, 1m);
            }

            return cost;
        }

        var units = 0m;
        var spent = 0m;
        foreach (var operation in live)
        {
            var amount = operation.Money.Amount;
            if (BuysCurrency(operation))
            {
                var paid = operation.CounterMoney!.Amount;
                cost.byOperation[operation.Id] = (paid, paid / amount);
                units += amount;
                spent += paid;
                continue;
            }

            var rate = units > 0 ? spent / units : operation.Money.RateToPln;
            cost.byOperation[operation.Id] = (Valuation.Money(amount * rate), rate);
            if (SellsCurrency(operation) && units > 0)
            {
                var sold = Math.Min(amount, units);
                spent -= sold * rate;
                units -= sold;
                if (units == 0)
                {
                    spent = 0m;
                }
            }
        }

        if (units > 0)
        {
            cost.AverageRate = spent / units;
            cost.PlnSpent = Valuation.Money(spent);
        }

        return cost;
    }

    public decimal PlnOf(InvestmentOperation operation) => byOperation[operation.Id].Pln;

    public decimal RateOf(Guid operationId) => byOperation[operationId].Rate;

    static bool BuysCurrency(InvestmentOperation operation) =>
        operation is { Kind: OperationKind.TransferIn, TransferType: TransferType.CurrencyConversion }
        && IsPln(operation.CounterMoney);

    static bool SellsCurrency(InvestmentOperation operation) =>
        operation is { Kind: OperationKind.TransferOut, TransferType: TransferType.CurrencyConversion }
        && IsPln(operation.CounterMoney);

    static bool IsPln(PSPad.Module.Money.Money? money) =>
        money is not null && string.Equals(money.Currency, Pln, StringComparison.OrdinalIgnoreCase);
}
