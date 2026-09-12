using PSPad.Contracts;

namespace PSPad.App.Api;

public interface IHistorySource
{
    Task<IReadOnlyList<HistoryEntry>> ReadAsync(long? before, int limit);
}
