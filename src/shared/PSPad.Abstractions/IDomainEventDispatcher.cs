namespace PSPad.Abstractions;

public interface IDomainEventDispatcher
{
    Task PublishAsync(IReadOnlyList<DomainEventEnvelope> events, CancellationToken ct);
}
