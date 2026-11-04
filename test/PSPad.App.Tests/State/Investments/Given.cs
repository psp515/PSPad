using PSPad.Module.Investments.Accounts;
using PSPad.Module.Money;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Values;

namespace PSPad.App.Tests.State.Investments;

internal static class Given
{
    public static readonly Guid User = Guid.NewGuid();
    static readonly DateTimeOffset Now = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    static readonly DateOnly Day = new(2026, 10, 9);

    public static InvestmentAccount AnAccount(string name = "IKE", string currency = "PLN", Guid? owner = null, bool archived = false)
    {
        var account = new InvestmentAccount();
        var id = Guid.NewGuid();
        var by = owner ?? User;
        account.ApplyAll(InvestmentAccount.Decide(null,
            new CreateInvestmentAccount(Guid.NewGuid(), by, id, name, currency, Broker.Xtb, null), Now));
        if (archived)
        {
            account.ApplyAll(InvestmentAccount.Decide(account, new ArchiveInvestmentAccount(Guid.NewGuid(), by, id), Now));
        }

        return account;
    }

    public static Budget ABudget()
    {
        var budget = new Budget();
        budget.ApplyAll(Budget.Decide(null, new CreateBudget(Guid.NewGuid(), User, Guid.NewGuid(), "Home"), Now));
        return budget;
    }

    public static BalanceSnapshot ASnapshot(Budget budget, YearMonth month, params Holding[] holdings)
    {
        var snapshot = new BalanceSnapshot();
        snapshot.ApplyAll(BalanceSnapshot.Decide(null, budget, new StartBalanceMonth(Guid.NewGuid(), User, budget.Id, month), Now));
        foreach (var holding in holdings)
        {
            snapshot.ApplyAll(BalanceSnapshot.Decide(snapshot, budget,
                new AddHolding(Guid.NewGuid(), User, budget.Id, month, holding.HoldingId, holding.Name, holding.Type, holding.Money,
                    holding.InvestmentAccountId), Now));
        }

        return snapshot;
    }

    public static Holding Linked(InvestmentAccount account, string? name = null) =>
        new(Guid.NewGuid(), name ?? account.Name, HoldingType.Investment, new Money(10m, account.Currency, 1m, Day), account.Id);
}
