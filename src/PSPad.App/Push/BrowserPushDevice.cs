using Microsoft.JSInterop;

namespace PSPad.App.Push;

public sealed class BrowserPushDevice(IJSRuntime js) : IPushDevice, IAsyncDisposable
{
    IJSObjectReference? _module;

    public async Task<PushSupport> SupportAsync()
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<PushSupport>("support");
    }

    public async Task<PushDeviceSubscription?> SubscribeAsync(string publicKey)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<PushDeviceSubscription?>("subscribe", publicKey);
    }

    public async Task<string?> UnsubscribeAsync()
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<string?>("unsubscribe");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/push.js");
}
