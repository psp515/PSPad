namespace PSPad.Module.Investments.Import;

internal static class XtbReceivedShares
{
    public const string Type = "Shares received";

    public static IReadOnlyList<XtbRow> Of(XtbFile file)
    {
        var purchased = file.Rows.Where(row => row.Kind == XtbRowKind.Buy)
            .SelectMany(row => new[] { row.PositionId, row.FilledPositionId })
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var start = file.Rows.Min(row => row.At);
        var end = file.Rows.Max(row => row.At);
        if (start is null || end is null)
        {
            return [];
        }

        var held = (file.OpenPositions ?? []).Select(position => new XtbClosedPosition(position.PositionId, position.Ticker,
            position.Instrument, position.Product, position.Volume, position.OpenPrice, position.OpenTime, null, position.Type,
            position.Category));
        var next = file.Rows.Max(row => row.SheetRow) + 1;
        return [.. file.ClosedPositions.Concat(held)
            .Where(position => IsStock(position) && !purchased.Contains(position.PositionId)
                && position.OpenTime >= start && position.OpenTime <= end)
            .GroupBy(position => position.PositionId, StringComparer.Ordinal)
            .Select(Merged)
            .OrderBy(position => position.OpenTime).ThenBy(position => position.PositionId, StringComparer.Ordinal)
            .Select((position, index) => RowOf(position, next + index))];
    }

    static bool IsStock(XtbClosedPosition position) =>
        (position.Type is null || string.Equals(position.Type, "BUY", StringComparison.OrdinalIgnoreCase))
        && position.Category?.Contains("CFD", StringComparison.OrdinalIgnoreCase) != true;

    static XtbClosedPosition Merged(IGrouping<string, XtbClosedPosition> parts)
    {
        var first = parts.First();
        return first with
        {
            Volume = parts.Sum(part => part.Volume),
            Instrument = parts.Select(part => part.Instrument).FirstOrDefault(name => name is not null),
            Product = parts.Select(part => part.Product).FirstOrDefault(product => product is not null),
            Comment = parts.Select(part => part.Comment).FirstOrDefault(comment => comment is not null)
        };
    }

    static XtbRow RowOf(XtbClosedPosition position, int sheetRow)
    {
        var errors = new List<string>();
        if (position.OpenPrice is not > 0)
        {
            errors.Add("The position has no open price.");
        }

        if (string.IsNullOrWhiteSpace(position.Ticker))
        {
            errors.Add("The row has no ticker.");
        }

        var price = position.OpenPrice is > 0 ? position.OpenPrice : null;
        var amount = price is { } known ? Math.Round(position.Volume * known, 2, MidpointRounding.AwayFromZero) : (decimal?)null;
        return new XtbRow(sheetRow, Type, XtbRowKind.SharesReceived, position.Instrument,
            string.IsNullOrWhiteSpace(position.Ticker) ? null : position.Ticker, position.OpenTime, amount, position.PositionId,
            position.Comment, position.Product, position.PositionId, position.Volume, price, errors);
    }
}
