using PSPad.Abstractions;

namespace PSPad.Infrastructure.Events;

public sealed class NullDomainEventDispatcher : IDomainEventDispatcher
{
    public Task PublishAsync(IReadOnlyList<DomainEventEnvelope> events, CancellationToken ct) =>
        Task.CompletedTask;
}
