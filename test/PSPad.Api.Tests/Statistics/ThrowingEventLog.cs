using PSPad.Contracts;
using PSPad.Module.Statistics;

namespace PSPad.Api.Tests.Statistics;

public sealed class ThrowingEventLog : IEventLog
{
    readonly TaskCompletionSource _attempted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Attempted => _attempted.Task;

    public Task<IReadOnlyList<RecordedEvent>> ReadForwardAsync(long afterSeq, int limit, CancellationToken ct)
    {
        _attempted.TrySetResult();
        throw new TimeoutException("the log is unreachable");
    }
}
