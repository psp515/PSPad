using System.Text.Json.Serialization;

namespace PSPad.Abstractions;

public abstract record DomainEvent(Guid AggregateId, Guid UserId, DateTimeOffset At)
{
    public Guid? ActorId { get; init; }

    [JsonIgnore]
    public Guid Actor => ActorId ?? UserId;
}
