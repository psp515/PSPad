using PSPad.Abstractions;
using PSPad.Module.Money;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;

namespace PSPad.App.Tests.Pages;

internal static class MoneyEntries
{
    public static MoneyEntry Expense(Budget budget, string name, string category, decimal amount, DateOnly date,
        string currency = "PLN", decimal rate = 1m) =>
        Recorded(budget, new RecordExpense(Guid.NewGuid(), budget.UserId, Guid.NewGuid(), budget.Id, name, category,
            new Money(amount, currency, rate, date), date, null));

    public static MoneyEntry Income(Budget budget, string name, string category, decimal amount, DateOnly date,
        string currency = "PLN", decimal rate = 1m) =>
        Recorded(budget, new RecordIncome(Guid.NewGuid(), budget.UserId, Guid.NewGuid(), budget.Id, name, category,
            new Money(amount, currency, rate, date), date, null));

    static MoneyEntry Recorded(Budget budget, ICommand command)
    {
        var entry = new MoneyEntry();
        entry.ApplyAll(MoneyEntry.Decide(null, budget, command, DateTimeOffset.UnixEpoch));
        return entry;
    }
}
