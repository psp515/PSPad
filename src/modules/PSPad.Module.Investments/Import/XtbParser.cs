using System.Globalization;

namespace PSPad.Module.Investments.Import;

public static class XtbParser
{
    public const int MaxRows = 20_000;

    static readonly string[] CashHeaders = ["Type", "Instrument", "Ticker", "Time", "Amount", "ID", "Comment", "Product", "Position ID"];

    public static XtbFile Parse(XlsxBook book)
    {
        foreach (var sheet in book.Sheets)
        {
            var headerRow = FindHeaderRow(sheet, CashHeaders);
            if (headerRow >= 0)
                return ParseCash(book, sheet, headerRow);
        }
        throw new XtbFileError("This file has no Cash Operations sheet.");
    }

    static XtbFile ParseCash(XlsxBook book, XlsxSheet sheet, int headerRow)
    {
        var account = FindAccountNumber(sheet, headerRow)
            ?? throw new XtbFileError("This file has no account number.");
        var columns = Columns(sheet.Rows[headerRow]);
        var rows = new List<XtbRow>();
        for (var i = headerRow + 1; i < sheet.Rows.Count; i++)
        {
            var cells = sheet.Rows[i];
            if (cells.All(IsBlank))
                continue;
            var type = TextOf(Cell(cells, columns["Type"])) ?? "";
            if (type == "Total")
                continue;
            if (rows.Count == MaxRows)
                throw new XtbFileError("This file has more than 20,000 rows. Export a shorter period.");
            rows.Add(ParseRow(i + 1, type, cells, columns));
        }
        var product = rows.Select(r => r.Product).FirstOrDefault(p => p is not null);
        return new XtbFile(account, product, rows, FindOpenPositions(book, sheet));
    }

    static XtbRow ParseRow(int sheetRow, string type, IReadOnlyList<XlsxCell?> cells, Dictionary<string, int> columns)
    {
        var errors = new List<string>();
        var kind = XtbTypeMap.KindOf(type);
        if (kind is null)
            errors.Add($"Unknown type '{type}'.");

        var ticker = TextOf(Cell(cells, columns["Ticker"]));
        var comment = TextOf(Cell(cells, columns["Comment"]));
        var positionId = TextOf(Cell(cells, columns["Position ID"]));
        var id = IdOf(Cell(cells, columns["ID"]));
        var at = TimeOf(Cell(cells, columns["Time"]));
        var amount = AmountOf(Cell(cells, columns["Amount"]));

        if (at is null)
            errors.Add("The row has no usable time.");
        decimal? absolute = null;
        if (amount is null or 0)
            errors.Add("The row has no usable amount.");
        else
        {
            absolute = Math.Round(Math.Abs(amount.Value), 2, MidpointRounding.AwayFromZero);
            if (kind is not null && !SignMatches(kind.Value, amount.Value))
                errors.Add($"The amount's sign does not match '{type}'.");
        }
        if (id is null)
            errors.Add("The row has no usable ID.");

        decimal? quantity = null, price = null;
        if (kind is XtbRowKind.Buy or XtbRowKind.Sell)
        {
            if (positionId is null)
                errors.Add("The row has no position ID.");
            var parsed = XtbComment.Parse(comment);
            if (parsed is null)
                errors.Add("The comment does not say how many shares.");
            else
                (quantity, price) = (parsed.Quantity, parsed.Price);
        }
        if (kind is XtbRowKind.Buy or XtbRowKind.Sell or XtbRowKind.Dividend or XtbRowKind.Tax && ticker is null)
            errors.Add("The row has no ticker.");

        return new XtbRow(sheetRow, type, kind, TextOf(Cell(cells, columns["Instrument"])), ticker, at, absolute, id, comment,
            TextOf(Cell(cells, columns["Product"])), positionId, quantity, price, errors);
    }

    static bool SignMatches(XtbRowKind kind, decimal amount) => kind switch
    {
        XtbRowKind.Buy or XtbRowKind.Withdrawal or XtbRowKind.Tax or XtbRowKind.Fee => amount < 0,
        _ => amount > 0,
    };

    static IReadOnlyList<XtbOpenPosition>? FindOpenPositions(XlsxBook book, XlsxSheet cash)
    {
        foreach (var sheet in book.Sheets.Where(s => !ReferenceEquals(s, cash)))
        {
            foreach (var symbolHeader in new[] { "Symbol", "Ticker" })
            {
                var headerRow = FindHeaderRow(sheet, ["Position", symbolHeader, "Volume"]);
                if (headerRow < 0)
                    continue;
                var columns = Columns(sheet.Rows[headerRow]);
                var positions = new List<XtbOpenPosition>();
                foreach (var cells in sheet.Rows.Skip(headerRow + 1))
                {
                    var positionId = TextOf(Cell(cells, columns["Position"]));
                    var volume = AmountOf(Cell(cells, columns["Volume"]));
                    if (positionId is not null && volume is not null)
                        positions.Add(new XtbOpenPosition(positionId, TextOf(Cell(cells, columns[symbolHeader])) ?? "", volume.Value));
                }
                return positions;
            }
        }
        return null;
    }

    static int FindHeaderRow(XlsxSheet sheet, string[] required)
    {
        for (var i = 0; i < sheet.Rows.Count; i++)
        {
            var headers = sheet.Rows[i].Select(TextOf).Where(t => t is not null).ToHashSet();
            if (required.All(headers.Contains))
                return i;
        }
        return -1;
    }

    static Dictionary<string, int> Columns(IReadOnlyList<XlsxCell?> header)
    {
        var columns = new Dictionary<string, int>();
        for (var c = 0; c < header.Count; c++)
            if (TextOf(header[c]) is { } name)
                columns.TryAdd(name, c);
        return columns;
    }

    static string? FindAccountNumber(XlsxSheet sheet, int headerRow)
    {
        for (var i = 0; i < headerRow; i++)
        {
            var cells = sheet.Rows[i];
            for (var c = 0; c < cells.Count - 1; c++)
                if (TextOf(cells[c]) == "Account number" && TextOf(cells[c + 1]) is { } number)
                    return number;
        }
        return null;
    }

    static XlsxCell? Cell(IReadOnlyList<XlsxCell?> cells, int column) =>
        column < cells.Count ? cells[column] : null;

    static bool IsBlank(XlsxCell? cell) => cell is null || (string.IsNullOrWhiteSpace(cell.Text) && cell.Number is null);

    static string? TextOf(XlsxCell? cell)
    {
        if (cell is null)
            return null;
        if (cell.Text is not null)
            return string.IsNullOrWhiteSpace(cell.Text) ? null : cell.Text.Trim();
        return cell.Number is { } n ? n.ToString("0.############", CultureInfo.InvariantCulture) : null;
    }

    static string? IdOf(XlsxCell? cell) =>
        TextOf(cell) is { } text && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id.ToString(CultureInfo.InvariantCulture)
            : null;

    static DateTimeOffset? TimeOf(XlsxCell? cell)
    {
        var serial = NumberOf(cell);
        return serial is > 0 and < 2_958_465 ? ExcelTime.ToUtc(serial.Value) : null;
    }

    static decimal? AmountOf(XlsxCell? cell)
    {
        var number = NumberOf(cell);
        return number is { } n && double.IsFinite(n) && Math.Abs(n) < 1e15 ? (decimal)n : null;
    }

    static double? NumberOf(XlsxCell? cell)
    {
        if (cell is null)
            return null;
        if (cell.Number is not null)
            return cell.Number;
        return double.TryParse(cell.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
    }
}
