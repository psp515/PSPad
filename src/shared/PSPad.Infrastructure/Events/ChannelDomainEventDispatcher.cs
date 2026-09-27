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

    readonly Lock _gate = new();
    readonly List<(long Target, TaskCompletionSource Done)> _drains = [];
    long _published;
    long _handled;

    public ChannelReader<DomainEventEnvelope> Reader => _channel.Reader;

    public async Task PublishAsync(IReadOnlyList<DomainEventEnvelope> events, CancellationToken ct)
    {
        foreach (var envelope in events)
        {
            // Counted before the write, so a drain racing this publish waits for it rather than missing it.
            Interlocked.Increment(ref _published);

            try
            {
                await _channel.Writer.WriteAsync(envelope, ct);
            }
            catch
            {
                Interlocked.Decrement(ref _published);
                throw;
            }
        }
    }

    public void MarkHandled()
    {
        lock (_gate)
        {
            _handled++;

            foreach (var drain in _drains.Where(drain => drain.Target <= _handled).ToList())
            {
                drain.Done.TrySetResult();
                _drains.Remove(drain);
            }
        }
    }

    public Task DrainAsync(CancellationToken ct)
    {
        var target = Interlocked.Read(ref _published);
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_gate)
        {
            if (_handled >= target)
            {
                return Task.CompletedTask;
            }

            _drains.Add((target, done));
        }

        return done.Task.WaitAsync(ct);
    }
}
