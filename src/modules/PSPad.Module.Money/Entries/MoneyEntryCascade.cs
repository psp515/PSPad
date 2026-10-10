using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;

namespace PSPad.Module.Money.Entries;

public static class MoneyEntryCascade
{
    public static IReadOnlyList<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)> Write(
        MoneyEntry? entry, Budget? budget, ICommand command, DateTimeOffset at)
    {
        var entryEvents = MoneyEntry.Decide(entry, budget, command, at);
        var written = entry ?? new MoneyEntry();
        written.ApplyAll(entryEvents);

        if (command is DeleteEntry || entryEvents.Count == 0)
        {
            return [(written, entryEvents)];
        }

        var owner = budget!;
        var categoryEvents = Budget.Decide(owner,
            new AddCategory(command.CommandId, command.UserId, owner.Id, written.Kind, written.Category), at);
        owner.ApplyAll(categoryEvents);

        return categoryEvents.Count == 0
            ? [(written, entryEvents)]
            : [(owner, categoryEvents), (written, entryEvents)];
    }
}
