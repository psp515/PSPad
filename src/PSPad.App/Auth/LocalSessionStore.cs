using Microsoft.JSInterop;

namespace PSPad.App.Auth;

public sealed class LocalSessionStore(IJSRuntime js) : ILocalSessionStore, IAsyncDisposable
{
    IJSObjectReference? _module;

    public async Task<LocalSession?> LoadAsync()
    {
        var module = await ModuleAsync();
        var row = await module.InvokeAsync<SessionRow?>("load");
        return row?.Value;
    }

    public async Task SaveAsync(LocalSession session)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("save", session);
    }

    public async Task ClearAsync()
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("clear");
    }

    async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/session.js");

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    public sealed record SessionRow(string Key, LocalSession Value);
}
