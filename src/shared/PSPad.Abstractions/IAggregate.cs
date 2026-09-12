namespace PSPad.Abstractions;

public interface IAggregate
{
    Guid Id { get; }
    Guid UserId { get; }
    int Version { get; }
}
