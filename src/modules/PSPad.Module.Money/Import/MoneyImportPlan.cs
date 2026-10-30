using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;

namespace PSPad.Module.Money.Import;

public static class MoneyImportPlan
{
    public static bool Includes(EntryPreview entry, IReadOnlySet<int> toggled) => entry.Status switch
    {
        ImportStatus.ToImport => !toggled.Contains(entry.Line),
        ImportStatus.AlreadyInBudget => toggled.Contains(entry.Line),
        _ => false
    };

    public static IReadOnlyList<PlannedCommand> Build(
        MoneyImportPreview preview, IReadOnlySet<int> toggled, Guid userId, Func<Guid> newId)
    {
        if (preview.IsWaitingForRates)
        {
            throw new InvalidOperationException("Rates are still loading.");
        }

        var budgetId = preview.BudgetId;
        var plan = new List<PlannedCommand>();

        foreach (var month in preview.Months)
        {
            if (month.IsNew)
            {
                plan.Add(new PlannedCommand(new StartBalanceMonth(newId(), userId, budgetId, month.Month), null));
            }

            foreach (var change in month.Changes)
            {
                var holding = change.Holding;
                plan.Add(new PlannedCommand(change.Kind switch
                {
                    HoldingChangeKind.Edit => new EditHolding(newId(), userId, budgetId, month.Month, holding.HoldingId,
                        holding.Name, holding.Type, holding.Money, holding.InvestmentAccountId),
                    HoldingChangeKind.Remove => new RemoveHolding(newId(), userId, budgetId, month.Month, holding.HoldingId),
                    _ => new AddHolding(newId(), userId, budgetId, month.Month, holding.HoldingId, holding.Name,
                        holding.Type, holding.Money)
                }, change.Line));
            }
        }

        foreach (var entry in preview.Entries.Where(entry => Includes(entry, toggled)))
        {
            var row = entry.Row!;
            plan.Add(new PlannedCommand(row.Kind == CategoryKind.Expense
                ? new RecordExpense(newId(), userId, newId(), budgetId, row.Name, row.Category, entry.Money!, row.Date, row.Note)
                : new RecordIncome(newId(), userId, newId(), budgetId, row.Name, row.Category, entry.Money!, row.Date, row.Note),
                row.Line));
        }

        return plan;
    }
}
