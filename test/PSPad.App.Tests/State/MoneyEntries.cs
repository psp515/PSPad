using PSPad.Abstractions;
using PSPad.Module.Money;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;

namespace PSPad.App.Tests.State;

internal static class MoneyEntries
{
    public static Budget ABudget(string name)
    {
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, new CreateBudget(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), name),
            DateTimeOffset.UnixEpoch));
        return budget;
    }

    public static MoneyEntry Expense(Budget budget, string name, string category, decimal amount, DateOnly date,
        string currency = "PLN", decimal rate = 1m, DateTimeOffset? at = null) =>
        Recorded(budget, new RecordExpense(Guid.NewGuid(), budget.UserId, Guid.NewGuid(), budget.Id, name, category,
            new Money(amount, currency, rate, date), date, null), at ?? DateTimeOffset.UnixEpoch);

    public static MoneyEntry Income(Budget budget, string name, string category, decimal amount, DateOnly date,
        string currency = "PLN", decimal rate = 1m, DateTimeOffset? at = null) =>
        Recorded(budget, new RecordIncome(Guid.NewGuid(), budget.UserId, Guid.NewGuid(), budget.Id, name, category,
            new Money(amount, currency, rate, date), date, null), at ?? DateTimeOffset.UnixEpoch);

    static MoneyEntry Recorded(Budget budget, ICommand command, DateTimeOffset at)
    {
        var entry = new MoneyEntry();
        entry.ApplyAll(MoneyEntry.Decide(null, budget, command, at));
        return entry;
    }
}
