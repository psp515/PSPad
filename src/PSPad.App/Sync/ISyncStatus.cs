namespace PSPad.App.Sync;

public interface ISyncStatus
{
    bool IsSyncing { get; }

    bool LastSyncFailed { get; }

    DateTimeOffset? LastSyncedAt { get; }

    IReadOnlyList<string> LastRejections { get; }

    event Action? Changed;
}
