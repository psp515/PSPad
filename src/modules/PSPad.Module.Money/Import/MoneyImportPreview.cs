namespace PSPad.Module.Money.Import;

public sealed record MoneyImportPreview(
    Guid BudgetId, IReadOnlyList<EntryPreview> Entries, IReadOnlyList<HoldingPreview> Holdings,
    IReadOnlyList<MonthPreview> Months, IReadOnlyList<string> NewExpenseCategories,
    IReadOnlyList<string> NewIncomeCategories, IReadOnlyList<RateRequest> MissingRates)
{
    public bool IsWaitingForRates =>
        Entries.Any(row => row.Status == ImportStatus.WaitingForRate) ||
        Holdings.Any(row => row.Status == ImportStatus.WaitingForRate);

    public static MoneyImportPreview Build(
        CsvParse<ParsedEntryRow>? entries, CsvParse<ParsedHoldingRow>? balances, ImportContext context, RateBook rates,
        Func<Guid> newId)
    {
        var missing = new List<RateRequest>();
        var (entryRows, newExpense, newIncome) = entries is null
            ? ([], [], [])
            : EntryPreviewer.Build(entries, context, rates, missing);
        var (holdingRows, months) = balances is null
            ? ([], [])
            : BalancePreviewer.Build(balances, context, rates, missing, newId);

        return new MoneyImportPreview(context.Budget.Id, entryRows, holdingRows, months, newExpense, newIncome,
            [.. missing.Distinct()]);
    }
}
