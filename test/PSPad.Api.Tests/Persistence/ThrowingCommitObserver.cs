using PSPad.Abstractions;

namespace PSPad.Api.Tests.Persistence;

public sealed class ThrowingCommitObserver : ICommitObserver
{
    public Task CommittedAsync(Guid actorId, IReadOnlyList<CommittedChange> changes, CancellationToken ct) =>
        throw new InvalidOperationException("observer down");
}
