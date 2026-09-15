using Microsoft.JSInterop;
using PSPad.Abstractions;

namespace PSPad.App.State;

public sealed class IndexedDbReplica(IJSRuntime js) : IReplica, IAsyncDisposable
{
    IJSObjectReference? _module;

    public async Task<T?> LoadAsync<T>(Guid id) where T : Aggregate
    {
        var module = await ModuleAsync();
        var row = await module.InvokeAsync<ReplicaRow?>("get", id);
        return row?.To<T>();
    }

    public async Task<IReadOnlyList<T>> LoadAllAsync<T>(Guid userId) where T : Aggregate
    {
        var module = await ModuleAsync();
        var rows = await module.InvokeAsync<ReplicaRow[]>("getAll", typeof(T).Name, userId);
        return rows.Select(row => row.To<T>()).ToArray();
    }

    public async Task SaveAsync(Aggregate aggregate)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("put", ReplicaRow.From(aggregate));
    }

    public async Task<long> MarkerAsync()
    {
        var module = await ModuleAsync();
        var stored = await module.InvokeAsync<MetaRow?>("getMeta", "marker");
        return stored?.Value ?? 0;
    }

    public async Task SetMarkerAsync(long marker)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("setMeta", "marker", marker);
    }

    public async Task<Guid?> OwnerAsync()
    {
        var module = await ModuleAsync();
        var stored = await module.InvokeAsync<OwnerRow?>("getMeta", "owner");
        return stored is null ? null : Guid.Parse(stored.Value);
    }

    public async Task SetOwnerAsync(Guid userId)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("setMeta", "owner", userId.ToString());
    }

    public async Task ClearAsync()
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("clearReplica");
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

    public sealed record MetaRow(string Key, long Value);

    public sealed record OwnerRow(string Key, string Value);
}
