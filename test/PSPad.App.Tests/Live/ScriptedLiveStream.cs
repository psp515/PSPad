using PSPad.App.Live;

namespace PSPad.App.Tests.Live;

public sealed class ScriptedLiveStream : ILiveStream
{
    readonly Queue<Func<CancellationToken, Task<Stream>>> _script = new();

    public int Opens { get; private set; }

    public ScriptedLiveStream Then(Func<CancellationToken, Task<Stream>> open)
    {
        _script.Enqueue(open);
        return this;
    }

    public ScriptedLiveStream ThenFail() =>
        Then(_ => Task.FromException<Stream>(new HttpRequestException("unreachable")));

    public ScriptedLiveStream ThenFeed(FeedStream feed) =>
        Then(_ => Task.FromResult<Stream>(feed));

    public async Task<Stream> OpenAsync(CancellationToken ct)
    {
        Opens++;
        if (_script.TryDequeue(out var open))
        {
            return await open(ct);
        }

        await Task.Delay(Timeout.Infinite, ct);
        throw new OperationCanceledException(ct);
    }
}
