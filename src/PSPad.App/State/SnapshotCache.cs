using Microsoft.JSInterop;
using PSPad.Contracts;

namespace PSPad.App.State;

public sealed class SnapshotCache(IJSRuntime js) : ISnapshotCache, IAsyncDisposable
{
    IJSObjectReference? _module;

    public async Task SaveAsync(string token, SnapshotView snapshot, DateTimeOffset openedAt)
    {
        try
        {
            var module = await ModuleAsync();
            await module.InvokeVoidAsync("putSnapshot", new SnapshotRow(token, snapshot, openedAt));
        }
        catch (JSException)
        {
        }
    }

    public async Task<SnapshotView?> GetAsync(string token)
    {
        try
        {
            var module = await ModuleAsync();
            var row = await module.InvokeAsync<SnapshotRow?>("getSnapshot", token);
            return row?.Snapshot;
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<(string Token, SnapshotView Snapshot, DateTimeOffset OpenedAt)>> AllAsync()
    {
        try
        {
            var module = await ModuleAsync();
            var rows = await module.InvokeAsync<SnapshotRow[]>("allSnapshots");
            return rows.Select(row => (row.Token, row.Snapshot, row.OpenedAt)).ToArray();
        }
        catch (JSException)
        {
            return [];
        }
    }

    public async Task PruneAsync(DateTimeOffset now)
    {
        foreach (var (token, snapshot, _) in await AllAsync())
        {
            if (snapshot.ExpiresAt <= now)
            {
                await DeleteAsync(token);
            }
        }
    }

    public async Task ClearAsync()
    {
        foreach (var (token, _, _) in await AllAsync())
        {
            await DeleteAsync(token);
        }
    }

    async Task DeleteAsync(string token)
    {
        try
        {
            var module = await ModuleAsync();
            await module.InvokeVoidAsync("deleteSnapshot", token);
        }
        catch (JSException)
        {
        }
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

    sealed record SnapshotRow(string Token, SnapshotView Snapshot, DateTimeOffset OpenedAt);
}
