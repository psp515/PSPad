using PSPad.Module.Money.Budgets;

namespace PSPad.Module.Money.Import;

internal static class EntryPreviewer
{
    public static (IReadOnlyList<EntryPreview> Rows, IReadOnlyList<string> NewExpense, IReadOnlyList<string> NewIncome)
        Build(CsvParse<ParsedEntryRow> parse, ImportContext context, RateBook rates, ICollection<RateRequest> missing)
    {
        var previews = parse.Errors
            .Select(error => new EntryPreview(error.Line, null, null, ImportStatus.Error, error.Message))
            .ToList();
        var added = new Dictionary<CategoryKind, List<string>> { [CategoryKind.Expense] = [], [CategoryKind.Income] = [] };
        var live = context.Entries
            .Where(entry => entry.BudgetId == context.Budget.Id && !entry.Deleted)
            .Select(entry => Key(entry.Kind, entry.Date, entry.Money.Amount, entry.Money.Currency, entry.Name))
            .ToHashSet();

        foreach (var row in parse.Rows)
        {
            var currency = row.Currency ?? context.DefaultCurrency;
            var (money, request, failure) = ImportMoney.Resolve(row.Amount, currency, row.Rate, row.Date, rates);

            if (request is not null)
            {
                missing.Add(request);
            }

            var status = !Admit(row, context.Budget, added[row.Kind])
                ? ImportStatus.Error
                : failure is not null
                    ? ImportStatus.Error
                    : money is null
                        ? ImportStatus.WaitingForRate
                        : live.Contains(Key(row.Kind, row.Date, row.Amount, currency, row.Name))
                            ? ImportStatus.AlreadyInBudget
                            : ImportStatus.ToImport;
            var error = status != ImportStatus.Error
                ? null
                : failure ?? $"A budget can have at most {Budget.MaxCategoriesPerKind} {row.Kind.ToString().ToLowerInvariant()} categories.";
            previews.Add(new EntryPreview(row.Line, row, status == ImportStatus.Error ? null : money, status, error));
        }

        return ([.. previews.OrderBy(preview => preview.Line)], added[CategoryKind.Expense], added[CategoryKind.Income]);
    }

    static bool Admit(ParsedEntryRow row, Budget budget, List<string> added)
    {
        var existing = budget.CategoriesOf(row.Kind);

        if (CategoryName.IndexIn(existing, row.Category) >= 0 || CategoryName.IndexIn(added, row.Category) >= 0)
        {
            return true;
        }

        if (existing.Count + added.Count >= Budget.MaxCategoriesPerKind)
        {
            return false;
        }

        added.Add(row.Category);
        return true;
    }

    static (CategoryKind, DateOnly, decimal, string, string) Key(
        CategoryKind kind, DateOnly date, decimal amount, string currency, string name) =>
        (kind, date, amount, currency, name.Trim().ToUpperInvariant());
}
