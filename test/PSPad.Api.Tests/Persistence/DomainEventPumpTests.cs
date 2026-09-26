using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PSPad.Abstractions;
using PSPad.Infrastructure.Events;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Persistence;

[UnitTest]
public class DomainEventPumpTests
{
    sealed record StubEvent(Guid AggregateId, Guid UserId, DateTimeOffset At)
        : DomainEvent(AggregateId, UserId, At);

    static DomainEventEnvelope Envelope(long seq) =>
        new(seq, new StubEvent(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow));

    sealed class RecordingHandler : IDomainEventHandler
    {
        readonly ConcurrentDictionary<long, TaskCompletionSource> _waits = new();

        public ConcurrentQueue<long> Seen { get; } = new();

        public Task Handled(long seq) => Waiter(seq).Task;

        public Task HandleAsync(DomainEventEnvelope envelope, CancellationToken ct)
        {
            Seen.Enqueue(envelope.Seq);
            Waiter(envelope.Seq).TrySetResult();
            return Task.CompletedTask;
        }

        TaskCompletionSource Waiter(long seq) =>
            _waits.GetOrAdd(seq, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    sealed class GatedHandler : IDomainEventHandler
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task HandleAsync(DomainEventEnvelope envelope, CancellationToken ct) => Release.Task;
    }

    sealed class ThrowingHandler : IDomainEventHandler
    {
        public Task HandleAsync(DomainEventEnvelope envelope, CancellationToken ct) =>
            throw new InvalidOperationException("projection is broken");
    }

    sealed class GatedReplay(RecordingHandler handler) : IDomainEventReplay
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int HandledBeforeCatchUp { get; private set; } = -1;

        public Task CatchUpAsync(CancellationToken ct)
        {
            HandledBeforeCatchUp = handler.Seen.Count;
            Entered.SetResult();
            return Release.Task;
        }
    }

    sealed class NoReplay : IDomainEventReplay
    {
        public Task CatchUpAsync(CancellationToken ct) => Task.CompletedTask;
    }

    static ServiceProvider ProviderFor(IDomainEventReplay replay, params IDomainEventHandler[] handlers)
    {
        var services = new ServiceCollection().AddSingleton(replay);

        foreach (var handler in handlers)
        {
            services.AddSingleton(handler);
        }

        return services.BuildServiceProvider();
    }

    static DomainEventPump PumpOver(ChannelDomainEventDispatcher dispatcher, IServiceProvider provider) =>
        new(dispatcher, provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DomainEventPump>.Instance);

    [Fact(Timeout = 30000)]
    public async Task NothingIsDeliveredUntilReplayHasCaughtUp()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var handler = new RecordingHandler();
        var replay = new GatedReplay(handler);
        var dispatcher = new ChannelDomainEventDispatcher();
        await using var provider = ProviderFor(replay, handler);
        var pump = PumpOver(dispatcher, provider);
        await dispatcher.PublishAsync([Envelope(1)], ct);

        await pump.StartAsync(ct);
        await replay.Entered.Task;

        Assert.Empty(handler.Seen);
        replay.Release.SetResult();
        await handler.Handled(1);
        await pump.StopAsync(ct);
        Assert.Equal(0, replay.HandledBeforeCatchUp);
        Assert.Equal([1], handler.Seen);
    }

    [Fact(Timeout = 30000)]
    public async Task AThrowingHandlerNeverStopsThePump()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var handler = new RecordingHandler();
        var dispatcher = new ChannelDomainEventDispatcher();
        await using var provider = ProviderFor(new NoReplay(), new ThrowingHandler(), handler);
        var pump = PumpOver(dispatcher, provider);

        await pump.StartAsync(ct);
        await dispatcher.PublishAsync([Envelope(1)], ct);
        await handler.Handled(1);
        await dispatcher.PublishAsync([Envelope(2)], ct);
        await handler.Handled(2);
        await pump.StopAsync(ct);

        Assert.Equal([1, 2], handler.Seen);
    }

    [Fact(Timeout = 30000)]
    public async Task ADrainReturnsOnlyOnceThePumpHasHandledWhatWasPublished()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var gate = new GatedHandler();
        var dispatcher = new ChannelDomainEventDispatcher();
        await using var provider = ProviderFor(new NoReplay(), gate);
        var pump = PumpOver(dispatcher, provider);
        await pump.StartAsync(ct);
        await dispatcher.PublishAsync([Envelope(1)], ct);

        var drained = dispatcher.DrainAsync(ct);
        await Task.Delay(100, ct);
        Assert.False(drained.IsCompleted);

        gate.Release.SetResult();
        await drained;
        await pump.StopAsync(ct);
    }

    [Fact(Timeout = 30000)]
    public async Task AThrowingHandlerStillCountsItsEventAsHandled()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var dispatcher = new ChannelDomainEventDispatcher();
        await using var provider = ProviderFor(new NoReplay(), new ThrowingHandler());
        var pump = PumpOver(dispatcher, provider);
        await pump.StartAsync(ct);
        await dispatcher.PublishAsync([Envelope(1)], ct);

        await dispatcher.DrainAsync(ct);
        await pump.StopAsync(ct);
    }
}
