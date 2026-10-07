using PSPad.App.Sync;

namespace PSPad.App.Tests;

public sealed class GatedSyncTrigger : ISyncTrigger
{
    TaskCompletionSource? _gate;

    public int Calls { get; private set; }

    public void Hold() => _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Release() => _gate?.SetResult();

    public Task SyncNowAsync()
    {
        Calls++;
        return _gate?.Task ?? Task.CompletedTask;
    }
}
