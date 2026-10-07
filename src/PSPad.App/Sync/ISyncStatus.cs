namespace PSPad.App.Sync;

public interface ISyncStatus
{
    bool IsSyncing { get; }

    DateTimeOffset? LastSyncedAt { get; }

    event Action? Changed;
}
