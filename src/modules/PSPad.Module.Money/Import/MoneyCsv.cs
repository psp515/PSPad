using PSPad.Abstractions;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Import;

public static class MoneyCsv
{
    public const int MaxRows = 5000;

    static readonly string[] EntryColumns = ["kind", "date", "name", "category", "amount", "currency", "rate", "note"];
    static readonly string[] EntryRequired = ["kind", "date", "name", "category", "amount"];
    static readonly string[] BalanceColumns = ["month", "holding", "type", "amount", "currency", "rate"];
    static readonly string[] BalanceRequired = ["month", "holding", "type", "amount"];

    public static CsvParse<ParsedEntryRow> ParseEntries(string text) =>
        Parse(text, EntryColumns, EntryRequired, EntryRow);

    public static CsvParse<ParsedHoldingRow> ParseBalances(string text) =>
        Parse(text, BalanceColumns, BalanceRequired, HoldingRow);

    static CsvParse<TRow> Parse<TRow>(
        string text, string[] known, string[] required, Func<CsvRecord, TRow> read)
    {
        var table = CsvReader.Read(text);

        if (table.Error is not null)
        {
            return CsvParse<TRow>.Failed(table.Error);
        }

        var columns = new Dictionary<string, int>();
        for (var index = 0; index < table.Header.Count; index++)
        {
            var name = table.Header[index].Trim().ToLowerInvariant();

            if (known.Contains(name) && !columns.TryAdd(name, index))
            {
                return CsvParse<TRow>.Failed($"The column {name} appears twice.");
            }
        }

        var missing = required.FirstOrDefault(column => !columns.ContainsKey(column));

        if (missing is not null)
        {
            return CsvParse<TRow>.Failed($"The file has no {missing} column.");
        }

        if (table.Lines.Count > MaxRows)
        {
            return CsvParse<TRow>.Failed($"A file can hold at most {MaxRows} rows. Split it and import each part.");
        }

        var rows = new List<TRow>();
        var errors = new List<CsvRowError>();
        foreach (var line in table.Lines)
        {
            try
            {
                rows.Add(read(new CsvRecord(line, columns)));
            }
            catch (Exception rejection) when (rejection is FormatException or DomainRejectedException)
            {
                errors.Add(new CsvRowError(line.Number, rejection.Message));
            }
        }

        return new CsvParse<TRow>(rows, errors, null);
    }

    static ParsedEntryRow EntryRow(CsvRecord record)
    {
        var kind = record.Text("kind").ToLowerInvariant() switch
        {
            "expense" => CategoryKind.Expense,
            "income" => CategoryKind.Income,
            _ => throw new FormatException("Kind must be expense or income.")
        };
        var date = CsvValues.Date(record.Text("date"));
        var name = MoneyEntry.RequireName(record.Text("name"));
        var category = CategoryName.Normalize(record.Text("category"));
        var amount = CsvValues.Amount(record.Text("amount"));
        var currency = CsvValues.Currency(record.Text("currency"));
        var rate = RateFor(currency, CsvValues.Rate(record.Text("rate")));
        var note = MoneyEntry.NoteOf(record.Text("note"));
        RequireMoney(amount, currency, rate, date);
        return new ParsedEntryRow(record.Line, kind, date, name, category, amount, currency, rate, note);
    }

    static ParsedHoldingRow HoldingRow(CsvRecord record)
    {
        var month = CsvValues.Month(record.Text("month"));
        var name = BalanceSnapshot.RequireName(record.Text("holding"));
        var type = record.Text("type").ToLowerInvariant() switch
        {
            "cash" => HoldingType.Cash,
            "savings" => HoldingType.Savings,
            "investment" => HoldingType.Investment,
            _ => throw new FormatException("Type must be cash, savings or investment.")
        };
        var amount = CsvValues.Amount(record.Text("amount"));
        var currency = CsvValues.Currency(record.Text("currency"));
        var rate = RateFor(currency, CsvValues.Rate(record.Text("rate")));
        RequireMoney(amount, currency, rate, month.LastDay);
        return new ParsedHoldingRow(record.Line, month, name, type, amount, currency, rate);
    }

    static decimal? RateFor(string? currency, decimal? rate) => currency == Currencies.Pln ? null : rate;

    static void RequireMoney(decimal amount, string? currency, decimal? rate, DateOnly date) =>
        Money.Require(new Money(amount, currency ?? Currencies.Pln, rate ?? 1m, date), date);
}
