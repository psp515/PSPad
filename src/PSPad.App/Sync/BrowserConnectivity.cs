using Microsoft.JSInterop;

namespace PSPad.App.Sync;

public sealed class BrowserConnectivity : IConnectivity, IAsyncDisposable
{
    readonly IJSRuntime _js;
    IJSObjectReference? _module;
    DotNetObjectReference<BrowserConnectivity>? _reference;

    public BrowserConnectivity(IJSRuntime js)
    {
        _js = js;
        _ = InitializeAsync();
    }

    public bool IsOnline { get; private set; } = true;

    public event Action? CameOnline;

    async Task InitializeAsync()
    {
        _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/connectivity.js");
        _reference = DotNetObjectReference.Create(this);
        IsOnline = await _module.InvokeAsync<bool>("subscribe", _reference);
    }

    [JSInvokable]
    public void OnOnline()
    {
        IsOnline = true;
        CameOnline?.Invoke();
    }

    [JSInvokable]
    public void OnOffline() => IsOnline = false;

    public async ValueTask DisposeAsync()
    {
        _reference?.Dispose();
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
