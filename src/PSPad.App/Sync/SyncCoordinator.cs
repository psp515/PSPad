using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using PSPad.Abstractions;
using PSPad.App.Api;
using PSPad.App.Localization;
using PSPad.App.Resources;
using PSPad.App.State;
using PSPad.App.State.Outbox;
using PSPad.App.State.Replica;

namespace PSPad.App.Sync;

public sealed class SyncCoordinator(
    SyncService sync,
    IConnectivity connectivity,
    IOutbox outbox,
    StatusBelts belts,
    NavigationManager navigation,
    IReplica replica,
    IClock clock,
    LanguagePreference languages,
    ILanguageApi api,
    IStringLocalizer<ShellStrings> text)
    : ISyncTrigger, ISyncStatus
{
    static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    bool _started;
    Task? _inFlight;
    bool _rerunRequested;
    readonly HashSet<Guid> _surfacedCommands = [];
    Guid? _stuckCommand;

    public int PendingCount { get; private set; }

    public int Revision { get; private set; }

    public bool IsSyncing { get; private set; }

    public bool LastSyncFailed { get; private set; }

    public DateTimeOffset? LastSyncedAt { get; private set; }

    public Task Started { get; private set; } = Task.CompletedTask;

    public IReadOnlyList<string> LastRejections { get; private set; } = [];

    public bool HasStuckChange => _stuckCommand is not null;

    public string RejectedText(int count) => text.Plural(count, "RejectedChanges");

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
        _ = LoadLastSyncedAtAsync();
        Started = SyncNowAsync();
    }

    public void Invalidate()
    {
        Revision++;
        Changed?.Invoke();
    }

    public async Task LoadLastSyncedAtAsync()
    {
        LastSyncedAt ??= await replica.LastSyncedAtAsync();
        Changed?.Invoke();
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
        IsSyncing = true;
        Changed?.Invoke();

        try
        {
            do
            {
                _rerunRequested = false;
                await RunOnceAsync();
            } while (_rerunRequested);
        }
        finally
        {
            IsSyncing = false;
            Changed?.Invoke();
        }
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
                LastSyncFailed = !outcome.ReachedServer;

                if (outcome.ReachedServer)
                {
                    LastSyncedAt = clock.UtcNow;
                    await replica.SetLastSyncedAtAsync(LastSyncedAt.Value);
                    await RetryPendingLanguageAsync();
                }

                if (outcome.Pulled > 0)
                {
                    Revision++;
                }

                // A rejection the user never sees is the same as a lost edit.
                if (outcome.Rejections.Count > 0)
                {
                    SurfaceRejections(outcome);
                }

                _stuckCommand = await StuckCommandAsync(outcome);
            }
            catch (ServerBusyException busy)
            {
                LastSyncFailed = false;
                _ = RetryAfterAsync(busy.RetryAfter);
            }
            catch (HttpRequestException)
            {
                LastSyncFailed = true;
            }
        }

        PendingCount = await outbox.CountAsync();
        Changed?.Invoke();
    }

    async Task RetryPendingLanguageAsync()
    {
        if (!languages.HasPendingWrite)
        {
            return;
        }

        var me = await api.SetLanguageAsync(languages.Language);
        if (me is not null && me.Language == languages.Language)
        {
            await languages.ClearPendingAsync();
        }
    }

    public async Task DiscardStuckChangeAsync()
    {
        if (_inFlight is { IsCompleted: false })
        {
            await _inFlight;
        }

        var head = (await outbox.PeekAsync(1)).FirstOrDefault();
        if (_stuckCommand is not { } stuck || head?.CommandId != stuck)
        {
            return;
        }

        await outbox.RemoveThroughAsync(head.Position);
        _stuckCommand = null;
        LastRejections = [];
        belts.Clear(BeltKind.Rejected);

        // The replica already shows the discarded change; only a full pull puts the server's copy back.
        await replica.SetMarkerAsync(0);
        await SyncNowAsync();
    }

    async Task<Guid?> StuckCommandAsync(SyncOutcome outcome)
    {
        var head = (await outbox.PeekAsync(1)).FirstOrDefault();
        return head is not null && outcome.RejectedCommands.Contains(head.CommandId) ? head.CommandId : null;
    }

    // Past the poll interval the regular loop retries anyway; a longer wait would only stack timers.
    async Task RetryAfterAsync(TimeSpan retryAfter)
    {
        await Task.Delay(retryAfter < PollInterval ? retryAfter : PollInterval);
        await SyncNowAsync();
    }

    void SurfaceRejections(SyncOutcome outcome)
    {
        var newlyRejected = outcome.RejectedCommands.Where(_surfacedCommands.Add).ToList();

        if (newlyRejected.Count > 0)
        {
            belts.Clear(BeltKind.Rejected);
        }

        LastRejections = [.. outcome.Rejections];
        belts.Show(BeltKind.Rejected, RejectedText(outcome.Rejections.Count), text["Details"], OpenDetailsAsync);
    }

    Task OpenDetailsAsync()
    {
        navigation.NavigateTo("settings#sync");
        return Task.CompletedTask;
    }
}
