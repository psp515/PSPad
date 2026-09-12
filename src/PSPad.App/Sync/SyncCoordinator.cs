using MudBlazor;

namespace PSPad.App.Sync;

public sealed class SyncCoordinator(SyncService sync, IConnectivity connectivity, State.IOutbox outbox, ISnackbar snackbar)
{
    static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    bool _started;

    public int PendingCount { get; private set; }

    public event Action? Changed;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        connectivity.CameOnline += () => _ = RunAsync();
        _ = RunAsync();
        _ = LoopAsync();
    }

    async Task LoopAsync()
    {
        while (true)
        {
            await Task.Delay(PollInterval);
            if (connectivity.IsOnline)
            {
                await RunAsync();
            }
        }
    }

    async Task RunAsync()
    {
        if (connectivity.IsOnline)
        {
            try
            {
                var outcome = await sync.SyncAsync(CancellationToken.None);
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
