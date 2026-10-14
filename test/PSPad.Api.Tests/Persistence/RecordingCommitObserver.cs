using PSPad.Abstractions;

namespace PSPad.Api.Tests.Persistence;

public sealed class RecordingCommitObserver : ICommitObserver
{
    public List<(Guid ActorId, IReadOnlyList<CommittedChange> Changes)> Calls { get; } = [];

    public Task CommittedAsync(Guid actorId, IReadOnlyList<CommittedChange> changes, CancellationToken ct)
    {
        Calls.Add((actorId, changes));
        return Task.CompletedTask;
    }
}
