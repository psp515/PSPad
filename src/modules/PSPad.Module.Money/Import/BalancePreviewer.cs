namespace PSPad.Module.Money.Import;

internal static class BalancePreviewer
{
    public static (IReadOnlyList<HoldingPreview> Rows, IReadOnlyList<MonthPreview> Months) Build(
        CsvParse<ParsedHoldingRow> parse, ImportContext context, RateBook rates, ICollection<RateRequest> missing,
        Func<Guid> newId) =>
        ([], []);
}
