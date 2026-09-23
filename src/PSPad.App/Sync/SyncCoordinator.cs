using MudBlazor;
using PSPad.App.State.Outbox;

namespace PSPad.App.Sync;

public sealed class SyncCoordinator(SyncService sync, IConnectivity connectivity, IOutbox outbox, ISnackbar snackbar)
{
    static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    bool _started;

    public int PendingCount { get; private set; }

    public int Revision { get; private set; }

    public event Action? Changed;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        connectivity.CameOnline += () => _ = SyncNowAsync();
        _ = SyncNowAsync();
        _ = LoopAsync();
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

    public async Task SyncNowAsync()
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
