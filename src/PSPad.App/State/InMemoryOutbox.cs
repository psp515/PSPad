using PSPad.Contracts;

namespace PSPad.App.State;

public sealed class InMemoryOutbox : IOutbox
{
    readonly List<OutboxEntry> _entries = [];
    long _nextPosition = 1;

    public Task AppendAsync(Guid commandId, CommandEnvelope envelope)
    {
        _entries.Add(new OutboxEntry(_nextPosition++, commandId, envelope));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OutboxEntry>> PeekAsync(int limit) =>
        Task.FromResult<IReadOnlyList<OutboxEntry>>(_entries.Take(limit).ToArray());

    public Task RemoveThroughAsync(long position)
    {
        _entries.RemoveAll(entry => entry.Position <= position);
        return Task.CompletedTask;
    }

    public Task<int> CountAsync() => Task.FromResult(_entries.Count);

    public Task ClearAsync()
    {
        _entries.Clear();
        return Task.CompletedTask;
    }
}
