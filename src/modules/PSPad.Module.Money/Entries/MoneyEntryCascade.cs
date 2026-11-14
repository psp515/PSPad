using PSPad.Abstractions;
using PSPad.Module.Money.Budgets;

namespace PSPad.Module.Money.Entries;

public static class MoneyEntryCascade
{
    public static IReadOnlyList<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)> Write(
        MoneyEntry? entry, Budget? budget, IReadOnlyList<MoneyEntry> entries, ICommand command, DateTimeOffset at)
    {
        var returns = entry is null || entry.IsReturn ? null : ReturnState.Of(entry, entries);

        if (command is EditEntry edit && entry is { Deleted: false, IsReturn: false } && returns is not null)
        {
            BudgetAccess.Writable(BudgetAccess.To(budget, command.UserId));
            MoneyEntry.GuardPurchase(entry, edit, returns);
        }

        var entryEvents = MoneyEntry.Decide(entry, budget, command, at);
        var written = entry ?? new MoneyEntry();
        written.ApplyAll(entryEvents);

        if (entryEvents.Count == 0)
        {
            return [(written, entryEvents)];
        }

        var followers = returns is null ? [] : Followers(written, returns, command, at);

        if (command is DeleteEntry)
        {
            return [(written, entryEvents), .. followers];
        }

        var owner = budget!;
        var categoryEvents = Budget.Decide(owner,
            new AddCategory(command.CommandId, command.UserId, owner.Id, written.Kind, written.Category), at);
        owner.ApplyAll(categoryEvents);

        return categoryEvents.Count == 0
            ? [(written, entryEvents), .. followers]
            : [(owner, categoryEvents), (written, entryEvents), .. followers];
    }

    static IReadOnlyList<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)> Followers(
        MoneyEntry purchase, ReturnState returns, ICommand command, DateTimeOffset at) =>
    [
        .. returns.Returns
            .Select(returned => (returned, Events: FollowerEvents(purchase, returned, command, at)))
            .Where(follower => follower.Events.Count > 0)
            .Select(follower =>
            {
                follower.returned.ApplyAll(follower.Events);
                return ((Aggregate)follower.returned, follower.Events);
            })
    ];

    static IReadOnlyList<DomainEvent> FollowerEvents(
        MoneyEntry purchase, MoneyEntry returned, ICommand command, DateTimeOffset at) =>
        command switch
        {
            DeleteEntry => [new MoneyEntryDeleted(returned.Id, returned.UserId, at)],
            EditEntry when returned.Name != purchase.Name || returned.Category != purchase.Category =>
                [new MoneyEntryRelabelled(returned.Id, returned.UserId, at, purchase.Name, purchase.Category)],
            _ => []
        };
}
