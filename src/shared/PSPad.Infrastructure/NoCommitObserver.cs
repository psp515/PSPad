using PSPad.Abstractions;

namespace PSPad.Infrastructure;

public sealed class NoCommitObserver : ICommitObserver
{
    public Task CommittedAsync(Guid actorId, IReadOnlyList<CommittedChange> changes, CancellationToken ct) =>
        Task.CompletedTask;
}
