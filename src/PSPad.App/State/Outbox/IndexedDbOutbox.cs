using Microsoft.JSInterop;
using PSPad.Contracts;

namespace PSPad.App.State.Outbox;

public sealed class IndexedDbOutbox(IJSRuntime js) : IOutbox, IAsyncDisposable
{
    IJSObjectReference? _module;

    public async Task AppendAsync(Guid commandId, CommandEnvelope envelope)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("append", new PendingEntry(commandId, envelope));
    }

    public async Task<IReadOnlyList<OutboxEntry>> PeekAsync(int limit)
    {
        var module = await ModuleAsync();
        var rows = await module.InvokeAsync<StoredEntry[]>("peek", limit);
        return rows.Select(row => new OutboxEntry(row.Position, row.CommandId, row.Envelope)).ToArray();
    }

    public async Task RemoveThroughAsync(long position)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("removeThrough", position);
    }

    public async Task<int> CountAsync()
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<int>("count");
    }

    public async Task ClearAsync()
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("clearOutbox");
    }

    async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/replica.js");

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    public sealed record PendingEntry(Guid CommandId, CommandEnvelope Envelope);

    public sealed record StoredEntry(long Position, Guid CommandId, CommandEnvelope Envelope);
}
