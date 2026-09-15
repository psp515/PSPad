using PSPad.Contracts;

namespace PSPad.App.State;

public interface IOutbox
{
    Task AppendAsync(Guid commandId, CommandEnvelope envelope);

    Task<IReadOnlyList<OutboxEntry>> PeekAsync(int limit);

    Task RemoveThroughAsync(long position);

    Task<int> CountAsync();

    Task ClearAsync();
}
