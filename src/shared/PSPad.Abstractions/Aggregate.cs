using System.Text.Json.Serialization;

namespace PSPad.Abstractions;

public abstract class Aggregate : IAggregate
{
    [JsonInclude]
    public Guid Id { get; protected set; }

    [JsonInclude]
    public Guid UserId { get; protected set; }

    [JsonInclude]
    public int Version { get; protected set; }

    [JsonInclude]
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
