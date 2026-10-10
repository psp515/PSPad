using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;

namespace PSPad.Module.Money.Tests;

internal static class Given
{
    public static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Stranger = Guid.Parse("99999999-9999-9999-9999-999999999999");
    public static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 10, 9);

    public static Budget ABudget()
    {
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, new CreateBudget(Guid.NewGuid(), Owner, Guid.NewGuid(), "Personal"), Now));
        return budget;
    }

    public static Budget Archive(Budget budget)
    {
        budget.ApplyAll(Budget.Decide(budget, new ArchiveBudget(Guid.NewGuid(), Owner, budget.Id), Now));
        return budget;
    }

    public static MoneyEntry AnExpense(
        Budget budget, string category = "Food", decimal amount = 10m, DateOnly? date = null, Money? money = null,
        DateTimeOffset? at = null, string name = "Groceries")
    {
        var on = date ?? Today;
        return Recorded(budget, new RecordExpense(Guid.NewGuid(), budget.UserId, Guid.NewGuid(), budget.Id, name,
            category, money ?? new Money(amount, "PLN", 1m, on), on, null), at ?? Now);
    }

    public static MoneyEntry AnIncome(
        Budget budget, string category = "Salary", decimal amount = 10m, DateOnly? date = null, Money? money = null,
        DateTimeOffset? at = null, string name = "Pay")
    {
        var on = date ?? Today;
        return Recorded(budget, new RecordIncome(Guid.NewGuid(), budget.UserId, Guid.NewGuid(), budget.Id, name,
            category, money ?? new Money(amount, "PLN", 1m, on), on, null), at ?? Now);
    }

    public static MoneyEntry Delete(MoneyEntry entry, Budget budget)
    {
        entry.ApplyAll(MoneyEntry.Decide(entry, budget, new DeleteEntry(Guid.NewGuid(), Owner, entry.Id), Now));
        return entry;
    }

    static MoneyEntry Recorded(Budget budget, PSPad.Abstractions.ICommand command, DateTimeOffset at)
    {
        var entry = new MoneyEntry();
        entry.ApplyAll(MoneyEntry.Decide(null, budget, command, at));
        return entry;
    }
}
