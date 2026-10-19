using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace PSPad.Api.Live;

public sealed class LiveConnections(IOptions<LiveOptions> options)
{
    static readonly BoundedChannelOptions OneNudge = new(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true
    };

    readonly Lock _gate = new();
    readonly Dictionary<Guid, Dictionary<Guid, Channel<bool>>> _streams = [];

    public bool IsEmpty
    {
        get
        {
            lock (_gate)
            {
                return _streams.Count == 0;
            }
        }
    }

    public int OpenCount(Guid userId)
    {
        lock (_gate)
        {
            return _streams.TryGetValue(userId, out var streams) ? streams.Count : 0;
        }
    }

    public LiveSubscription? Open(Guid userId)
    {
        lock (_gate)
        {
            if (!_streams.TryGetValue(userId, out var streams))
            {
                streams = [];
                _streams[userId] = streams;
            }

            if (streams.Count >= options.Value.MaxStreamsPerUser)
            {
                return null;
            }

            var id = Guid.NewGuid();
            var channel = Channel.CreateBounded<bool>(OneNudge);
            streams[id] = channel;
            return new LiveSubscription(channel.Reader, () => Close(userId, id));
        }
    }

    public void Nudge(IEnumerable<Guid> userIds)
    {
        lock (_gate)
        {
            foreach (var userId in userIds)
            {
                if (_streams.TryGetValue(userId, out var streams))
                {
                    foreach (var channel in streams.Values)
                    {
                        channel.Writer.TryWrite(true);
                    }
                }
            }
        }
    }

    void Close(Guid userId, Guid id)
    {
        lock (_gate)
        {
            if (_streams.TryGetValue(userId, out var streams) && streams.Remove(id) && streams.Count == 0)
            {
                _streams.Remove(userId);
            }
        }
    }
}
