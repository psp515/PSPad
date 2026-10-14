using System.Threading.Channels;

namespace PSPad.Api.Live;

public sealed class LiveSubscription(ChannelReader<bool> nudges, Action close) : IDisposable
{
    int _closed;

    public ChannelReader<bool> Nudges => nudges;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 0)
        {
            close();
        }
    }
}
