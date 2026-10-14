namespace PSPad.Abstractions;

public sealed record CommittedChange(Aggregate Aggregate, IReadOnlyList<DomainEvent> Events);
