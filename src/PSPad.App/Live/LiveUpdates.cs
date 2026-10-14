using PSPad.App.Sync;

namespace PSPad.App.Live;

public sealed class LiveUpdates(
    ILiveStream live,
    ISyncTrigger sync,
    IConnectivity connectivity,
    IPageVisibility visibility,
    LiveTiming timing) : IAsyncDisposable
{
    readonly CancellationTokenSource _stopping = new();
    TaskCompletionSource _wake = NewWake();
    CancellationTokenSource? _connection;
    Task? _loop;

    bool CanConnect => connectivity.IsOnline && visibility.IsVisible;

    public void Start()
    {
        if (_loop is not null)
        {
            return;
        }

        connectivity.Changed += Wake;
        visibility.Changed += Wake;
        _loop = RunAsync();
    }

    void Wake()
    {
        if (!CanConnect)
        {
            _connection?.Cancel();
        }

        _wake.TrySetResult();
    }

    async Task RunAsync()
    {
        var failures = 0;

        while (!_stopping.IsCancellationRequested)
        {
            _wake = NewWake();

            if (!CanConnect)
            {
                await WaitAsync(Timeout.InfiniteTimeSpan);
                continue;
            }

            var reachedReady = false;
            var cleanEnd = false;
            var connection = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
            _connection = connection;

            try
            {
                await ListenAsync(connection.Token, () => reachedReady = true);
                cleanEnd = true;
            }
            catch (Exception)
            {
            }
            finally
            {
                _connection = null;
                connection.Dispose();
            }

            if (reachedReady || cleanEnd)
            {
                failures = 0;
            }

            if (_stopping.IsCancellationRequested || cleanEnd || !CanConnect)
            {
                continue;
            }

            failures++;
            _wake = NewWake();
            await WaitAsync(timing.BackoffAfter(failures));
        }
    }

    async Task ListenAsync(CancellationToken ct, Action ready)
    {
        await using var stream = await live.OpenAsync(ct);
        using var silence = CancellationTokenSource.CreateLinkedTokenSource(ct);
        silence.CancelAfter(timing.Silence);

        await foreach (var frame in SseReader.FramesAsync(stream, silence.Token))
        {
            silence.CancelAfter(timing.Silence);

            if (frame == "ready")
            {
                ready();
            }

            if (frame is "ready" or "sync")
            {
                _ = sync.SyncNowAsync();
            }
        }
    }

    async Task WaitAsync(TimeSpan delay)
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
        var waited = delay == Timeout.InfiniteTimeSpan
            ? Task.Delay(Timeout.Infinite, cancel.Token)
            : timing.Delay(delay, cancel.Token);

        await Task.WhenAny(waited, _wake.Task);
        await cancel.CancelAsync();
    }

    static TaskCompletionSource NewWake() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async ValueTask DisposeAsync()
    {
        connectivity.Changed -= Wake;
        visibility.Changed -= Wake;
        await _stopping.CancelAsync();

        if (_loop is not null)
        {
            await _loop;
        }

        _stopping.Dispose();
    }
}
