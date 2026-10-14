namespace PSPad.Api.Live;

public interface IContainerMembers
{
    Task<IReadOnlyList<ContainerMembers>> ListsAsync(IReadOnlyCollection<Guid> listIds, CancellationToken ct);

    Task<IReadOnlyList<ContainerMembers>> BudgetsAsync(IReadOnlyCollection<Guid> budgetIds, CancellationToken ct);
}
