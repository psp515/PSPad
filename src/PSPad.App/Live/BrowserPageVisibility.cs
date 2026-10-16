using Microsoft.JSInterop;

namespace PSPad.App.Live;

public sealed class BrowserPageVisibility : IPageVisibility, IAsyncDisposable
{
    readonly IJSRuntime _js;
    IJSObjectReference? _module;
    DotNetObjectReference<BrowserPageVisibility>? _reference;

    public BrowserPageVisibility(IJSRuntime js)
    {
        _js = js;
        _ = InitializeAsync();
    }

    public bool IsVisible { get; private set; } = true;

    public event Action? Changed;

    async Task InitializeAsync()
    {
        _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/visibility.js");
        _reference = DotNetObjectReference.Create(this);

        if (!await _module.InvokeAsync<bool>("subscribe", _reference))
        {
            OnVisibilityChanged(false);
        }
    }

    [JSInvokable]
    public void OnVisibilityChanged(bool visible)
    {
        IsVisible = visible;
        Changed?.Invoke();
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
