namespace PSPad.Module.Money.Entries;

public sealed record ReturnState(IReadOnlyList<MoneyEntry> Returns, decimal Returned, decimal Left)
{
    public bool IsFull => Left <= 0;

    public static ReturnState Of(MoneyEntry purchase, IEnumerable<MoneyEntry> entries)
    {
        MoneyEntry[] returns =
        [
            .. entries
                .Where(entry => !entry.Deleted && entry.ReturnOf == purchase.Id)
                .OrderByDescending(entry => entry.Date)
                .ThenByDescending(entry => entry.RecordedAt)
        ];
        var returned = returns.Sum(entry => entry.Money.Amount);
        return new ReturnState(returns, returned, purchase.Money.Amount - returned);
    }
}
