using PSPad.Module.Investments.Operations;

namespace PSPad.Module.Investments.Reading;

internal sealed class LotBook
{
    readonly List<OpenLot> open = [];
    readonly List<ClosedResult> closed = [];
    readonly Dictionary<string, decimal> oversold = new(StringComparer.Ordinal);

    public IReadOnlyList<ClosedResult> Closed => closed;

    public IReadOnlyDictionary<string, decimal> Oversold => oversold;

    public IReadOnlyList<Lot> Open => open.Select(lot => new Lot(lot.Key, lot.Ticker, lot.OpenedAt, lot.Quantity, lot.Cost)).ToList();

    public void Apply(InvestmentOperation operation)
    {
        if (operation.Ticker is not { } ticker || operation.Quantity is not { } quantity)
        {
            return;
        }

        switch (operation.Kind)
        {
            case OperationKind.Buy:
                Buy(operation, ticker, quantity);
                break;
            case OperationKind.Sell:
                Sell(operation, ticker, quantity);
                break;
        }
    }

    void Buy(InvestmentOperation operation, string ticker, decimal quantity)
    {
        var key = operation.PositionId ?? operation.Id.ToString();
        if (open.Find(lot => lot.Ticker == ticker && lot.Key == key) is { } lot)
        {
            lot.Quantity += quantity;
            lot.Cost += operation.Money.Amount;
            return;
        }

        open.Add(new OpenLot(key, ticker, operation.At) { Quantity = quantity, Cost = operation.Money.Amount });
    }

    void Sell(InvestmentOperation operation, string ticker, decimal quantity)
    {
        var closing = open.Where(lot => lot.Ticker == ticker && (operation.PositionId is null || lot.Key == operation.PositionId))
            .ToList();
        var remaining = quantity;
        var closedCost = 0m;
        foreach (var lot in closing.TakeWhile(_ => remaining > 0))
        {
            var taken = Math.Min(remaining, lot.Quantity);
            var cost = taken == lot.Quantity
                ? lot.Cost
                : Math.Round(lot.Cost * taken / lot.Quantity, 2, MidpointRounding.AwayFromZero);
            lot.Quantity -= taken;
            lot.Cost -= cost;
            remaining -= taken;
            closedCost += cost;
            if (lot.Quantity == 0)
            {
                open.Remove(lot);
            }
        }

        if (remaining > 0)
        {
            oversold[ticker] = oversold.GetValueOrDefault(ticker) + remaining;
        }

        closed.Add(new ClosedResult(operation.Id, ticker, operation.Money.Amount, closedCost));
    }

    sealed class OpenLot(string key, string ticker, DateTimeOffset openedAt)
    {
        public string Key { get; } = key;

        public string Ticker { get; } = ticker;

        public DateTimeOffset OpenedAt { get; } = openedAt;

        public decimal Quantity { get; set; }

        public decimal Cost { get; set; }
    }
}
