namespace PSPad.Module.Investments.Import;

internal static class XtbOrderFills
{
    static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    public static IReadOnlyList<XtbRow> Merge(IReadOnlyList<XtbRow> rows)
    {
        var merged = MergedPositions(rows);
        return merged.Count == 0
            ? rows
            : [.. rows.Select(row => row is { Kind: XtbRowKind.Buy or XtbRowKind.Sell, PositionId: { } position }
                && merged.TryGetValue(position, out var into)
                    ? row with { PositionId = into }
                    : row)];
    }

    static Dictionary<string, string> MergedPositions(IEnumerable<XtbRow> rows)
    {
        var orders = new List<List<(XtbRow Row, decimal Total)>>();
        var fills = rows
            .Where(row => row is { Kind: XtbRowKind.Buy, Errors.Count: 0, PositionId: not null, Quantity: not null })
            .Select(row => (Row: row, Total: XtbComment.Parse(row.Comment)?.OrderTotal))
            .Where(fill => fill.Total is not null)
            .OrderBy(fill => fill.Row.At).ThenBy(fill => fill.Row.SheetRow);

        foreach (var (row, total) in fills)
        {
            var order = orders.FirstOrDefault(order =>
                order[0].Row.Ticker == row.Ticker && order[0].Total == total
                && row.At - order[^1].Row.At <= Window
                && order.Sum(fill => fill.Row.Quantity!.Value) + row.Quantity!.Value <= total);
            if (order is null)
                orders.Add([(row, total!.Value)]);
            else
                order.Add((row, total!.Value));
        }

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var order in orders.Where(order => order.Select(fill => fill.Row.PositionId).Distinct().Count() > 1))
        {
            var smallest = order.Select(fill => fill.Row.PositionId!).MinBy(PositionKey)!;
            foreach (var fill in order.Where(fill => fill.Row.PositionId != smallest))
                merged.TryAdd(fill.Row.PositionId!, smallest);
        }

        return merged;
    }

    static (int, string) PositionKey(string position) => (position.Length, position);
}
