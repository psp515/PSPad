using PSPad.App.Sync;

namespace PSPad.App.Tests;

public sealed class GatedSyncTrigger : ISyncTrigger, ISyncStatus
{
    TaskCompletionSource? _gate;

    public int Calls { get; private set; }

    public bool IsSyncing { get; private set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    public event Action? Changed;

    public void Hold() => _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => _gate?.SetResult();

    public void Announce() => Changed?.Invoke();

    public async Task SyncNowAsync()
    {
        Calls++;
        IsSyncing = true;
        Changed?.Invoke();
        await (_gate?.Task ?? Task.CompletedTask);
        IsSyncing = false;
        Changed?.Invoke();
    }
}
