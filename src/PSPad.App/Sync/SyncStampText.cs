namespace PSPad.App.Sync;

public static class SyncStampText
{
    public static (string Text, bool Failed) Describe(ISyncStatus status, bool online, DateTimeOffset now)
    {
        if (status.IsSyncing)
        {
            return ("Updating…", false);
        }

        var failed = status.LastSyncFailed && online;

        if (status.LastSyncedAt is not { } at)
        {
            return (failed ? "Couldn't update" : online ? "Not synced yet" : "Offline", failed);
        }

        var ago = RelativeTime.Describe(at, now);
        var text = failed ? $"Couldn't update · updated {ago}"
            : online ? $"Updated {ago}"
            : $"Offline · updated {ago}";

        return (text, failed);
    }
}
