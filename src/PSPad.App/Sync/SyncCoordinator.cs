using MudBlazor;
using PSPad.App.State.Outbox;

namespace PSPad.App.Sync;

public sealed class SyncCoordinator(SyncService sync, IConnectivity connectivity, IOutbox outbox, ISnackbar snackbar)
    : ISyncTrigger
{
    static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    bool _started;
    Task? _inFlight;
    bool _rerunRequested;

    public int PendingCount { get; private set; }

    public int Revision { get; private set; }

    public Task Started { get; private set; } = Task.CompletedTask;

    public event Action? Changed;

    public void Start()
    {
        if (!_started)
        {
            _started = true;
            connectivity.CameOnline += () => _ = SyncNowAsync();
            _ = LoopAsync();
        }

        // The shell starts the coordinator once for the signed-out redirect it renders and again
        // once signed in. Handing the second caller the first pull would hand it a task that
        // already finished with no session behind it.
        Started = SyncNowAsync();
    }

    async Task LoopAsync()
    {
        while (true)
        {
            await Task.Delay(PollInterval);
            if (connectivity.IsOnline)
            {
                await SyncNowAsync();
            }
        }
    }

    // A command handler and the poll loop can both call this within the same tick. PushAsync
    // peeks the outbox without removing entries until its response lands, so two overlapping
    // runs would peek and resend the same batch -- callers that arrive mid-run share the
    // in-flight task and get one guaranteed follow-up pass instead of a second parallel run.
    public Task SyncNowAsync()
    {
        if (_inFlight is { IsCompleted: false })
        {
            _rerunRequested = true;
            return _inFlight;
        }

        _inFlight = RunAsync();
        return _inFlight;
    }

    async Task RunAsync()
    {
        do
        {
            _rerunRequested = false;
            await RunOnceAsync();
        } while (_rerunRequested);
    }

    async Task RunOnceAsync()
    {
        if (connectivity.IsOnline)
        {
            try
            {
                var outcome = await sync.SyncAsync(CancellationToken.None);

                // Screens read the replica once and keep what they got. Nothing else would tell
                // one rendered from an empty replica -- every screen, right after a sign-in --
                // that its data has since arrived.
                if (outcome.Pulled > 0)
                {
                    Revision++;
                }

                foreach (var rejection in outcome.Rejections)
                {
                    // A rejection the user never sees is the same as a lost edit.
                    snackbar.Add(rejection, Severity.Warning);
                }
            }
            catch (HttpRequestException)
            {
            }
        }

        PendingCount = await outbox.CountAsync();
        Changed?.Invoke();
    }
}
