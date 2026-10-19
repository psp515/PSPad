using Microsoft.Extensions.Localization;
using PSPad.App.Resources;

namespace PSPad.App.Sync;

public static class SyncStampText
{
    public static (string Text, bool Failed) Describe(
        ISyncStatus status, bool online, DateTimeOffset now, IStringLocalizer<ShellStrings> text)
    {
        if (status.IsSyncing)
        {
            return (text["SyncUpdating"], false);
        }

        var failed = status.LastSyncFailed && online;

        if (status.LastSyncedAt is not { } at)
        {
            return (failed ? text["SyncCouldNotUpdate"] : online ? text["SyncNotYet"] : text["SyncOffline"], failed);
        }

        var ago = RelativeTime.Describe(at, now, text);
        var described = failed ? text["SyncFailedUpdatedAgo", ago]
            : online ? text["SyncUpdatedAgo", ago]
            : text["SyncOfflineUpdatedAgo", ago];

        return (described, failed);
    }
}
