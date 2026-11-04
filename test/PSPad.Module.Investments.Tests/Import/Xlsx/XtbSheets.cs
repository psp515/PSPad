namespace PSPad.Module.Investments.Tests.Import.Xlsx;

public static class XtbSheets
{
    public static readonly object?[] Header =
        ["Type", "Instrument", "Ticker", "Category", "Time", "Amount", "ID", "Comment", "Product", "Position ID"];

    public static object?[][] CashOperations(string account, params object?[][] rows) =>
        [
            ["Account number", account],
            ["Cash Operations"],
            ["Date from (UTC)", 38718.0],
            ["Date to (UTC)", 46324.79],
            Header,
            .. rows,
            ["Total", null, null, null, null, 0.0],
        ];

    public static object?[] Row(string type, string? instrument, string? ticker, double? time, double? amount,
        object? id, string? comment, string? product, object? positionId) =>
        [type, instrument, ticker, instrument is null ? null : "STOCK", time, amount, id, comment, product, positionId];
}
