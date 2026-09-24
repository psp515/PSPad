namespace PSPad.Abstractions;

public sealed record DomainEventEnvelope(long Seq, DomainEvent Event);
