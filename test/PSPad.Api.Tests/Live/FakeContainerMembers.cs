using PSPad.Api.Live;

namespace PSPad.Api.Tests.Live;

public sealed class FakeContainerMembers : IContainerMembers
{
    public Dictionary<Guid, ContainerMembers> Lists { get; } = [];

    public Dictionary<Guid, ContainerMembers> Budgets { get; } = [];

    public int Lookups { get; private set; }

    public Task<IReadOnlyList<ContainerMembers>> ListsAsync(IReadOnlyCollection<Guid> listIds, CancellationToken ct) =>
        Find(Lists, listIds);

    public Task<IReadOnlyList<ContainerMembers>> BudgetsAsync(IReadOnlyCollection<Guid> budgetIds, CancellationToken ct) =>
        Find(Budgets, budgetIds);

    Task<IReadOnlyList<ContainerMembers>> Find(Dictionary<Guid, ContainerMembers> source, IReadOnlyCollection<Guid> ids)
    {
        Lookups++;
        IReadOnlyList<ContainerMembers> found = [.. ids.Where(source.ContainsKey).Select(id => source[id])];
        return Task.FromResult(found);
    }
}
