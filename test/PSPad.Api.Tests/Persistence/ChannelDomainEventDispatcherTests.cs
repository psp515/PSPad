using PSPad.Abstractions;
using PSPad.Infrastructure.Events;
using PSPad.TestInfrastructure;

namespace PSPad.Api.Tests.Persistence;

[UnitTest]
public class ChannelDomainEventDispatcherTests
{
    sealed record StubEvent(Guid AggregateId, Guid UserId, DateTimeOffset At)
        : DomainEvent(AggregateId, UserId, At);

    static DomainEventEnvelope Envelope(long seq) =>
        new(seq, new StubEvent(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow));

    [Fact]
    public async Task PublishedEventsQueueUpInOrder()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var dispatcher = new ChannelDomainEventDispatcher();

        await dispatcher.PublishAsync([Envelope(41), Envelope(42)], ct);

        Assert.Equal(41, (await dispatcher.Reader.ReadAsync(ct)).Seq);
        Assert.Equal(42, (await dispatcher.Reader.ReadAsync(ct)).Seq);
    }

    [Fact]
    public async Task TheCallersListMayBeClearedAsSoonAsPublishReturns()
    {
        var ct = global::Xunit.TestContext.Current.CancellationToken;
        var dispatcher = new ChannelDomainEventDispatcher();
        List<DomainEventEnvelope> published = [Envelope(7)];

        await dispatcher.PublishAsync(published, ct);
        published.Clear();

        Assert.Equal(7, (await dispatcher.Reader.ReadAsync(ct)).Seq);
    }
}
