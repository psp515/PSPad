using System.Threading.Channels;
using PSPad.Abstractions;

namespace PSPad.Infrastructure.Events;

public sealed class ChannelDomainEventDispatcher : IDomainEventDispatcher
{
    readonly Channel<DomainEventEnvelope> _channel =
        Channel.CreateBounded<DomainEventEnvelope>(new BoundedChannelOptions(1024)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });

    public ChannelReader<DomainEventEnvelope> Reader => _channel.Reader;

    public async Task PublishAsync(IReadOnlyList<DomainEventEnvelope> events, CancellationToken ct)
    {
        foreach (var envelope in events)
        {
            await _channel.Writer.WriteAsync(envelope, ct);
        }
    }
}
