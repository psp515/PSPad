using PSPad.Abstractions;
using PSPad.Module.Money.Entries;

namespace PSPad.Module.Money.Budgets;

public static class CategoryCascade
{
    public static IReadOnlyList<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)> Rename(
        Budget? budget, RenameCategory command, IReadOnlyList<MoneyEntry> entries, DateTimeOffset at) =>
        Relabelled(budget, command, entries, at);

    public static IReadOnlyList<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)> Merge(
        Budget? budget, MergeCategory command, IReadOnlyList<MoneyEntry> entries, DateTimeOffset at) =>
        Relabelled(budget, command, entries, at);

    public static IReadOnlyList<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)> Remove(
        Budget? budget, RemoveCategory command, IReadOnlyList<MoneyEntry> entries, DateTimeOffset at)
    {
        var budgetEvents = Budget.Decide(budget, command, at);
        var removing = budget!;

        if (entries.Any(entry => entry.BudgetId == removing.Id && entry.Uses(command.Kind, command.Name)))
        {
            throw new DomainRejectedException("That category still has entries.");
        }

        removing.ApplyAll(budgetEvents);
        return [(removing, budgetEvents)];
    }

    static IReadOnlyList<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)> Relabelled(
        Budget? budget, ICommand command, IReadOnlyList<MoneyEntry> entries, DateTimeOffset at)
    {
        var budgetEvents = Budget.Decide(budget, command, at);
        var owner = budget!;
        owner.ApplyAll(budgetEvents);
        var relabels = budgetEvents.SelectMany(change => change switch
        {
            CategoryRenamed renamed => Relabel(owner, renamed.Kind, renamed.From, renamed.To, entries, at),
            CategoriesMerged merged => Relabel(owner, merged.Kind, merged.From, merged.Into, entries, at),
            _ => Enumerable.Empty<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)>()
        }).ToArray();
        return [(owner, budgetEvents), .. relabels];
    }

    static IEnumerable<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)> Relabel(
        Budget budget, CategoryKind kind, string from, string to, IReadOnlyList<MoneyEntry> entries, DateTimeOffset at)
    {
        foreach (var entry in entries.Where(entry => entry.BudgetId == budget.Id && entry.Uses(kind, from)).ToArray())
        {
            var events = MoneyEntry.Relabel(entry, to, at);
            entry.ApplyAll(events);

            if (events.Count > 0)
            {
                yield return (entry, events);
            }
        }
    }
}
