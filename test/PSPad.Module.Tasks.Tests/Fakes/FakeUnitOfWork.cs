using PSPad.Abstractions;

namespace PSPad.Module.Tasks.Tests.Fakes;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public List<(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events)> Staged { get; } = [];

    public bool Committed { get; private set; }

    public IReadOnlyList<DomainEvent> Events => Staged.SelectMany(entry => entry.Events).ToArray();

    readonly HashSet<Guid> _processed = [];

    public void Stage(Aggregate aggregate, IReadOnlyList<DomainEvent> events) =>
        Staged.Add((aggregate, events));

    public Task CommitAsync(Guid commandId, Guid userId, CancellationToken ct)
    {
        Committed = true;
        _processed.Add(commandId);
        return Task.CompletedTask;
    }

    public Task<bool> IsProcessedAsync(Guid commandId, CancellationToken ct) =>
        Task.FromResult(_processed.Contains(commandId));
}
