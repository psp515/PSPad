using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Money.Values;

namespace PSPad.Module.Money.Tests;

internal static class Given
{
    public static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Member = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid Stranger = Guid.Parse("99999999-9999-9999-9999-999999999999");
    public const string InviteToken = "abcdefghijklmnopqrstuvwxyz";
    public const string InviteCode = "K7M4PX";
    public static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 10, 9);

    public static Budget ABudget()
    {
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, new CreateBudget(Guid.NewGuid(), Owner, Guid.NewGuid(), "Personal"), Now));
        return budget;
    }

    public static Budget Share(Budget budget)
    {
        budget.ApplyAll(Budget.Decide(budget,
            new ShareBudget(Guid.NewGuid(), budget.UserId, budget.Id, InviteToken, InviteCode, "Lukasz Kolber"), Now));
        return budget;
    }

    public static Budget Join(Budget budget, Guid? who = null)
    {
        budget.ApplyAll(Budget.Decide(budget,
            new JoinBudget(Guid.NewGuid(), who ?? Member, budget.Id, InviteToken, InviteCode, "Anna Nowak"), Now));
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

    public static readonly YearMonth October = new(2026, 10);
    public static readonly YearMonth November = new(2026, 11);

    public static Holding AHolding(
        string name = "Wallet", decimal amount = 100m, string currency = "PLN", decimal rate = 1m,
        DateOnly? rateDate = null, HoldingType type = HoldingType.Cash) =>
        new(Guid.NewGuid(), name, type, new Money(amount, currency, rate, rateDate ?? Today));

    public static BalanceSnapshot ASnapshot(
        Budget budget, YearMonth month, IReadOnlyList<Holding>? holdings = null, DateTimeOffset? at = null)
    {
        var when = at ?? Now;
        var snapshot = new BalanceSnapshot();
        snapshot.ApplyAll(BalanceSnapshot.Decide(null, budget,
            new StartBalanceMonth(Guid.NewGuid(), budget.UserId, budget.Id, month), when));

        foreach (var holding in holdings ?? [])
        {
            snapshot.ApplyAll(BalanceSnapshot.Decide(snapshot, budget,
                new AddHolding(Guid.NewGuid(), budget.UserId, budget.Id, month, holding.HoldingId, holding.Name,
                    holding.Type, holding.Money, holding.InvestmentAccountId), when));
        }

        return snapshot;
    }

    public static BalanceSnapshot DeleteSnapshot(BalanceSnapshot snapshot, Budget budget)
    {
        snapshot.ApplyAll(BalanceSnapshot.Decide(snapshot, budget,
            new DeleteBalanceMonth(Guid.NewGuid(), budget.UserId, budget.Id, snapshot.Month), Now));
        return snapshot;
    }
}
