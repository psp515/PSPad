using PSPad.Abstractions;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using static PSPad.Module.Money.Tests.Given;

namespace PSPad.Module.Money.Tests.Import;

internal sealed class PlanRunner(Budget budget)
{
    public List<MoneyEntry> Entries { get; } = [];

    public List<BalanceSnapshot> Snapshots { get; } = [];

    public void Run(IEnumerable<ICommand> commands)
    {
        foreach (var command in commands)
        {
            switch (command)
            {
                case RecordExpense or RecordIncome:
                    Entries.AddRange(MoneyEntryCascade.Write(null, budget, command, Now)
                        .Select(written => written.Aggregate).OfType<MoneyEntry>());
                    break;
                case StartBalanceMonth start:
                    var id = BalanceSnapshot.IdFor(budget.Id, start.Month);
                    var existing = Snapshots.FirstOrDefault(snapshot => snapshot.Id == id);
                    var snapshot = existing ?? new BalanceSnapshot();
                    snapshot.ApplyAll(BalanceSnapshot.Decide(existing, budget, Snapshots, start, Now));
                    if (existing is null)
                    {
                        Snapshots.Add(snapshot);
                    }

                    break;
                case AddHolding or EditHolding or RemoveHolding:
                    var month = command switch
                    {
                        AddHolding add => add.Month,
                        EditHolding edit => edit.Month,
                        _ => ((RemoveHolding)command).Month
                    };
                    var target = Snapshots.Single(snapshot => snapshot.Id == BalanceSnapshot.IdFor(budget.Id, month));
                    target.ApplyAll(BalanceSnapshot.Decide(target, budget, command, Now));
                    break;
            }
        }
    }
}
