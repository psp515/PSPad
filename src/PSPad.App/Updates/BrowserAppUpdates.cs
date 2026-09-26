using Microsoft.JSInterop;

namespace PSPad.App.Updates;

public sealed class BrowserAppUpdates : IAppUpdates, IAsyncDisposable
{
    readonly Task<IJSObjectReference> _module;
    DotNetObjectReference<BrowserAppUpdates>? _reference;

    public BrowserAppUpdates(IJSRuntime js)
    {
        _module = SubscribeAsync(js);
    }

    public bool IsAvailable { get; private set; }

    public event Action? Available;

    public Task Subscribed => _module;

    async Task<IJSObjectReference> SubscribeAsync(IJSRuntime js)
    {
        var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/updates.js");
        _reference = DotNetObjectReference.Create(this);
        await module.InvokeVoidAsync("subscribe", _reference);
        return module;
    }

    [JSInvokable]
    public void OnUpdateAvailable()
    {
        if (IsAvailable)
        {
            return;
        }

        IsAvailable = true;
        Available?.Invoke();
    }

    public async Task ApplyAsync()
    {
        var module = await _module;
        await module.InvokeVoidAsync("activate");
    }

    public async ValueTask DisposeAsync()
    {
        _reference?.Dispose();
        if (_module.IsCompletedSuccessfully)
        {
            await _module.Result.DisposeAsync();
        }
    }
}
