namespace PSPad.Abstractions;

public interface IUnitOfWork
{
    void Stage(Aggregate aggregate, IReadOnlyList<DomainEvent> events);

    Task CommitAsync(Guid commandId, Guid userId, CancellationToken ct);

    Task<bool> IsProcessedAsync(Guid commandId, CancellationToken ct);
}
