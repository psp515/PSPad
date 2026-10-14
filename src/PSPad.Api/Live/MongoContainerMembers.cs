using MongoDB.Driver;
using PSPad.Infrastructure.Mongo;
using PSPad.Module.Money.Budgets;
using PSPad.Module.Tasks.Lists;

namespace PSPad.Api.Live;

public sealed class MongoContainerMembers(MongoContext context) : IContainerMembers
{
    public async Task<IReadOnlyList<ContainerMembers>> ListsAsync(IReadOnlyCollection<Guid> listIds, CancellationToken ct) =>
        (await context.Collection<TaskList>()
            .Find(Builders<TaskList>.Filter.In(list => list.Id, listIds))
            .ToListAsync(ct))
        .Select(list => new ContainerMembers(list.UserId, [.. list.Members.Select(member => member.UserId)]))
        .ToArray();

    public async Task<IReadOnlyList<ContainerMembers>> BudgetsAsync(IReadOnlyCollection<Guid> budgetIds, CancellationToken ct) =>
        (await context.Collection<Budget>()
            .Find(Builders<Budget>.Filter.In(budget => budget.Id, budgetIds))
            .ToListAsync(ct))
        .Select(budget => new ContainerMembers(budget.UserId, [.. budget.Members.Select(member => member.UserId)]))
        .ToArray();
}
