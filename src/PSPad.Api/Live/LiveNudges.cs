using PSPad.Abstractions;

namespace PSPad.Api.Live;

public sealed class LiveNudges(LiveAudience audience, LiveConnections connections) : ICommitObserver
{
    public async Task CommittedAsync(Guid actorId, IReadOnlyList<CommittedChange> changes, CancellationToken ct)
    {
        if (connections.IsEmpty)
        {
            return;
        }

        connections.Nudge(await audience.ForAsync(actorId, changes, ct));
    }
}
