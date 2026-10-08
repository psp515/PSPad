using Microsoft.JSInterop;

namespace PSPad.App.Sync;

public sealed class BrowserConnectivity : IConnectivity, IAsyncDisposable
{
    static readonly TimeSpan DefaultRecheckEvery = TimeSpan.FromSeconds(10);

    readonly IJSRuntime _js;
    readonly IServerProbe _probe;
    readonly TimeSpan _recheckEvery;
    IJSObjectReference? _module;
    DotNetObjectReference<BrowserConnectivity>? _reference;
    bool _recovering;

    public BrowserConnectivity(IJSRuntime js, IServerProbe probe, TimeSpan? recheckEvery = null)
    {
        _js = js;
        _probe = probe;
        _recheckEvery = recheckEvery ?? DefaultRecheckEvery;
        _ = InitializeAsync();
    }

    public bool IsOnline { get; private set; } = true;

    public event Action? CameOnline;

    public event Action? Changed;

    async Task InitializeAsync()
    {
        _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/connectivity.js");
        _reference = DotNetObjectReference.Create(this);

        if (!await _module.InvokeAsync<bool>("subscribe", _reference))
        {
            await OnBrowserOffline();
        }
    }

    [JSInvokable]
    public void OnOnline()
    {
        IsOnline = true;
        CameOnline?.Invoke();
        Changed?.Invoke();
    }

    // navigator.onLine reports "offline" for plenty of working setups (a LAN-only server, a VPN
    // switch, a sleeping adapter) and never says otherwise again, so the server gets the last word.
    [JSInvokable]
    public async Task OnBrowserOffline()
    {
        if (!await _probe.ReachableAsync())
        {
            OnOffline();
        }
    }

    public void OnOffline()
    {
        IsOnline = false;
        Changed?.Invoke();

        if (!_recovering)
        {
            _recovering = true;
            _ = RecoverAsync();
        }
    }

    async Task RecoverAsync()
    {
        try
        {
            while (true)
            {
                await Task.Delay(_recheckEvery);

                if (IsOnline)
                {
                    return;
                }

                if (await _probe.ReachableAsync())
                {
                    OnOnline();
                    return;
                }
            }
        }
        finally
        {
            _recovering = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _reference?.Dispose();
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
