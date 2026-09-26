namespace PSPad.Abstractions;

public interface IDomainEventHandler
{
    Task HandleAsync(DomainEventEnvelope envelope, CancellationToken ct);
}
