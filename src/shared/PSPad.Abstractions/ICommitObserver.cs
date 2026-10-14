namespace PSPad.Abstractions;

public interface ICommitObserver
{
    Task CommittedAsync(Guid actorId, IReadOnlyList<CommittedChange> changes, CancellationToken ct);
}
