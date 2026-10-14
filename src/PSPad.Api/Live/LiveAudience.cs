using PSPad.Abstractions;
using PSPad.Module.Money.Balance;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Money.Entries;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Api.Live;

public sealed class LiveAudience(IContainerMembers containers)
{
    public async Task<IReadOnlySet<Guid>> ForAsync(
        Guid actorId, IReadOnlyList<CommittedChange> changes, CancellationToken ct)
    {
        var audience = new HashSet<Guid> { actorId };
        var listIds = new HashSet<Guid>();
        var budgetIds = new HashSet<Guid>();

        foreach (var (aggregate, events) in changes)
        {
            audience.Add(aggregate.UserId);

            switch (aggregate)
            {
                case TaskList list:
                    audience.UnionWith(list.Members.Select(member => member.UserId));
                    audience.UnionWith(events.OfType<ListMemberRemoved>().Select(removed => removed.MemberId));
                    break;
                case TodoTask task:
                    AddList(listIds, task.ListId, task.PreviousListId);
                    break;
                case ReferenceItem item:
                    AddList(listIds, item.ListId, item.PreviousListId);
                    break;
                case Budget budget:
                    audience.UnionWith(budget.Members.Select(member => member.UserId));
                    audience.UnionWith(events.OfType<BudgetMemberRemoved>().Select(removed => removed.MemberId));
                    break;
                case MoneyEntry entry:
                    budgetIds.Add(entry.BudgetId);
                    break;
                case BalanceSnapshot snapshot:
                    budgetIds.Add(snapshot.BudgetId);
                    break;
            }
        }

        if (listIds.Count > 0)
        {
            AddAll(audience, await containers.ListsAsync(listIds, ct));
        }

        if (budgetIds.Count > 0)
        {
            AddAll(audience, await containers.BudgetsAsync(budgetIds, ct));
        }

        audience.Remove(Guid.Empty);
        return audience;
    }

    static void AddList(HashSet<Guid> listIds, Guid listId, Guid? previousListId)
    {
        listIds.Add(listId);
        if (previousListId is { } previous)
        {
            listIds.Add(previous);
        }
    }

    static void AddAll(HashSet<Guid> audience, IReadOnlyList<ContainerMembers> found)
    {
        foreach (var container in found)
        {
            audience.Add(container.OwnerId);
            audience.UnionWith(container.MemberIds);
        }
    }
}
