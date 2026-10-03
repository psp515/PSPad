using Microsoft.Extensions.Logging;
using PSPad.Abstractions;
using PSPad.Module.Sharing.Ports;
using PSPad.Module.Tasks.Lists;
using PSPad.Module.Tasks.References;
using PSPad.Module.Tasks.Tasks;

namespace PSPad.Module.Sharing.Snapshots;

public sealed class SnapshotMarking(
    ISnapshotStore snapshots, IServerCommands commands, IClock clock, ILogger<SnapshotMarking> logger)
{
    public async Task<MarkOutcome> MarkAsync(string token, Guid entryId, Guid? stepId, bool marked, CancellationToken ct)
    {
        var snapshot = await snapshots.FindByTokenAsync(token, ct);
        var now = clock.UtcNow;
        if (snapshot is null || !snapshot.IsLiveAt(now) || !snapshot.Holds(entryId, stepId))
        {
            return MarkOutcome.NotFound;
        }

        var updated = snapshot.WithMark(entryId, stepId, marked, now);
        if (ReferenceEquals(updated, snapshot))
        {
            return MarkOutcome.Unchanged;
        }

        await snapshots.SaveAsync(updated, ct);

        ICommand command = snapshot.Kind == ListKind.Tasks
            ? new MarkTaskFromSnapshot(Guid.NewGuid(), snapshot.UserId, entryId, stepId, snapshot.Id, marked)
            : new MarkReferenceItemFromSnapshot(Guid.NewGuid(), snapshot.UserId, entryId, snapshot.Id, marked);

        var result = await commands.RunAsync(command, ct);
        if (!result.Accepted)
        {
            logger.LogInformation(
                "Snapshot mark for {EntryId} did not reach the list: {Reason}", entryId, result.Rejection);
        }

        return MarkOutcome.Marked;
    }
}
