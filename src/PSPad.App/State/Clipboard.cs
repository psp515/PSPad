using Microsoft.JSInterop;

namespace PSPad.App.State;

public sealed class Clipboard(IJSRuntime js) : IAsyncDisposable
{
    IJSObjectReference? _module;

    public async Task<bool> CopyAsync(string text)
    {
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/clipboard.js");
            await _module.InvokeVoidAsync("copy", text);
            return true;
        }
        catch (JSException)
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
