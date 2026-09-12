namespace PSPad.Abstractions;

public abstract class Aggregate : IAggregate
{
    public Guid Id { get; protected set; }
    public Guid UserId { get; protected set; }
    public int Version { get; protected set; }
    public bool Deleted { get; protected set; }
    public long Seq { get; set; }

    public void Apply(DomainEvent @event)
    {
        When(@event);
        Version++;
    }

    public void ApplyAll(IReadOnlyList<DomainEvent> events)
    {
        foreach (var @event in events)
        {
            Apply(@event);
        }
    }

    protected abstract void When(DomainEvent @event);
}
