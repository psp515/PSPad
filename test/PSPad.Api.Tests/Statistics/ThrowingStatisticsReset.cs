using PSPad.Module.Statistics;

namespace PSPad.Api.Tests.Statistics;

public sealed class ThrowingStatisticsReset : IStatisticsReset
{
    readonly TaskCompletionSource _attempted = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Attempted => _attempted.Task;

    public Task ClearAsync(CancellationToken ct)
    {
        _attempted.TrySetResult();
        throw new InvalidOperationException("Drop failed.");
    }
}
