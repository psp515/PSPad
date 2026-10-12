using PSPad.Module.Money;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Values;

namespace PSPad.App.Tests.Pages;

internal static class Balances
{
    public static Holding AHolding(string name, decimal amount, string currency = "PLN", decimal rate = 1m,
        HoldingType type = HoldingType.Cash, DateOnly? rateDate = null) =>
        new(Guid.NewGuid(), name, type, new Money(amount, currency, rate, rateDate ?? new DateOnly(2026, 10, 31)));

    public static BalanceSnapshot Started(Budget budget, YearMonth month, params Holding[] holdings)
    {
        var snapshot = new BalanceSnapshot();
        snapshot.ApplyAll(BalanceSnapshot.Decide(null, budget,
            new StartBalanceMonth(Guid.NewGuid(), budget.UserId, budget.Id, month), DateTimeOffset.UnixEpoch));

        foreach (var holding in holdings)
        {
            snapshot.ApplyAll(BalanceSnapshot.Decide(snapshot, budget,
                new AddHolding(Guid.NewGuid(), budget.UserId, budget.Id, month, holding.HoldingId, holding.Name,
                    holding.Type, holding.Money), DateTimeOffset.UnixEpoch));
        }

        return snapshot;
    }

    public static BalanceSnapshot Deleted(BalanceSnapshot snapshot, Budget budget)
    {
        snapshot.ApplyAll(BalanceSnapshot.Decide(snapshot, budget,
            new DeleteBalanceMonth(Guid.NewGuid(), budget.UserId, budget.Id, snapshot.Month), DateTimeOffset.UnixEpoch));
        return snapshot;
    }

    public static Budget Archived(Budget budget)
    {
        budget.ApplyAll(Budget.Decide(budget, new ArchiveBudget(Guid.NewGuid(), budget.UserId, budget.Id),
            DateTimeOffset.UnixEpoch));
        return budget;
    }
}
